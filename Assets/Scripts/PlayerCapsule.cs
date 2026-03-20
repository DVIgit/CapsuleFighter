using System.Linq;
using Unity.Mathematics;
using UnityEngine;

public class PlayerCapsule : MonoBehaviour
{
    /// <summary>
    /// Local State is purely for the visual capsule position. Gets updated from game manager Update()
    /// </summary>
    public PlayerState LocalState;
    const float interpolationAcceleration = 20;

    // should be constant througout the game
    [SerializeField]
    bool IsPlayerOne = true;
    [SerializeField] 
    int Accelleration = 1000;
    [SerializeField] 
    int JumpStrength = 10;
    [SerializeField] 
    int maxVelocity = 10000;

    const int Gravity = -7000;

    [SerializeField]
    Animator animator;
    [SerializeField]
    AnimationClip[] clips;

    [SerializeField]
    BoxHitbox debugBoxHitbox;
    [SerializeField]
    CircularHitbox debugCircleHitbox;


    [SerializeField]
    BoxHitbox groundedHurtbox;
    [SerializeField]
    BoxHitbox crouchingHurtbox;

    [SerializeField]
    BoxHitbox jabHitbox;
    [SerializeField]
    BoxHitbox pokeHitbox;
    [SerializeField]
    BoxHitbox pokingHurtbox;
    [SerializeField]
    CircularHitbox spinHitbox;
    [SerializeField]
    BoxHitbox spinHurtbox;


    public const int UnitConversion = 1000;
    
    
    private void Awake()
    {
        LocalState = new PlayerState(IsPlayerOne);
    }
    void Update()
    {
        float visualX = LocalState.PosX / (float)UnitConversion;
        float visualY = LocalState.PosY / (float)UnitConversion;
        Vector3 targetPos = new Vector3(visualX, visualY, 0);

        // smoothing
        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * interpolationAcceleration);
    }

    // animation states must have the same name as the state enum and clips
    public void UpdateAnimation(PlayerState state)
    {
        AnimationClip clip = GetAnimationClip(state.State.ToString());
        if (clip == default)
            return;

        float rollbackSec = (float)GameManager.TickTime * state.StateFrame;

        if (clip.isLooping)
            rollbackSec = rollbackSec % clip.length;

        float normalizedOffset = math.clamp(rollbackSec / clip.length, 0, 1);

        animator.Play(state.State.ToString(), 0, normalizedOffset);
    }

    AnimationClip GetAnimationClip(string name)
    {
        return clips.FirstOrDefault(x => x.name == name);
    }
    
    public void Tick(ref PlayerState state, NetworkInput input, PlayerState opponentState)
    {
        ActionStates lastState = state.State;

        switch (state.State)
        {
            case ActionStates.Grounded:
                if (input.HasFlag(NetworkInput.Attack))
                    state.State = ActionStates.Jabbing;

                else if (input.HasFlag(NetworkInput.Down))
                    state.State = ActionStates.Crouching;

                else if (state.PosY > 0)
                    state.State = ActionStates.InAir;

                else if (input.HasFlag(NetworkInput.Up))
                {
                    state.State = ActionStates.InAir;
                    state.VelocityY = JumpStrength;
                }

                MoveX(ref state, input);
                break;

            case ActionStates.Crouching:
                if (input.HasFlag(NetworkInput.Attack))
                    state.State = ActionStates.Poking;

                else if (state.PosY > 0)
                    state.State = ActionStates.InAir;

                else if (!input.HasFlag(NetworkInput.Down))
                    state.State = ActionStates.Grounded;

                MoveX(ref state, input);
                break;

            case ActionStates.InAir:
                if (input.HasFlag(NetworkInput.Attack))
                    state.State = ActionStates.AirSpin;

                Fall(ref state);

                if (state.PosY <= 0)
                    state.State = ActionStates.Grounded;
                break;

            case ActionStates.Jabbing:
                if (state.StateFrame > 40)
                    state.State = ActionStates.Grounded;

                MoveX(ref state, NetworkInput.None);
                break;

            case ActionStates.Poking:
                if (state.StateFrame > 60)
                    state.State = ActionStates.Crouching;

                MoveX(ref state, NetworkInput.None);
                break;

            case ActionStates.AirSpin:
                if (state.StateFrame > 35)
                    state.State = ActionStates.InAir;

                Fall(ref state);

                if (state.PosY <= 0)
                    state.State = ActionStates.Grounded;
                break;

            case ActionStates.Hitstun:
                Fall(ref state);

                if (state.StateFrame > 30)
                {
                    if (state.PosY > 0)
                        state.State = ActionStates.InAir;
                    else
                        state.State = ActionStates.Grounded;
                }
                break;
        }

        if (CheckHit(ref state, GetAttackingHitbox(opponentState, IsPlayerOne)))
        {
            if (state.State != ActionStates.Dead)
            {
                state.State = ActionStates.Hitstun;
                state.VelocityX = 2500 * (IsPlayerOne ? -1 : 1);
                state.VelocityY = 1750;
            }
                
        }
        
        if (lastState == state.State)
            state.StateFrame++;
        else
            state.StateFrame = 0;


        // walls + player bounds
        long min = -5000;
        long max = 5000;

        if (IsPlayerOne)
            max = opponentState.PosX - groundedHurtbox.ExtentsX;
        else
            min = opponentState.PosX + groundedHurtbox.ExtentsX;

        state.PosX = math.clamp(state.PosX, min, max);


        if (!animator.GetCurrentAnimatorStateInfo(0).IsName(state.State.ToString()))
            UpdateAnimation(state);
    }
    void Fall(ref PlayerState state)
    {
        int delta = (int)(GameManager.TickTime * Gravity);

        if (state.PosY > 0 || state.VelocityY > 0)
        {
            state.VelocityY += delta;

            if (state.VelocityY < -maxVelocity)
                state.VelocityY = -maxVelocity;
        }
        else
        {
            state.VelocityY = 0;
            state.PosY = 0;
        }

        state.PosY = math.max(state.PosY + (int)(GameManager.TickTime * state.VelocityY), 0);
        state.PosX += (int)(GameManager.TickTime * state.VelocityX);
    }
    void MoveX(ref PlayerState state, NetworkInput input)
    {
        int delta = (int)(GameManager.TickTime * Accelleration);

        int maxV = maxVelocity;

        if (state.State == ActionStates.Crouching)
            maxV /= 2;

        if (input.HasFlag(NetworkInput.Right))
        {
            state.VelocityX += delta;
            if (state.VelocityX > maxV)
                state.VelocityX = maxV;
        }
        else if (input.HasFlag(NetworkInput.Left))
        {
            state.VelocityX -= delta;
            if (state.VelocityX < -maxV)
                state.VelocityX = -maxV;
        }
        else
        {
            if (math.abs(state.VelocityX) < delta)
                state.VelocityX = 0;
            else
                state.VelocityX -= math.sign(state.VelocityX) * delta;
        }

        state.PosX += (long)(GameManager.TickTime * state.VelocityX);
    }
    Hitbox GetAttackingHitbox(PlayerState state, bool mirror)
    {
        Hitbox hitbox = null;
        switch (state.State)
        {
            case ActionStates.Jabbing:
                if (state.StateFrame > 27 && state.StateFrame < 35)
                    hitbox = new BoxHitbox(jabHitbox);
                break;

            case ActionStates.Poking:
                if (state.StateFrame > 10 && state.StateFrame < 15)
                    hitbox = new BoxHitbox(pokeHitbox);
                break;

            case ActionStates.AirSpin:
                if (state.StateFrame > 10 && state.StateFrame < 30)
                    hitbox = new CircularHitbox(spinHitbox);
                break;
        }

        if (hitbox != null)
        {
            //mirror attacks for player 2
            if (hitbox != null && mirror)
                hitbox.CenterX *= -1;

            hitbox.CenterX += state.PosX;
            hitbox.CenterY += state.PosY;
        }

        return hitbox;
    }
    BoxHitbox GetHurtbox(PlayerState state)
    {
        BoxHitbox hurtbox;
        switch (state.State)
        {
            case ActionStates.Crouching:
                hurtbox = new BoxHitbox(crouchingHurtbox);
                break;

            case ActionStates.Poking:
                hurtbox = new BoxHitbox(pokingHurtbox);
                break;

            case ActionStates.AirSpin:
                hurtbox = new BoxHitbox(spinHurtbox);
                break;

            default:
                hurtbox = new BoxHitbox(groundedHurtbox);
                break;
        }

        hurtbox.CenterX += state.PosX;
        hurtbox.CenterY += state.PosY;

        return hurtbox;
    }
    bool CheckHit(ref PlayerState state, Hitbox hitbox)
    {
        if (hitbox == null)
        {
            state.WasHitByCurrentHitbox = false;
            return false;
        }

        if (Hitbox.DoHiboxesOverlap(GetHurtbox(state), hitbox))
        {
            if (state.WasHitByCurrentHitbox)
                return false;

            state.WasHitByCurrentHitbox = true;
            state.TakeDamage(hitbox.Damage);
            return true;
        }
        else
            state.WasHitByCurrentHitbox = false;

        return false;
    }

    private void OnDrawGizmos()
    {
        debugBoxHitbox.Draw();
        GetHurtbox(LocalState).Draw();
        GetAttackingHitbox(LocalState, !IsPlayerOne)?.Draw();        
    }

}
