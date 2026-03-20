using System;

[System.Serializable]
public struct PlayerState
{
    public int StateFrame;
    public long PosX;
    public long PosY;
    public int VelocityX;
    public int VelocityY;
    public ActionStates State;
    public bool WasHitByCurrentHitbox;
    public int HP;
    public const int MaxHP = 100;
    public PlayerState(bool isPlayerOne)
    {
        StateFrame = 0;
        PosX = isPlayerOne ? -3000 : 3000;
        PosY = 1000;
        VelocityX = 0;
        VelocityY = 0;
        State = 0;
        HP = MaxHP;
        WasHitByCurrentHitbox = false;
    }
    
    public void TakeDamage(int damage)
    {          
        HP -= damage;

        if (HP <= 0)
        {
            HP = 0;
            State = ActionStates.Dead;
        } 
    }
    public bool IsInAir()
    { 
        return State == ActionStates.InAir || 
            State == ActionStates.AirSpin;
    }
    public bool IsAttacking()
    {
        return State == ActionStates.Jabbing || 
            State == ActionStates.Poking || 
            State == ActionStates.AirSpin;
    }
    public bool CanAttack()
    {
        return State == ActionStates.InAir ||
            State == ActionStates.Crouching ||
            State == ActionStates.Grounded;
    }    

    public int GetHash()
    {
        int hash = 1166136261;

        hash = (hash ^ StateFrame) * 16777619;
        hash = (hash ^ (int)PosX) * 16777619;
        hash = (hash ^ (int)PosY) * 16777619;
        hash = (hash ^ VelocityX) * 16777619;
        hash = (hash ^ VelocityY) * 16777619;
        hash = (hash ^ HP) * 16777619;
        hash = (hash ^ (int)State) * 16777619;

        return hash;
    }
}
public enum ActionStates : short
{
    Grounded,
    Crouching,
    InAir,
    Jabbing,
    Poking,
    AirSpin,
    Hitstun,
    Dead,
}