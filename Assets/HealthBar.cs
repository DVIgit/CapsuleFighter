using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    public PlayerCapsule player;
    public Image fill;

    void Update()
    {
        fill.fillAmount = (float) player.LocalState.HP / (float) PlayerState.MaxHP;
    }
}
