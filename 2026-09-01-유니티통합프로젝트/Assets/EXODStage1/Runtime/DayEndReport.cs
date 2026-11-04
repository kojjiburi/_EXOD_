using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 일차가 넘어갈 때 컷신 (2026-11-02)
/// 일차를 넘기기 직전(아이템 점검·초기화 전)에 제자리에 없는 물건을 적어 두었다가,
/// 일차 넘김 검은 화면 위에서 여자친구가 말해 주고, 그 뒤에 'DAY-n'이 나옵니다 (DayController가 TryPlayNow를 부름).
/// 검은 화면 연출이 없거나 산책 중이었으면 아래 Update가 나중에 말해 줍니다.
///  - 안 돌려놓은 물건이 있으면: "이거, 왜 제자리에 없어? 「황금 열쇠, 손전등」" (하트 감소는 ItemPositionManager가 함)
///  - 다 제자리면: "좋은 아침, 자기야~ 오늘도 얌전히 있었네? ♡"
/// 대사: Stage1Dialogues.DayReportMissing / DayReportClean
/// 실행할 때 SliderManager가 있는 씬에 자동으로 붙습니다.
/// </summary>
public sealed class DayEndReport : MonoBehaviour
{
    private static bool healPending;
    private static string pendingReport;   // null: 없음, "": 다 제자리, 그 외: 물건 이름들

    /// <summary>곰돌이 보답 컷신이 한 번 나왔는지 (그때 퀵슬롯이 꽉 차서 못 받았으면 거실 곰인형을 눌러 받음)</summary>
    public static bool BearGiftDelivered { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        pendingReport = null;
        BearGiftDelivered = false;
        healPending = false;
    }

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

        pendingReport = null;
        BearGiftDelivered = false;
        SliderManager slider = FindFirstObjectByType<SliderManager>();
        if (slider != null && slider.GetComponent<DayEndReport>() == null)
            slider.gameObject.AddComponent<DayEndReport>();
    }

    /// <summary>일차를 넘기기 직전에 DayCycleController가 부릅니다.</summary>
    public static void Prepare()
    {
        // 10의 배수 일차가 시작되면 하트 +1 (사용자 요청 2026-11-02). 제자리 검사(하트 감소) 뒤, 아침 대사 때 채움
        int nextDay = (GManager.Instance != null ? GManager.Instance.Day : 0) + 1;
        healPending = nextDay % 10 == 0;

        ItemPositionManager positions = ItemPositionManager.Instance;
        if (positions == null || ItemPositionManager.SkipDayEndCheck)
        {
            pendingReport = null;
            return;
        }

        List<string> names = new List<string>();
        foreach (ItemPositionRecord record in positions.GetMisplacedRecords())
        {
            string name = record.OriginalItem != null ? record.OriginalItem.DisplayName : record.OriginalItemId;
            if (!string.IsNullOrEmpty(name) && !names.Contains(name))
                names.Add(name);
        }

        QuickSlotInventory inventory = QuickSlotInventory.Instance;
        foreach (string itemId in positions.GetMisplacedInventoryItemIds())
        {
            QuickSlotEntry entry = inventory != null
                ? inventory.Entries.FirstOrDefault(e => e != null && e.ItemData != null && e.ItemData.ItemId == itemId)
                : null;
            string name = entry != null ? entry.ItemData.DisplayName : itemId;
            if (!string.IsNullOrEmpty(name) && !names.Contains(name))
                names.Add(name);
        }

        pendingReport = string.Join(", ", names);
    }

    /// <summary>제자리에 없는 물건이 있어서, 지금 TryPlayNow를 부르면 그 대사가 나오는지 (DayController가 컷신 이미지를 먼저 띄울 때 씀)</summary>
    public static bool HasMissingReport =>
        !string.IsNullOrEmpty(pendingReport)
        && !Stage2HouseLink.IsInStage2
        && !(PlayerFaintController.Instance != null && PlayerFaintController.Instance.IsBusy);

    /// <summary>
    /// 일차 넘김 검은 화면 위에서 바로 말해 줍니다 (DayController가 부름, kojjiburi 2026-11-02).
    /// 말할 게 없거나 게임 오버·산책 중이면 false (산책 중이면 돌아온 뒤 Update가 말함).
    /// </summary>
    public static bool TryPlayNow(System.Action onFinished)
    {
        if ((pendingReport == null && !BedSleepStreak.HasPending) || Stage2HouseLink.IsInStage2)
            return false;

        string report = pendingReport;
        pendingReport = null;
        if (PlayerFaintController.Instance != null && PlayerFaintController.Instance.IsBusy)
            return false;

        List<DialogueLine> lines = report == null
            ? new List<DialogueLine>()
            : report.Length > 0
                ? Stage1Dialogues.DayReportMissing(report)
                : Stage1Dialogues.DayReportClean();

        // 침대에서 연속으로 잤으면 "오늘은 무사히 넘긴 것 같다." 대신 연속 잠 대사 (4번째면 순응 엔딩, BedSleepStreak)
        bool submission = BedSleepStreak.ApplyTo(lines, report != null && report.Length == 0);
        if (submission)
        {
            System.Action afterReport = onFinished;
            onFinished = () =>
            {
                if (PlayerFaintController.Instance != null)
                    PlayerFaintController.Instance.ShowEndingAboveAll(
                        Stage1Dialogues.PotionEndingLine,
                        Stage1Dialogues.PotionEndingTitle,
                        Stage1Dialogues.PotionEndingSubtitle,
                        EndingStyle.Submission);
                afterReport?.Invoke();   // 일차 넘김 화면은 엔딩 화면 뒤에서 마저 끝남
            };
        }
        else
            ApplyHeal(lines);

        // 전날 팔다리를 다 돌려줬으면 곰돌이 보답도 이어서 (아래 Update와 같은 규칙)
        BearLimbGimmickManager bear = FindFirstObjectByType<BearLimbGimmickManager>();
        if (!submission && bear != null && bear.IsRewardPending && !BearGiftDelivered)
        {
            lines.AddRange(Stage1Dialogues.BearMorningGift());
            BearGiftDelivered = true;
            System.Action afterReport = onFinished;
            onFinished = () =>
            {
                bear.GiveRewardNote();
                afterReport?.Invoke();
            };
        }

        if (Stage1StoryController.Instance != null)
        {
            Stage1StoryController.Instance.PlayCutscene(lines, onFinished);
            return true;
        }

        DialogueManager dm = DialogueManager.Instance;
        if (dm == null)
            return false;

        UnityEngine.Events.UnityAction once = null;
        once = () =>
        {
            dm.onDialogueEnd.RemoveListener(once);
            onFinished?.Invoke();
        };
        dm.onDialogueEnd.AddListener(once);
        dm.StartDialogue(lines);
        return true;
    }

    // 10의 배수 일차: 하트 하나 채우고 한 줄 덧붙임
    private static void ApplyHeal(List<DialogueLine> lines)
    {
        if (!healPending)
            return;
        healPending = false;

        HeartManager hearts = FindFirstObjectByType<HeartManager>(FindObjectsInactive.Include);
        if (hearts == null)
            return;
        bool wasFull = hearts.HP >= hearts.MaxHP;
        hearts.HPP();
        lines.AddRange(wasFull ? Stage1Dialogues.DayHealFull() : Stage1Dialogues.DayHeal());
        EXODSound.Play("item_pickup", 0.7f);
    }

    private void Update()
    {
        if (pendingReport == null)
            return;

        // 하트가 0이 되어 게임 오버면 말하지 않음
        if (PlayerFaintController.Instance != null && PlayerFaintController.Instance.IsBusy)
        {
            if (!DayController.IsTransitioning)
                pendingReport = null;
            return;
        }

        // 'DAY-n' 화면이 끝나고, 다른 대화·창이 없을 때
        if (DayController.IsTransitioning || Stage1Room.IsBlocked() || Stage2HouseLink.IsInStage2)
            return;
        if (Stage1StoryController.Instance != null && Stage1StoryController.Instance.IsPlaying)
            return;

        string report = pendingReport;
        pendingReport = null;
        List<DialogueLine> lines = report.Length > 0
            ? Stage1Dialogues.DayReportMissing(report)
            : Stage1Dialogues.DayReportClean();
        ApplyHeal(lines);

        // 전날 팔다리를 다 돌려줬으면, 그녀가 나간 뒤 곰돌이가 보답(냉장고 비밀번호 쪽지)을 줌
        BearLimbGimmickManager bear = FindFirstObjectByType<BearLimbGimmickManager>();
        if (bear != null && bear.IsRewardPending && !BearGiftDelivered)
        {
            lines.AddRange(Stage1Dialogues.BearMorningGift());
            BearGiftDelivered = true;
            Stage1Room.PlayCutscene(lines, () => bear.GiveRewardNote());
            return;
        }

        Stage1Room.PlayCutscene(lines);
    }
}
