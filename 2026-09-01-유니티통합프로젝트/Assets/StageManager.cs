using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬 전환 매니저
/// 씬 이름: Day1, Day2, Day3, Day4
/// </summary>
public class StageManager : MonoBehaviour
{
    public static StageManager Instance { get; private set; }

    [Header("=== 현재 일차 ===")]
    public int currentDay = 1; // 현재 몇 일차인지

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject); // 씬 넘어가도 유지
    }

    /// <summary>다음 일차로 넘어가기</summary>
    public void NextDay()
    {
        currentDay++;

        if (currentDay > 4)
        {
            // 4일차 끝나면 엔딩 or 게임오버
            Debug.Log("게임 클리어!");
            // SceneManager.LoadScene("Ending"); // 나중에 연결
            return;
        }

        SceneManager.LoadScene("Day" + currentDay);
    }

    /// <summary>특정 일차로 이동</summary>
    public void GoToDay(int day)
    {
        currentDay = day;
        SceneManager.LoadScene("Day" + day);
    }

    /// <summary>1일차로 처음부터</summary>
    public void RestartGame()
    {
        currentDay = 1;
        SceneManager.LoadScene("Day1");
    }
}
