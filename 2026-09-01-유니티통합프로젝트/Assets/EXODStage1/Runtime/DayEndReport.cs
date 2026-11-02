using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 일차가 넘어갈 때 컷신 (2026-11-02)
/// 일차를 넘기기 직전(아이템 점검·초기화 전)에 제자리에 없는 물건을 적어 두었다가,
/// 'DAY-n' 화면이 끝나고 밝아지면 여자친구가 말해 줍니다.
///  - 안 돌려놓은 물건이 있으면: "이거, 왜 제자리에 없어? 「황금 열쇠, 손전등」" (하트 감소는 ItemPositionManager가 함)
///  - 다 제자리면: "좋은 아침, 자기야~ 오늘도 얌전히 있었네? ♡"
/// 대사: Stage1Dialogues.DayReportMissing / DayReportClean
/// 실행할 때 SliderManager가 있는 씬에 자동으로 붙습니다.
/// </summary>
public sealed class DayEndReport : MonoBehaviour
{
    private static string pendingReport;   // null: 없음, "": 다 제자리, 그 외: 물건 이름들

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => pendingReport = null;

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
        SliderManager slider = FindFirstObjectByType<SliderManager>();
        if (slider != null && slider.GetComponent<DayEndReport>() == null)
            slider.gameObject.AddComponent<DayEndReport>();
    }

    /// <summary>일차를 넘기기 직전에 DayCycleController가 부릅니다.</summary>
    public static void Prepare()
    {
        ItemPositionManager positions = ItemPositionManager.Instance;
        if (positions == null)
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
        Stage1Room.PlayCutscene(report.Length > 0
            ? Stage1Dialogues.DayReportMissing(report)
            : Stage1Dialogues.DayReportClean());
    }
}
