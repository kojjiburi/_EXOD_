using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 침대에서 연속으로 자면 순응 엔딩 (사용자 요청 2026-11-04, 운명의 갈림길 '침대' 연출을 실제 게임에서도)
/// 침대로 날을 넘길 때(DayCycleController.OnBedClicked → [넘어간다]) 아래면 연속 +1, 아니면 0:
///  - 오늘 침실 밖으로 한 번도 안 나감
///  - 침실 밖에 나갔지만, 오늘 게임 타이머로 45초가 안 지났음 (45초 안에 날을 두 번 넘김)
/// 침대가 아닌 방법(시간이 다 돼서 그녀가 옴 등)으로 날이 넘어가면 0.
/// 아침 대사(DayEndReport)의 "오늘은 무사히 넘긴 것 같다." 대신:
///  1: 자고 바로 또 자서 그런가? 몸이 약간 찌뿌둥하다 (침실에만 있었을 때) / 많이 잤더니 오히려 더 피곤하다. (45초 안)
///  2: 하암... 다시 누울까? 포근하네..
///  3: .. 여기서 계속 사는것도 좋을지도?
///  4: 여기서 살자. (이 줄이 뜰 때 cursed_horror) → 물약 실패 대사(PotionFail) → 순응 엔딩
/// 실행할 때 SliderManager가 있는 씬에 자동으로 붙습니다.
/// </summary>
public sealed class BedSleepStreak : MonoBehaviour
{
    public const float QuickSleepSeconds = 45f;
    public const int EndingCount = 4;

    public static int Count { get; private set; }

    private static bool leftBedroomToday;
    private static bool advancedByBed;   // 이번 날 넘김이 침대로 한 것인지
    private static int pendingCount;     // 아침 대사에 쓸 값 (0이면 없음)
    private static bool pendingQuick;

    public static bool HasPending => pendingCount > 0;

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

        ResetAll();
        SliderManager slider = FindFirstObjectByType<SliderManager>();
        if (slider != null && slider.GetComponent<BedSleepStreak>() == null)
            slider.gameObject.AddComponent<BedSleepStreak>();
    }

    public static void ResetAll()
    {
        Count = 0;
        leftBedroomToday = false;
        advancedByBed = false;
        pendingCount = 0;
        pendingQuick = false;
    }

    // 침실 밖에 나갔는지 (산책 포함)
    private void Update()
    {
        if (leftBedroomToday)
            return;
        if (Stage2HouseLink.IsInStage2)
        {
            leftBedroomToday = true;
            return;
        }
        SliderManager slider = SliderManager.Instance;
        if (slider != null && slider.isUnlocked && slider.CurrentRoom != "침실")
            leftBedroomToday = true;
    }

    /// <summary>침대에서 [넘어간다]를 골랐을 때 (DayCycleController). elapsed = 오늘 지난 게임 시간(초)</summary>
    public static void OnBedSleep(float elapsed)
    {
        advancedByBed = true;
        bool stayedIn = !leftBedroomToday;
        if (stayedIn || elapsed < QuickSleepSeconds)
        {
            Count++;
            pendingCount = Mathf.Min(Count, EndingCount);
            pendingQuick = !stayedIn;
        }
        else
        {
            Count = 0;
            pendingCount = 0;
        }
    }

    /// <summary>새 날이 시작될 때 (DayCycleController.AdvanceDayNow)</summary>
    public static void OnDayStarted()
    {
        if (!advancedByBed)
        {
            Count = 0;
            pendingCount = 0;
        }
        advancedByBed = false;
        leftBedroomToday = false;
    }

    /// <summary>
    /// 아침 대사에 연속 잠 대사를 넣습니다. clean이면 마지막 줄("오늘은 무사히 넘긴 것 같다.")을 바꾸고, 아니면 끝에 붙입니다.
    /// 순응 엔딩으로 가야 하면 true (물약 실패 대사까지 붙여 둠).
    /// </summary>
    public static bool ApplyTo(List<DialogueLine> lines, bool clean)
    {
        if (pendingCount <= 0)
            return false;

        int count = pendingCount;
        bool quick = pendingQuick;
        pendingCount = 0;

        string text;
        switch (count)
        {
            case 1: text = quick ? "많이 잤더니 오히려 더 피곤하다." : "자고 바로 또 자서 그런가? 몸이 약간 찌뿌둥하다"; break;
            case 2: text = "하암... 다시 누울까? 포근하네.."; break;
            case 3: text = ".. 여기서 계속 사는것도 좋을지도?"; break;
            default: text = "여기서 살자."; break;
        }

        DialogueLine line = Stage1Dialogues.Thought(text)[0];
        if (clean && lines.Count > 0)
            lines[lines.Count - 1] = line;
        else
            lines.Add(line);

        if (count < EndingCount)
            return false;

        PlaySoundWhenShown(line, "cursed_horror");
        lines.AddRange(Stage1Dialogues.BedSubmission());
        Count = 0;
        return true;
    }

    private static void PlaySoundWhenShown(DialogueLine target, string sound)
    {
        DialogueManager dm = DialogueManager.Instance;
        if (dm == null)
            return;

        System.Action<DialogueLine> hook = null;
        hook = l =>
        {
            if (l != target)
                return;
            dm.LineShown -= hook;
            EXODSound.Play(sound);
        };
        dm.LineShown += hook;
    }
}
