using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 일차 진행
/// - 일차는 3분이 지나면 넘어갑니다. (대화·컷신·기절 연출 중에는 시간이 멈춤)
/// - 침실 침대를 클릭하면 "다음 일차로 넘어갈까?"를 묻고, [넘어간다]를 고르면 바로 다음 일차로 넘어갑니다.
/// - 일차가 넘어가면 GManager.NextDay()가 호출되어 DAY 표시가 오르고,
///   ItemPositionManager가 제자리 검사(틀리면 하트 감소) 및 초기화를 합니다.
/// - 황금 열쇠를 얻은 뒤 처음 맞는 일차 끝에 안내 대사를 한 번만 띄웁니다.
///   · 넘어가기 1분 전: "지금까지 얻은 모든 물건들을 제자리에 돌려놔야 할 것 같다." → "내가 나왔다는 증거가 남아있으면 안 된다."
///   · 넘어가기 30초 전: "내 방 열쇠 또한 제자리로 돌려놔야 할 것 같다."
///   · 그 전에 침대를 클릭하면 남은 대사만 바로 보여 주고 일차는 넘기지 않습니다. (처음 한 번)
///     하트·아이템도 그대로이며, 그다음 침대 클릭부터는 평소처럼 넘어갈지 묻습니다.
/// 실행할 때 SliderManager가 있는 씬에 자동으로 붙습니다.
/// </summary>
[DefaultExecutionOrder(215)]
public sealed class DayCycleController : MonoBehaviour
{
    public static DayCycleController Instance { get; private set; }

    private const float DayLengthSeconds = 180f;      // 3분
    private const float FirstWarningRemaining = 60f;  // 넘어가기 1분 전
    private const float SecondWarningRemaining = 30f; // 넘어가기 30초 전

    private const string GoldenKeyItemId = "golden_key";
    private const string BedroomViewName = "침실 기믹 화면";
    private const float ReferenceWidth = 1920f;
    private const float ReferenceHeight = 1080f;

    // 침대 클릭 영역 (침실 그림 1920x1080 기준, 위쪽이 y=0)
    // 베개·사진 조사 영역과 서랍장 조사 영역을 피해 이불 아랫부분만 잡습니다.
    private static readonly RectInt BedRect = new RectInt(580, 620, 680, 180);

    private static readonly string[] FirstWarningLines =
    {
        "지금까지 얻은 모든 물건들을 제자리에 돌려놔야 할 것 같다.",
        "내가 나왔다는 증거가 남아있으면 안 된다.",
    };

    private const string AskNextDayText = "다음 일차로 넘어갈까?";
    private static readonly string[] AskNextDayChoices = { "넘어간다", "아직 아니다" };

    private static readonly string[] SecondWarningLines =
    {
        "내 방 열쇠 또한 제자리로 돌려놔야 할 것 같다.",
    };

    private float elapsed;
    private bool keyObtained;
    private bool firstWarningShown;
    private bool secondWarningShown;
    private GameObject bedButton;
    private float nextRefreshTime;

    public float RemainingSeconds => Mathf.Max(0f, DayLengthSeconds - elapsed);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHandler()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (FindFirstObjectByType<DayCycleController>() != null)
            return;

        SliderManager slider = FindFirstObjectByType<SliderManager>();
        if (slider != null)
            slider.gameObject.AddComponent<DayCycleController>();
    }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextRefreshTime)
        {
            nextRefreshTime = Time.unscaledTime + 0.5f;
            if (bedButton == null)
                CreateBedButton();
            if (!keyObtained)
                keyObtained = HasGoldenKey();
        }

        if (IsTimePaused())
            return;

        elapsed += Time.deltaTime;

        if (keyObtained && !firstWarningShown && RemainingSeconds <= FirstWarningRemaining)
        {
            firstWarningShown = true;
            Think(FirstWarningLines);
            return;
        }

        if (keyObtained && !secondWarningShown && RemainingSeconds <= SecondWarningRemaining)
        {
            secondWarningShown = true;
            Think(SecondWarningLines);
            return;
        }

        if (elapsed >= DayLengthSeconds)
            AdvanceDay();
    }

    // ═══════════════════════════════════════════════════
    //  일차 넘기기
    // ═══════════════════════════════════════════════════

    public void AdvanceDay()
    {
        elapsed = 0f;

        // 열쇠 안내는 열쇠를 얻은 뒤 처음 맞는 일차 끝에만 나옵니다.
        if (keyObtained)
        {
            firstWarningShown = true;
            secondWarningShown = true;
        }

        GManager dayManager = GManager.Instance != null
            ? GManager.Instance
            : FindFirstObjectByType<GManager>(FindObjectsInactive.Include);
        if (dayManager == null)
        {
            Debug.LogWarning("[DayCycle] GManager를 찾을 수 없어 일차를 넘기지 못했습니다.", this);
            return;
        }

        dayManager.NextDay();
        Debug.Log($"[DayCycle] 다음 일차: DAY {dayManager.Day}", this);
    }

    private void OnBedClicked()
    {
        if (IsBusy())
            return;

        // 열쇠를 얻은 뒤 아직 안내를 다 못 봤으면, 남은 안내를 바로 보여 주고 이번 한 번만 넘김을 막습니다.
        if (keyObtained && (!firstWarningShown || !secondWarningShown))
        {
            List<string> lines = new List<string>();
            if (!firstWarningShown)
                lines.AddRange(FirstWarningLines);
            if (!secondWarningShown)
                lines.AddRange(SecondWarningLines);

            firstWarningShown = true;
            secondWarningShown = true;
            Think(lines.ToArray());
            return;
        }

        // 넘어가기 전에 한 번 묻습니다.
        DialogueManager dialogue = DialogueManager.Instance;
        if (dialogue == null)
        {
            AdvanceDay();
            return;
        }

        dialogue.StartChoiceDialogue(
            Stage1Dialogues.Thought(AskNextDayText),
            AskNextDayChoices,
            chosen =>
            {
                if (chosen == 0)
                    AdvanceDay();
            });
    }

    // ═══════════════════════════════════════════════════
    //  침대 클릭 영역
    // ═══════════════════════════════════════════════════

    private void CreateBedButton()
    {
        Transform bedroom = FindRectByName(BedroomViewName);
        if (bedroom == null)
            return;

        bedButton = new GameObject(
            "침대 (다음 일차로)",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        bedButton.transform.SetParent(bedroom, false);
        // 침실의 다른 조사 영역보다 뒤에 두어, 겹치는 곳은 기존 조사가 먼저 반응하게 합니다.
        bedButton.transform.SetAsFirstSibling();

        RectTransform rect = bedButton.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(BedRect.xMin / ReferenceWidth, 1f - BedRect.yMax / ReferenceHeight);
        rect.anchorMax = new Vector2(BedRect.xMax / ReferenceWidth, 1f - BedRect.yMin / ReferenceHeight);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = bedButton.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0f); // 보이지 않는 클릭 영역

        Button button = bedButton.GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(OnBedClicked);
    }

    // ═══════════════════════════════════════════════════
    //  도우미
    // ═══════════════════════════════════════════════════

    // 대화·컷신·기절/게임 오버 연출 중에는 시간이 흐르지 않습니다.
    private static bool IsTimePaused()
    {
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueOpen)
            return true;

        if (Stage1StoryController.Instance != null && Stage1StoryController.Instance.IsPlaying)
            return true;

        return PlayerFaintController.Instance != null && PlayerFaintController.Instance.IsBusy;
    }

    private static bool IsBusy()
    {
        if (IsTimePaused())
            return true;

        if (InspectionManager.Instance != null && InspectionManager.Instance.IsOpen)
            return true;

        if (RestroomGimmick.Instance != null && RestroomGimmick.Instance.IsKeypadOpen)
            return true;

        return FlashlightGimmick.Instance != null && FlashlightGimmick.Instance.IsViewerOpen;
    }

    private static bool HasGoldenKey()
    {
        QuickSlotInventory inventory = QuickSlotInventory.Instance != null
            ? QuickSlotInventory.Instance
            : FindFirstObjectByType<QuickSlotInventory>();
        if (inventory != null)
        {
            foreach (QuickSlotEntry entry in inventory.Entries)
            {
                if (entry?.ItemData != null && entry.ItemData.ItemId == GoldenKeyItemId)
                    return true;
            }
        }

        PasswordDrawer drawer = FindFirstObjectByType<PasswordDrawer>(FindObjectsInactive.Include);
        return drawer != null && drawer.IsKeyCollected;
    }

    private static void Think(string[] lines)
    {
        DialogueManager.Instance?.StartDialogue(Stage1Dialogues.Thought(lines));
    }

    private static Transform FindRectByName(string objectName)
    {
        foreach (RectTransform rect in FindObjectsByType<RectTransform>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (rect.name == objectName)
                return rect;
        }

        return null;
    }
}
