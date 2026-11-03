using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// [이지 모드] 행동 요령에 노란 글씨로 덧붙는 힌트 (사용자 요청 2026-11-03). 하드 모드에서는 아무것도 안 붙음.
/// - 산책 중: "뭔가 얻을 수 있는 것을 찾아보자."
/// - 산책을 한 번 다녀온 뒤 집: "화장실에 있는 단서들을 조사해 볼까?"
///     → 화장실 단서(수건·손전등)를 처음 얻은 날의 다다음 날부터 안 보임
/// - 종이 A·수건·손전등을 동시에 가지고 있으면: "물건끼리 상호작용시켜 보자(클릭). 무슨 일이 생길지도?"
///     → 손전등으로 종이 A를 비추면 그 뒤로 안 보임
/// - 부스 열쇠가 있고 산책을 한 번 다녀왔으면: "바깥 전화 부스를 열어 보자." → 부스가 열리면 안 보임
/// - 부스가 열려 있으면: "바깥에서 전화 부스를 수리할 만한 걸 찾아보자." → 다이얼 버튼 [1]을 찾으면 안 보임
/// 실행할 때 SliderManager가 있는 씬에 자동으로 붙습니다. 표시는 Stage1HelpBar가 함.
/// </summary>
public sealed class EasyHelpTips : MonoBehaviour
{
    private const string PaperAId = "paper_a";
    private const string BoothKeyId = "stage2_booth_key";
    private const string DialButton1Id = "stage2_dial_button_1";

    private static EasyHelpTips instance;

    private bool walkedOnce;
    private bool wasInStage2;
    private int restroomClueDay = -1;
    private bool paperALit;
    private bool dialButton1Found;

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
        SliderManager slider = FindFirstObjectByType<SliderManager>();
        if (slider != null && slider.GetComponent<EasyHelpTips>() == null)
            slider.gameObject.AddComponent<EasyHelpTips>();
    }

    private void Awake()
    {
        instance = this;
        FlashlightGimmick.PaperLit += OnPaperLit;
    }

    private void OnDestroy()
    {
        FlashlightGimmick.PaperLit -= OnPaperLit;
        if (instance == this)
            instance = null;
    }

    private void OnPaperLit(string paperId)
    {
        if (paperId == PaperAId)
            paperALit = true;
    }

    private void Update()
    {
        bool inStage2 = Stage2HouseLink.IsInStage2;
        if (wasInStage2 && !inStage2)
            walkedOnce = true;
        wasInStage2 = inStage2;

        if (restroomClueDay < 0 && (Has(RestroomGimmick.TowelItemId) || Has(FlashlightGimmick.FlashlightItemId)))
            restroomClueDay = CurrentDay;

        if (!dialButton1Found && Has(DialButton1Id))
            dialButton1Found = true;
    }

    private static int CurrentDay => GManager.Instance != null ? GManager.Instance.Day : 1;

    private static bool Has(string itemId)
    {
        QuickSlotInventory inventory = QuickSlotInventory.Instance;
        if (inventory == null)
            return false;
        foreach (QuickSlotEntry entry in inventory.Entries)
        {
            if (entry != null && entry.ItemData != null && entry.ItemData.ItemId == itemId)
                return true;
        }
        return false;
    }

    /// <summary>집에 있을 때 노란 힌트 (이지 모드만)</summary>
    public static List<string> HouseTips()
    {
        List<string> tips = new List<string>();
        if (!GameDifficulty.IsEasy || instance == null)
            return tips;

        if (instance.walkedOnce && (instance.restroomClueDay < 0 || CurrentDay < instance.restroomClueDay + 2))
            tips.Add("화장실에 있는 단서들을 조사해 볼까?");

        if (!instance.paperALit && Has(PaperAId) && Has(RestroomGimmick.TowelItemId) && Has(FlashlightGimmick.FlashlightItemId))
            tips.Add("물건끼리 상호작용시켜 보자(클릭). 무슨 일이 생길지도?");

        AddBoothTips(tips);
        return tips;
    }

    /// <summary>산책 중 노란 힌트 (이지 모드만)</summary>
    public static List<string> WalkTips()
    {
        List<string> tips = new List<string>();
        if (!GameDifficulty.IsEasy)
            return tips;

        tips.Add("뭔가 얻을 수 있는 것을 찾아보자.");
        AddBoothTips(tips);
        return tips;
    }

    private static void AddBoothTips(List<string> tips)
    {
        if (instance == null)
            return;

        if (!EXOD.Walk.PhoneBoothProgress.BoothUnlocked)
        {
            if (instance.walkedOnce && Has(BoothKeyId))
                tips.Add("바깥 전화 부스를 열어 보자.");
            return;
        }

        bool button1Found = EXOD.Walk.PhoneBoothProgress.Button1Installed || instance.dialButton1Found;
        if (!button1Found)
            tips.Add("바깥에서 전화 부스를 수리할 만한 걸 찾아보자.");
    }
}
