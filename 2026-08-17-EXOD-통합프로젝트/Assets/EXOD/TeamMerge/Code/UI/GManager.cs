using UnityEngine;

public class GManager : MonoBehaviour
{
    public static GManager Instance;
    public int Day = 1; //Day변수 r값
    public delegate void OnDayChanged(int newDay);
    public event OnDayChanged onDayChanged;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void Awake()
    {
        Instance = this;
    }


    
    public void NextDay()  //코드명 NextDay 이거 호출되면 Day가 +됨. 침대? 누르고 다음날 확인버튼 누르면 호출
    {
        Day++;
        onDayChanged?.Invoke(Day);
    }
}
