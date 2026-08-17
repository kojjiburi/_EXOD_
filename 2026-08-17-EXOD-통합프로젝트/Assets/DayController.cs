using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 각 Day 씬에 붙이는 컨트롤러
/// Day1, Day2, Day3, Day4 씬에 각각 붙이기
/// </summary>
public class DayController : MonoBehaviour
{
    [Header("=== 일차 정보 ===")]
    public int dayNumber = 1;           // 이 씬이 몇 일차인지

    [Header("=== 대화 데이터 ===")]
    public DialogueData openingDialogue;  // 씬 시작 대화
    public DialogueData endingDialogue;   // 씬 종료 대화

    [Header("=== UI ===")]
    public GameObject dayTitleUI;         // "DAY-1" 텍스트 UI (임시)

    [Header("=== 미니게임 오브젝트 ===")]
    public GameObject miniGameObject;     // 미니게임 오브젝트 (임시 박스)

    private bool _dayCleared = false;

    void Start()
    {
        // 일차 타이틀 표시
        ShowDayTitle();

        // 시작 대화 실행
        if (openingDialogue != null)
        {
            DialogueManager.Instance.StartDialogue(openingDialogue);
            DialogueManager.Instance.onDialogueEnd.AddListener(OnOpeningEnd);
        }
    }

    private void ShowDayTitle()
    {
        if (dayTitleUI != null)
            dayTitleUI.SetActive(true);

        // 2초 후 숨기기
        Invoke(nameof(HideDayTitle), 2f);
    }

    private void HideDayTitle()
    {
        if (dayTitleUI != null)
            dayTitleUI.SetActive(false);
    }

    // 오프닝 대화 끝나면 호출
    private void OnOpeningEnd()
    {
        DialogueManager.Instance.onDialogueEnd.RemoveListener(OnOpeningEnd);
        // 미니게임 활성화
        if (miniGameObject != null)
            miniGameObject.SetActive(true);
    }

    /// <summary>미니게임 클리어 시 호출</summary>
    public void OnMiniGameClear()
    {
        if (_dayCleared) return;
        _dayCleared = true;

        // 종료 대화 후 다음 씬으로
        if (endingDialogue != null)
        {
            DialogueManager.Instance.StartDialogue(endingDialogue);
            DialogueManager.Instance.onDialogueEnd.AddListener(OnEndingEnd);
        }
        else
        {
            GoNextDay();
        }
    }

    private void OnEndingEnd()
    {
        DialogueManager.Instance.onDialogueEnd.RemoveListener(OnEndingEnd);
        GoNextDay();
    }

    private void GoNextDay()
    {
        if (StageManager.Instance != null)
            StageManager.Instance.NextDay();
    }
}
