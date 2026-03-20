using TMPro;
using UnityEngine;

public class TimerUI : MonoBehaviour
{
    public TextMeshProUGUI text;
    void Update()
    {
        text.text = $"{(int)((GameManager.Instance.MaxFrame - GameManager.Instance.CurrentFrame) * GameManager.TickTime)}";
    }
}
