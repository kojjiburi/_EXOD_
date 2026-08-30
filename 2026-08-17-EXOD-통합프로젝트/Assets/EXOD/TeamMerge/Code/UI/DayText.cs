using TMPro;
using UnityEngine;

public class DayText : MonoBehaviour
{
    public TextMeshProUGUI DAY;
    void Start()
    {
        GManager.Instance.onDayChanged += UpdateDay;
        UpdateDay(GManager.Instance.Day); // 시작값 표시
    }
    void UpdateDay(int day)
    {
        DAY.text = "DAY " + day;
    }
}
