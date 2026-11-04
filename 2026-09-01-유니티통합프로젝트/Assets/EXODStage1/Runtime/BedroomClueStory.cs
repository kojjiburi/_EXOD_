using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 침실 서랍 비밀번호(724) 단서를 의미 있게 (2026-11-02 사용자 결정)
/// - 달력(7): 7일에 동그라미 = 그녀가 정한 "우리 기념일"
/// - 사진(2): 베개 밑 여친 감성 폴라로이드. 뒤쪽 꼬마전구 줄이 숫자 2 모양으로 숨어 있음 (Resources/EXODStage1/Clue_Photo)
/// - 메모(4): 그녀 글씨 "1. 우리 기념일 날짜 2. 우리 사진 속에 숨겨 둔 숫자 3. 내 마음 '사'랑해의 '사'" (Clue_Memo)
/// 단서 오브젝트는 SampleScene에 있어서(씬은 팀원이 작업 중이라 건드리지 않음) 실행할 때 그림·문구만 바꿔 끼웁니다.
/// 대사: Stage1Dialogues.BedroomCalendar* / BedroomPhoto* / BedroomMemo*
/// </summary>
public static class BedroomClueStory
{
    private const string CalendarPath = "EXODStage1/Clue_Calendar";   // 7에 동그라미 + 빨간 글씨 "우리 기념일 ♡" (2026-11-02)
    private const string PhotoPath = "EXODStage1/Clue_Photo";
    private const string MemoPath = "EXODStage1/Clue_Memo";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHandler()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single)
            return;

        foreach (InspectableItem item in Object.FindObjectsByType<InspectableItem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            switch (item.ItemId)
            {
                case "bedroom_calendar":
                    Apply(item, LoadSprite(CalendarPath), Stage1Dialogues.BedroomCalendarOpening, Stage1Dialogues.BedroomCalendarFirst, Stage1Dialogues.BedroomCalendarRepeat);
                    break;
                case "bedroom_photo":
                    Apply(item, LoadSprite(PhotoPath), Stage1Dialogues.BedroomPhotoOpening, Stage1Dialogues.BedroomPhotoFirst, Stage1Dialogues.BedroomPhotoRepeat);
                    break;
                case "bedroom_memo":
                    Apply(item, LoadSprite(MemoPath), Stage1Dialogues.BedroomMemoOpening, Stage1Dialogues.BedroomMemoFirst, Stage1Dialogues.BedroomMemoRepeat);
                    break;
            }
        }
    }

    private static void Apply(InspectableItem item, Sprite detail, string[] opening, string[] first, string[] repeat)
    {
        if (detail != null)
            Set(item, "detailSprite", detail);
        Set(item, "openingTexts", opening);
        Set(item, "firstExamineTexts", first);
        Set(item, "repeatExamineTexts", repeat);
    }

    private static void Set(InspectableItem item, string field, object value)
    {
        FieldInfo info = typeof(InspectableItem).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
        if (info != null)
            info.SetValue(item, value);
        else
            Debug.LogWarning("[BedroomClueStory] InspectableItem에 " + field + " 칸이 없습니다.");
    }

    private static Sprite LoadSprite(string path)
    {
        Texture2D texture = Resources.Load<Texture2D>(path);
        if (texture == null)
        {
            Debug.LogWarning("[BedroomClueStory] 그림을 찾을 수 없습니다: Resources/" + path);
            return null;
        }

        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
    }
}
