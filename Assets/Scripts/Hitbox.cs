using System;
using TMPro;
using Unity.Mathematics;
using UnityEngine;

[Serializable]
public abstract class Hitbox
{
    public long CenterX;
    public long CenterY;
    public int Damage;
    public virtual bool IsPointInside(long x, long y)
    {
        return false;
    }

    public static bool DoHiboxesOverlap(BoxHitbox a, Hitbox b)
    {
        switch (b)
        {
            case BoxHitbox box:
                return DoHiboxesOverlap(a, box);

            case CircularHitbox circle:
                return DoHiboxesOverlap(a, circle);

            default:
                return false;
        }
    }
    public static bool DoHiboxesOverlap(BoxHitbox a, BoxHitbox b)
    {
        long dx = math.abs(a.CenterX - b.CenterX);
        long dy = math.abs(a.CenterY - b.CenterY);

        return dx < a.ExtentsX + b.ExtentsX && dy < a.ExtentsY + b.ExtentsY;
    }
    public static bool DoHiboxesOverlap(BoxHitbox a, CircularHitbox b)
    {
        long closestX = math.clamp(b.CenterX, a.CenterX - a.ExtentsX, a.CenterX + a.ExtentsX);
        long closestY = math.clamp(b.CenterY, a.CenterY - a.ExtentsY, a.CenterY + a.ExtentsY);

        long dx = closestX - b.CenterX;
        long dy = closestY - b.CenterY;

        return dx * dx + dy * dy < b.Radius * b.Radius;
    }
    public virtual void Draw() {  }
}
[Serializable]
public class CircularHitbox : Hitbox
{
    public long Radius;

    public CircularHitbox(long centerX, long centerY, long radius)
    {
        CenterX = centerX;
        CenterY = centerY;
        Radius = radius;
    }
    public CircularHitbox(CircularHitbox copy)
    {
        CenterX = copy.CenterX;
        CenterY = copy.CenterY;
        Damage = copy.Damage;

        Radius = copy.Radius;
    }
    public override bool IsPointInside(long x, long y)
    {
        long dx = CenterX - x;
        long dy = CenterY - y;
        return dx * dx + dy * dy < Radius * Radius;
    }

    public override void Draw()
    {
        Gizmos.DrawSphere(new Vector3(CenterX, CenterY, 0) / PlayerCapsule.UnitConversion,
            Radius / (float)PlayerCapsule.UnitConversion);
    }
}
[Serializable]
public class BoxHitbox : Hitbox
{
    public long ExtentsX;
    public long ExtentsY;

    public BoxHitbox(long centerX, long centerY, long extentsX, long extentsY) 
    {
        CenterX = centerX;
        CenterY = centerY;
        ExtentsX = extentsX; 
        ExtentsY = extentsY;
    }
    public BoxHitbox(BoxHitbox copy)
    {
        CenterX = copy.CenterX;
        CenterY = copy.CenterY;
        Damage = copy.Damage;

        ExtentsX = copy.ExtentsX;
        ExtentsY = copy.ExtentsY;
    }

    public override bool IsPointInside(long x, long y)
    {
        return math.abs(CenterX - x) < ExtentsX && math.abs(CenterY - y) < ExtentsY;
    }

    public override void Draw()
    {
        Gizmos.DrawCube(new Vector3(CenterX, CenterY, 0) / PlayerCapsule.UnitConversion,
            new Vector3(ExtentsX * 2, ExtentsY * 2, 1) / PlayerCapsule.UnitConversion);
    }
}
