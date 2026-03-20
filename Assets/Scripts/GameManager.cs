using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Linq;
using System;
using Unity.Mathematics;
using TMPro;


public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    double tickTimer = 0;
    double lastFrameTime = 0;

    public const double TickTime = 1f / 60f;

    public int CurrentFrame = 0;
    // last recieved frame from opponent
    int lastRemoteFrame = 0;

    public int MaxFrame = 6000;

    public static bool IsHost => ConnectionManager.Instance?.Role == ConnectionManager.Roles.Host;

    [SerializeField]
    public PlayerCapsule[] players = new PlayerCapsule[2];
    [SerializeField]
    TextMeshProUGUI[] scores = new TextMeshProUGUI[2];
    ushort scoreA = 0;
    ushort scoreB = 0;

    List<PlayerState> localPlayerHistory = new();
    List<PlayerState> remotePlayerHistory = new();

    List<NetworkInput> localInputHistory = new();
    List<NetworkInput> remoteInputHistory = new();

    [SerializeField]
    InputActionReference moveInputRef;
    [SerializeField]
    InputActionReference attackInputRef;

    [SerializeField] PlayerState[] debugLastPlayerAStates = new PlayerState[10];
    [SerializeField] PlayerState[] debugLastPlayerBStates = new PlayerState[10];
    [SerializeField] NetworkInput[] debugLastInuptsLocal = new NetworkInput[10];
    [SerializeField] NetworkInput[] debugLastInuptsRemote = new NetworkInput[10];

    bool gameStarted = false;

    private void Awake()
    {
        Instance = this;
    }
    private void Start()
    {
        ConnectionManager.Instance.OnInputDataReceived += OnInputDataReceived;
        ConnectionManager.Instance.OnStateSyncCheckReceived += OnStateSyncCheckReceived;
        ConnectionManager.Instance.OnConnectionFound += OnGameStart;
    }

    void OnGameStart()
    {
        players[0].LocalState = new PlayerState(true);
        players[1].LocalState = new PlayerState(false);

        localInputHistory.Clear();
        remoteInputHistory.Clear();

        localPlayerHistory.Clear();
        remotePlayerHistory.Clear();

        CurrentFrame = 0;
        lastRemoteFrame = 0;

        if (IsHost)
            Tick(0, players[0].LocalState, players[1].LocalState, 0, 0);
        else
            Tick(0, players[1].LocalState, players[0].LocalState, 0, 0);

        lastFrameTime = Time.realtimeSinceStartupAsDouble;
        gameStarted = true;
    }
    void Update()
    {
        if (!gameStarted)
            return;

        tickTimer += Time.realtimeSinceStartupAsDouble - lastFrameTime;
        lastFrameTime = Time.realtimeSinceStartupAsDouble;

        // while to catch up after lag spikes
        while (tickTimer >= TickTime)
        {
            tickTimer -= TickTime;
            CurrentFrame++;

            NetworkInput localInput = GetLocalInput();
            // predict current input same as last frame
            NetworkInput remoteInput = remoteInputHistory[math.max(0, CurrentFrame - 1)];

            Tick(CurrentFrame, localPlayerHistory[CurrentFrame], remotePlayerHistory[CurrentFrame], localInput, remoteInput);

            // new game condition
            if (IsOnePlayerDead()  || CurrentFrame >= MaxFrame)
            {
                UpdateScore();

                OnGameStart();
            }

            SendRecentInputs();

            if (CurrentFrame % 30 == 0)
                SendStateSyncCheck();     
        }

        // history debug
        if (CurrentFrame > 10)
        {
            debugLastInuptsLocal = localInputHistory.GetRange(math.max(0, CurrentFrame - 11), 10).Reverse<NetworkInput>().ToArray();
            debugLastInuptsRemote = remoteInputHistory.GetRange(math.max(0, CurrentFrame - 11), 10).Reverse<NetworkInput>().ToArray();

            debugLastPlayerAStates = localPlayerHistory.GetRange(math.max(0, CurrentFrame - 11), 10).Reverse<PlayerState>().ToArray();
            debugLastPlayerBStates = remotePlayerHistory.GetRange(math.max(0, CurrentFrame - 11), 10).Reverse<PlayerState>().ToArray();
        }
        
        UpdatePlayerCapsules();
    }

    void UpdateScore()
    {
        UpdatePlayerCapsules();
        // both alive
        if (localPlayerHistory[lastRemoteFrame].State != ActionStates.Dead && remotePlayerHistory[lastRemoteFrame].State != ActionStates.Dead)
        {
            if (players[0].LocalState.HP >= players[1].LocalState.HP)
                scoreA++;
            else if (players[1].LocalState.HP >= players[0].LocalState.HP)
                scoreB++;
        }
        else
        {
            if (players[0].LocalState.State == ActionStates.Dead)
                scoreB++;

            if (players[1].LocalState.State == ActionStates.Dead)
                scoreA++;
        }

        scores[0].text = scoreA.ToString();
        scores[1].text = scoreB.ToString();
    }

    bool IsOnePlayerDead()
    {
        return CurrentFrame > ConnectionManager.NetworkHistorySize &&

            (localPlayerHistory[CurrentFrame - ConnectionManager.NetworkHistorySize].State == ActionStates.Dead ||
            remotePlayerHistory[CurrentFrame - ConnectionManager.NetworkHistorySize].State == ActionStates.Dead) &&

            (localPlayerHistory[lastRemoteFrame].State == ActionStates.Dead ||
            remotePlayerHistory[lastRemoteFrame].State == ActionStates.Dead);
    }
    void UpdatePlayerCapsules()
    {
        if (IsHost)
        {
            players[0].LocalState = localPlayerHistory[CurrentFrame];
            players[1].LocalState = remotePlayerHistory[CurrentFrame];
        }
        else
        {
            players[1].LocalState = localPlayerHistory[CurrentFrame];
            players[0].LocalState = remotePlayerHistory[CurrentFrame];
        }
    }
    void SendRecentInputs()
    {
        int networkHistorySize = Math.Min(CurrentFrame + 1, ConnectionManager.NetworkHistorySize);
        ConnectionManager.Instance.SendInputData(CurrentFrame, localInputHistory.GetRange(CurrentFrame + 1 - networkHistorySize, networkHistorySize).Reverse<NetworkInput>().ToArray());
    }
    
    void SendStateSyncCheck()
    {
        // -n to account for lastRemoteFrame differences between machines
        int frame = math.max(0, lastRemoteFrame - 20);

        PlayerState syncedStateA = IsHost ? localPlayerHistory[frame] : remotePlayerHistory[frame];
        PlayerState syncedStateB = IsHost ? remotePlayerHistory[frame] : localPlayerHistory[frame];

        ConnectionManager.Instance.SendStateSyncCheck(frame, syncedStateA, syncedStateB);
    }



    void Tick(int frame, PlayerState localPlayerState, PlayerState remotePlayerState, NetworkInput localInput, NetworkInput remoteInput)
    {
        RecordInputHistory(frame, localInput, remoteInput);

        RecordStateHistory(frame, localPlayerState, remotePlayerState);

        // host vs client as player capsules 1 and 2
        PlayerCapsule localP = IsHost? players[0] : players[1];
        PlayerCapsule remoteP = !IsHost? players[0] : players[1];


        if (IsHost)
        {
            localP.Tick(ref localPlayerState, localInput, remotePlayerState);
            remoteP.Tick(ref remotePlayerState, remoteInput, localPlayerState);
        }
        else
        {
            remoteP.Tick(ref remotePlayerState, remoteInput, localPlayerState);
            localP.Tick(ref localPlayerState, localInput, remotePlayerState);
        }
        
        RecordStateHistory(frame + 1, localPlayerState, remotePlayerState);
    }
    

    NetworkInput GetLocalInput()
    {
        NetworkInput input = new();

        if (attackInputRef.action.IsPressed())
            input |= NetworkInput.Attack;


        Vector2 moveI = moveInputRef.action.ReadValue<Vector2>();
        if (moveI.x > 0.5f)
            input |= NetworkInput.Right;
        else if (moveI.x < -0.5f)
            input |= NetworkInput.Left;

        if (moveI.y > 0.5f)
            input |= NetworkInput.Up;
        else if (moveI.y < -0.5f)
            input |= NetworkInput.Down;

        return input;
    }

    #region Rollback Region
    void RecordStateHistory(int frame, PlayerState localState, PlayerState remoteState)
    {
        // tick writes the movement result to the next frame, not current
        if (frame > CurrentFrame + 1)
            return;
        if (frame == localPlayerHistory.Count)
        {
            localPlayerHistory.Add(localState);
            remotePlayerHistory.Add(remoteState);
        }
        else
        {
            localPlayerHistory[frame] = localState;
            remotePlayerHistory[frame] = remoteState;
        }
    }
    void RecordInputHistory(int frame, NetworkInput localInput, NetworkInput remoteInput)
    {
        if (frame > CurrentFrame)
            return;
        if (localInputHistory.Count == frame)
        {
            localInputHistory.Add(localInput);
            remoteInputHistory.Add(remoteInput);
        }
        else
        {
            localInputHistory[frame] = localInput;
            remoteInputHistory[frame] = remoteInput;
        }
    }

    /// <param name="frame">Frame when the packet was sent out</param>
    /// <param name="history">Ordered retrospectively, starting from the latest input</param>
    void OnInputDataReceived(int frame, NetworkInput[] history)
    {
        // ignore outdated packets or time traveling packets
        if (lastRemoteFrame > frame || frame > CurrentFrame)
            return;

        lastRemoteFrame = frame;
        int rollbackStart = -1;

        int retroIndex = math.max(0, frame - history.Length + 1);
        int loops = frame - retroIndex + 1;
        for (int i = 0; i < loops; i++)
        {
            // prediction was wrong condition
            if (remoteInputHistory[retroIndex + i] != history[history.Length - 1 - i])
            {
                remoteInputHistory[retroIndex + i] = history[history.Length - 1 - i];

                if (rollbackStart == -1)
                    rollbackStart = retroIndex + i;
            }
                
        }

        if (rollbackStart >= 0)
        {
            // future predictions up to current frame
            for (int i = frame; i < CurrentFrame; i++)
                remoteInputHistory[i + 1] = remoteInputHistory[i];

            Rollback(rollbackStart);
        }   
    }

    void OnStateSyncCheckReceived(int frame, int stateAHash, int stateBHash)
    {
        if (frame > lastRemoteFrame)
        {
            Debug.LogError("There is a large difference in the last remote frame, connection error");
            return;
        }
        int localHash = IsHost? stateAHash : stateBHash;
        int remoteHash = IsHost? stateBHash : stateAHash;

        if (localPlayerHistory[frame].GetHash() != localHash || remotePlayerHistory[frame].GetHash() != remoteHash)
        {
            for (int i= 0; i < 5; i++)
            {
                var x = !IsHost ? localPlayerHistory[frame - i] : remotePlayerHistory[frame - i];
                print(JsonUtility.ToJson(x));
            }
                
            Debug.LogError("There is a difference in game state, match is invalid!");
            return;
        }
    }
    void Rollback(int fromFrame)
    {
        print("prediction wrong - Rollback!");
        int frameDif = CurrentFrame - fromFrame + 1;

        int start = fromFrame > 0 ? -1 : 0;

        for (int i = start; i < frameDif; i++)
            Tick(fromFrame + i, localPlayerHistory[fromFrame + i], remotePlayerHistory[fromFrame + i], localInputHistory[fromFrame + i], remoteInputHistory[fromFrame + i]);
        
        // rollback animations
        foreach (var p in players)
            p.UpdateAnimation(p.LocalState);
    }
    #endregion
}
[System.Flags]
public enum NetworkInput : byte
{
    None = 0,
    Right = 1 << 0,
    Left = 1 << 1,
    Up = 1 << 2,
    Down = 1 << 3,
    Attack = 1 << 4
}
