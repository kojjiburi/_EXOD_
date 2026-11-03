using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 일차 진행
/// - 일차는 3분(1일차는 1분 30초). 침대로 넘기지 않고 0초가 되면 여자친구가 바로 옴 → 끝나면 다음 일차 (2026-11-03)
///   (침실 안이면 '아직도 안 자?' 대사, 밖이면 방 밖에서 들킨 컷신. 대화·컷신·기절 연출 중에는 시간이 멈춤)
/// - 1일차: 남은 20초부터 행동 요령에 노란 글씨 안내, 5초 남을 때까지 침대를 안 눌렀으면 혼잣말
/// - 오른쪽 위 장소 이름 아래에 이번 일차의 남은 시간(m:ss)을 표시합니다.
///   2스테이지(산책)에 있는 동안에는 그 바로 아래에 2스테이지 1분 타이머(DayClock)도 표시합니다.
/// - 2스테이지에 가 있어도 이 3분 타이머는 초기화되지 않고 계속 흐릅니다.
/// - 침실 침대를 클릭하면 "다음 일차로 넘어갈까?"를 묻고, [넘어간다]를 고르면 바로 다음 일차로 넘어갑니다.
/// - 일차가 넘어가면 GManager.NextDay()가 호출되어 DAY 표시가 오르고,
///   ItemPositionManager가 제자리 검사(틀리면 하트 감소) 및 초기화를 합니다.
/// - 황금 열쇠를 얻은 뒤 처음 맞는 일차 끝에 안내 대사를 한 번만 띄웁니다.
///   · 넘어가기 30초 전: "지금까지 얻은 모든 물건들을 제자리에 돌려놔야 할 것 같다." → "내가 나왔다는 증거가 남아있으면 안 된다."
///   · 넘어가기 15초 전: "내 방 열쇠 또한 제자리로 돌려놔야 할 것 같다."
///   · 그 전에 침대를 클릭하면 남은 대사만 바로 보여 주고 일차는 넘기지 않습니다. (처음 한 번)
///     하트·아이템도 그대로이며, 그다음 침대 클릭부터는 평소처럼 넘어갈지 묻습니다.
/// 실행할 때 SliderManager가 있는 씬에 자동으로 붙습니다.
/// </summary>
[DefaultExecutionOrder(215)]
public sealed class DayCycleController : MonoBehaviour
{
    public static DayCycleController Instance { get; private set; }

    private const float NormalDayLengthSeconds = 180f;   // 3분
    private const float FirstDayLengthSeconds = 90f;     // 1일차만 1분 30초 (사용자 요청 2026-11-02)
    private const float FirstWarningRemaining = 30f;     // 넘어가기 30초 전: 물건 제자리 (2026-11-02: 1분 → 30초)
    private const float SecondWarningRemaining = 15f;    // 넘어가기 15초 전: 황금 열쇠 (2026-11-02: 30초 → 15초)

    // 이번 일차의 길이 (1일차 1분 30초, 그 밖 3분)
    private static float DayLengthSeconds
    {
        get
        {
            GManager manager = GManager.Instance;
            return manager != null && manager.Day <= 1 ? FirstDayLengthSeconds : NormalDayLengthSeconds;
        }
    }

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
    private bool dayEndHandled;        // 0초가 돼서 여자친구 등장 → 다음 일차를 이미 시작함
    private bool bedClickedToday;      // 오늘 침대를 한 번이라도 눌렀는지
    private bool bedWarningShown;      // 1일차 5초 남았을 때 안내를 보였는지
    private const float FirstDayBedWarningRemaining = 5f;   // (사용자 요청 2026-11-03)
    private bool keyObtained;
    private bool firstWarningShown;
    private bool secondWarningShown;
    private GameObject bedButton;
    private TextMeshProUGUI timerText;
    private TextMeshProUGUI stage2TimerText;
    private int lastShownStage2Seconds = -1;
    private static readonly Color Stage2TimerColor = new(1f, 0.72f, 0.72f, 1f);
    private int lastShownSeconds = -1;
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
            if (timerText == null)
                CreateTimerText();
            if (!keyObtained)
                keyObtained = HasGoldenKey();
        }

        UpdateTimerText();

        if (IsTimePaused())
            return;

        elapsed += Time.deltaTime;

        if (keyObtained && !firstWarningShown && RemainingSeconds <= FirstWarningRemaining)
        {
            firstWarningShown = true;
            GimmickInterrupt.CloseAll();
            Think(FirstWarningLines);
            return;
        }

        if (keyObtained && !secondWarningShown && RemainingSeconds <= SecondWarningRemaining)
        {
            secondWarningShown = true;
            GimmickInterrupt.CloseAll();
            Think(SecondWarningLines);
            return;
        }

        // 1일차: 5초 남을 때까지 침대를 안 눌렀으면 안내 (사용자 요청 2026-11-03)
        bool firstDay = GManager.Instance == null || GManager.Instance.Day <= 1;
        if (firstDay && !bedClickedToday && !bedWarningShown && RemainingSeconds <= FirstDayBedWarningRemaining)
        {
            bedWarningShown = true;
            GimmickInterrupt.CloseAll();
            Think(new[] { Stage1Dialogues.FirstDayBedWarning });
            return;
        }

        // 침대로 넘기지 않고 0초가 되면: 여자친구가 바로 옴 (침실 안이면 '아직도 안 자?', 밖이면 들킴) → 끝나면 다음 일차
        if (elapsed >= DayLengthSeconds && !dayEndHandled && !DangerLevel.IsAppearing)
        {
            dayEndHandled = true;
            if (!DangerLevel.TryAppearAtDayEnd(AdvanceDay))
                AdvanceDay();
        }
    }

    // ═══════════════════════════════════════════════════
    //  일차 넘기기
    // ═══════════════════════════════════════════════════

    /// <summary>오늘 남은 시간에서 초를 뺍니다. (2스테이지에서 들켰을 때 등) 0이 되면 바로 다음 일차로 넘어갑니다.</summary>
    public void ConsumeTime(float seconds)
    {
        if (seconds <= 0f)
            return;

        elapsed = Mathf.Min(DayLengthSeconds, elapsed + seconds);
    }

    public void AdvanceDay()
    {
        // 화면이 천천히 검어졌다가 'DAY-n'을 보여주고 다시 투명해집니다 (DayController). 일차는 검은 화면일 때 넘어갑니다.
        if (DayController.IsTransitioning)
            return;
        // 순서: 검은 화면 → 일차 끝 대사(nicezzomaru의 DayEndReport, 검은 화면 위) → 'DAY-n' → 밝아짐
        if (DayController.TryPlayDayTransition(AdvanceDayNow))
            return;

        AdvanceDayNow();
    }

    private void AdvanceDayNow()
    {
        elapsed = 0f;
        dayEndHandled = false;
        bedClickedToday = false;
        bedWarningShown = false;

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

        // 점검·초기화 전에 제자리에 없는 물건을 적어 두고, 검은 화면 위에서 여자친구가 말해 줌 (DayEndReport 컷신)
        DayEndReport.Prepare();

        dayManager.NextDay();
        Debug.Log($"[DayCycle] 다음 일차: DAY {dayManager.Day}", this);
    }

    private void OnBedClicked()
    {
        if (IsBusy())
            return;

        bedClickedToday = true;

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
    //  남은 시간 표시 (장소 이름 아래)
    // ═══════════════════════════════════════════════════

    private void CreateTimerText()
    {
        TextMeshProUGUI roomText = SliderManager.Instance != null ? SliderManager.Instance.roomNameText : null;
        if (roomText == null)
            return;

        GameObject go = new GameObject("일차 남은 시간", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(roomText.transform, false);

        timerText = go.GetComponent<TextMeshProUGUI>();
        timerText.font = roomText.font;
        timerText.fontSize = roomText.fontSize * 0.7f;
        timerText.fontStyle = FontStyles.Normal;
        timerText.color = roomText.color;
        timerText.alignment = roomText.alignment;
        timerText.raycastTarget = false;
        timerText.textWrappingMode = TextWrappingModes.NoWrap;

        lastShownSeconds = -1;
        UpdateTimerText();
    }

    private void UpdateTimerText()
    {
        if (timerText == null)
            return;

        int seconds = Mathf.CeilToInt(RemainingSeconds);
        if (seconds != lastShownSeconds)
        {
            lastShownSeconds = seconds;
            timerText.text = $"{seconds / 60}:{seconds % 60:00}";
        }

        // 장소 이름 글자 바로 아래에 둡니다. (방 이름 길이가 바뀌어도 같은 줄 맞춤)
        TMP_Text roomText = timerText.transform.parent != null
            ? timerText.transform.parent.GetComponent<TMP_Text>()
            : null;
        if (roomText == null)
            return;

        RectTransform parentRect = roomText.rectTransform;
        RectTransform rect = timerText.rectTransform;
        rect.anchorMin = rect.anchorMax = parentRect.pivot;
        rect.pivot = new Vector2(parentRect.pivot.x, 1f);
        rect.sizeDelta = new Vector2(parentRect.rect.width, timerText.fontSize * 1.3f);

        float textBottom = string.IsNullOrEmpty(roomText.text) ? 0f : roomText.textBounds.min.y;
        rect.anchoredPosition = new Vector2(0f, textBottom - 2f);

        UpdateStage2TimerText(rect);
    }

    // 2스테이지 1분 타이머: 3분 타이머 바로 아래, 2스테이지에 있을 때만 보입니다.
    private void UpdateStage2TimerText(RectTransform stage1TimerRect)
    {
        EXOD.Walk.DayClock clock = EXOD.Walk.DayClock.Instance;
        bool show = clock != null && Stage2HouseLink.IsInStage2;

        if (stage2TimerText == null)
        {
            if (!show)
                return;

            GameObject go = new GameObject("2스테이지 남은 시간", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(timerText.transform, false);
            stage2TimerText = go.GetComponent<TextMeshProUGUI>();
            stage2TimerText.font = timerText.font;
            stage2TimerText.fontSize = timerText.fontSize;
            stage2TimerText.fontStyle = FontStyles.Normal;
            stage2TimerText.color = Stage2TimerColor;
            stage2TimerText.alignment = timerText.alignment;
            stage2TimerText.raycastTarget = false;
            stage2TimerText.textWrappingMode = TextWrappingModes.NoWrap;

            RectTransform rect = stage2TimerText.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(stage1TimerRect.pivot.x, 1f);
            rect.sizeDelta = new Vector2(0f, timerText.fontSize * 1.3f);
            rect.anchoredPosition = Vector2.zero;
        }

        if (stage2TimerText.gameObject.activeSelf != show)
            stage2TimerText.gameObject.SetActive(show);
        if (!show)
        {
            lastShownStage2Seconds = -1;
            return;
        }

        int seconds = Mathf.CeilToInt(clock.Remaining);
        if (seconds != lastShownStage2Seconds)
        {
            lastShownStage2Seconds = seconds;
            stage2TimerText.text = $"{seconds / 60}:{seconds % 60:00}";
        }
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

    // 대화·컷신·기절/게임 오버 연출 중에는 시간이 흐르지 않습니다. (위험도 DangerLevel도 같은 기준으로 멈춤)
    public static bool IsTimePaused()
    {
        // 산책(2스테이지) 중에는 3분 타이머가 멈추고, 복귀할 때 1분을 한 번에 뺍니다.
        if (Stage2HouseLink.IsInStage2)
            return true;

        // 일차 넘김 페이드 중
        if (DayController.IsTransitioning)
            return true;

        if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueOpen)
            return true;

        if (Stage1StoryController.Instance != null && Stage1StoryController.Instance.IsPlaying)
            return true;

        // 거실 '이상한 물약' 머릿속 연타 기믹 중
        if (StrangePotionGimmick.IsOpen)
            return true;

        // 손전등으로 비춘 그림(Light A~E)을 보는 중: 시간·위험도 모두 멈춤 (사용자 요청 2026-11-03)
        if (FlashlightGimmick.Instance != null && FlashlightGimmick.Instance.IsViewerOpen)
            return true;

        if (BoothMode.ForcingEnding)
            return true;   // 부스 모드 강제 엔딩 진행 중

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
