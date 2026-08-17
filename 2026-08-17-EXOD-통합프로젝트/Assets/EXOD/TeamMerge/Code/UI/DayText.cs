using TMPro;
using UnityEngine;

public class DayText : MonoBehaviour
{
    public TextMeshProUGUI DAY;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        GManager.Instance.onDayChanged += UpdateDay;
        UpdateDay(GManager.Instance.Day); // 시작값 표시
    }
    void UpdateDay(int day)
    {
        DAY.text = "DAY " + day;
    }
    // Update is called once per frame
    void Update()
    {
        
    }
}
