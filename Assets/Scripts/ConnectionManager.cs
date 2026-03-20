using System;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Networking.Transport;
using UnityEngine;
using UnityEngine.UI;

public class ConnectionManager : MonoBehaviour
{
    public static ConnectionManager Instance;

    [SerializeField]
    Button[] buttons = new Button[2];

    public NetworkDriver Driver;
    public NetworkConnection Connection;

    public InputDataReceiver OnInputDataReceived;
    public StateSyncReceiver OnStateSyncCheckReceived;
    public Action OnConnectionFound;

    public const int NetworkHistorySize = 10;
    public float SimulatedPing = 0.05f;

    public enum Roles
    {
        None,
        Host,
        Client
    }
    public Roles Role = Roles.None;

    enum MessageType : byte
    {
        InputData = 0,
        StateSyncCheck = 1,
    }



    private void Awake()
    {
        Instance = this;
        Application.runInBackground = true;
    }
    void OnDestroy()
    {
        CloseDriver();
        Instance = null;
    }

    private void Update()
    {
        if (Driver.IsCreated)
            FlushDriver();
    }
    void FlushDriver()
    {
        if (!Driver.IsCreated)
            return;

        Driver.ScheduleUpdate().Complete();

        SearchForConnections();

        NetworkEvent.Type cmd;
        while ((cmd = Driver.PopEvent(out var connection, out var stream)) != NetworkEvent.Type.Empty)
        {
            if (cmd == NetworkEvent.Type.Data)
            {
                MessageType type = (MessageType)stream.ReadByte();

                switch (type)
                {
                    case MessageType.InputData:
                        ReadInputData(stream);
                        break;

                    case MessageType.StateSyncCheck:
                        ReadStateSyncCheck(stream);
                        break;
                }
            }
            else if (cmd == NetworkEvent.Type.Connect)
            {
                Debug.Log("You are now connected to the server.");

                Connect(connection);
            }
            else if (cmd == NetworkEvent.Type.Disconnect)
            {
                Debug.Log("Client got disconnected from server.");
                Connection = default;
            }
        }
    }

    public void StartHost()
    {
        Driver = NetworkDriver.Create();
        var endpoint = NetworkEndpoint.AnyIpv4;
        endpoint.Port = 9000;

        if (Driver.Bind(endpoint) != 0)
            Debug.Log($"Error binding to port {endpoint.Port}");
        else
            Driver.Listen();

        Role = Roles.Host;

        foreach (Button b in buttons)
            b.interactable = false;
    }
    public void StartClient()
    {
        Driver = NetworkDriver.Create();
        var endpoint = NetworkEndpoint.LoopbackIpv4.WithPort(9000);

        Connection = Driver.Connect(endpoint);

        Role = Roles.Client;

        foreach (Button b in buttons)
            b.interactable = false;
    }
    
    void CloseDriver()
    {
        if (Driver.IsCreated)
        {
            Driver.ScheduleUpdate().Complete();

            if (Connection.IsCreated)
                Connection.Close(Driver);

            Driver.Dispose();
        }
    }
    
    void SearchForConnections()
    {
        if (Role == Roles.Host && Connection.GetState(Driver) != NetworkConnection.State.Connected)
        {
            NetworkConnection c;
            while ((c = Driver.Accept()) != default)
            {
                Debug.Log("Accepted a new connection from client.");

                Connect(c);
            }
        }
    }
    void Connect(NetworkConnection connection)
    {
        Connection = connection;

        foreach (Button b in buttons)
            b.gameObject.SetActive(false);

        OnConnectionFound?.Invoke();
    }

    public void SetAddedPing(float ping)
    {
        SimulatedPing = ping;
    }

    /// <param name="frame">Frame when the packet was sent out</param>
    /// <param name="recentHistory">Ordered retrospectively, starting from the latest input</param>
    public async void SendInputData(int frame, NetworkInput[] recentHistory)
    {
        await Task.Delay((int)(SimulatedPing * 1000));

        Driver.BeginSend(NetworkPipeline.Null, Connection, out DataStreamWriter writter);

        if (writter.IsCreated)
        {
            writter.WriteByte((byte)MessageType.InputData);

            writter.WriteInt(frame);

            int historySize = Math.Min(NetworkHistorySize, recentHistory.Length);
            for (int i = 0; i < historySize; i++)
                writter.WriteUShort((ushort)recentHistory[i]);

            Driver.EndSend(writter);
        }
        else
            Driver.AbortSend(writter);
    }
    public async void SendStateSyncCheck(int frame, PlayerState stateA, PlayerState stateB)
    {
        await Task.Delay((int)(SimulatedPing * 1000));

        Driver.BeginSend(NetworkPipeline.Null, Connection, out DataStreamWriter writter);

        if (writter.IsCreated)
        {
            writter.WriteByte((byte)MessageType.StateSyncCheck);

            writter.WriteInt(frame);

            writter.WriteInt(stateA.GetHash());
            writter.WriteInt(stateB.GetHash());

            Driver.EndSend(writter);
        }
        else
            Driver.AbortSend(writter);
    }

    void ReadInputData(DataStreamReader stream)
    {
        int remoteFrame = stream.ReadInt();

        NetworkInput[] history = new NetworkInput[NetworkHistorySize];
        int actualSize = Math.Min(NetworkHistorySize, remoteFrame);
        for (int i = 0; i < actualSize; i++)
        {
            history[i] = (NetworkInput)stream.ReadUShort();
        }

        OnInputDataReceived?.Invoke(remoteFrame, history);
    }
    void ReadStateSyncCheck(DataStreamReader stream)
    {
        int frame = stream.ReadInt();

        int stateAHash = stream.ReadInt();
        int stateBHash = stream.ReadInt();

        OnStateSyncCheckReceived?.Invoke(frame, stateAHash, stateBHash);
    }

    public delegate void InputDataReceiver(int frame, NetworkInput[] inputHistory);
    public delegate void StateSyncReceiver(int frame, int stateAHash, int stateBHash);
}

