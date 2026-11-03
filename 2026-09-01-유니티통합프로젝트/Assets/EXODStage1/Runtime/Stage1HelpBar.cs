using System.Collections.Generic;
using EXOD.Walk;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 위쪽(하트 HUD 오른쪽)에 지금 지켜야 할 행동 요령을 띄웁니다.
/// - 집 안: 지금 해야 할 목표(진행 상황에 따라 바뀜) + 하루가 끝나기 전에 물건을 제자리에 돌려놓으라는 안내
/// - 산책 중: 그녀가 뒤돌아볼 때 건드리지 말라는 안내 (산책 기믹 규칙)
///   그녀가 뒤돌아보는 동안에는 빨갛게 깜빡이며 경고하고 심장 소리가 빨라집니다.
/// - 대화창이 떠 있을 때와 Tab으로 상단 UI를 숨겼을 때는 같이 숨깁니다.
/// </summary>
[DefaultExecutionOrder(270)]
public sealed class Stage1HelpBar : MonoBehaviour
{
    private const string Title = "행동 요령";
    private const string BedTip = "다음 날로 넘어가려면 침실 침대를 눌러.";

    private static readonly string[] HouseRules =
    {
        "하루가 끝나기 전에 꺼낸 물건은 모두 제자리로.",
        "퀵슬롯의 물건을 원래 자리에 끌어다 놓으면 돼.",
    };

    private static readonly Color GoalColor = new Color(1f, 0.82f, 0.45f, 1f);

    private static readonly string[] WalkTips =
    {
        "그녀와 길을 한 번 왕복하면 끝.",
        "그녀가 뒤돌아볼 때는 아무것도 건드리지 마.",
    };

    private const string WatchingWarning = "!! 그녀가 보고 있다! 손대지 마 !!";

    private static readonly Color PanelColor = new Color(0.06f, 0.05f, 0.06f, 0.72f);
    private static readonly Color WarningColor = new Color(0.55f, 0.03f, 0.06f, 0.9f);
    private static readonly Color TitleColor = new Color(0.86f, 0.30f, 0.40f, 1f);
    private static readonly Color TipColor = new Color(0.88f, 0.85f, 0.86f, 1f);

    private CanvasGroup group;
    private Image panel;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI tipText;
    private string shownTips;
    private bool warningShown;
    private WalkYandere yandere;
    private float nextTipRefreshTime;
    private RectTransform panelRect;

    // 왼쪽 Shift로 접기/펴기 (사용자 요청 2026-11-02). 씬을 다시 불러와도 유지.
    // (대화 중 Shift는 DialogueManager의 타이핑 넘기기 키라서, 대화창이 열려 있을 땐 접기/펴기 안 함)
    public const KeyCode CollapseKey = KeyCode.LeftShift;
    private static bool collapsed;
    // 오른쪽 가장자리에 세로로 긴 모양 (사용자 그림 2026-11-02: 가로 줄이고 오른쪽 아래로 길게)
    private const float ExpandedWidth = 720f;
    private const float MinExpandedHeight = 160f;
    private const float MaxExpandedHeight = 420f;
    private const float CollapsedWidth = 450f;
    private const float CollapsedHeight = 62f;

    private void Start()
    {
        Build();
    }

    private void OnDestroy()
    {
        if (warningShown)
            EXODSound.StopHeartbeat(0.3f);
    }

    private void Update()
    {
        bool hidden = (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueOpen)
            || HudToggle.IsStatusHidden
            || (PlayerFaintController.Instance != null && PlayerFaintController.Instance.IsBusy)
            // 1.5배로 커져 화면 가운데에 걸치므로, 확대 화면·키패드·기믹 창이 열려 있으면 숨김
            || (InspectionManager.Instance != null && InspectionManager.Instance.IsOpen)
            || (MemoZoom.Instance != null && MemoZoom.Instance.IsOpen) || Stage1Keypad.IsOpen || MirrorMazeGimmick.IsOpen || StrangePotionGimmick.IsOpen
            || Stage1FridgeGimmick.IsAnyInsideShown;

        bool dialogueOpen = DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueOpen;
        if (!dialogueOpen && Input.GetKeyDown(CollapseKey))
        {
            collapsed = !collapsed;
            EXODSound.Play("ui_click", 0.5f);
        }

        bool inWalk = Stage2HouseLink.IsInStage2;
        bool watching = inWalk && !hidden && IsHerWatching();

        SetWarning(watching);
        // 목표는 진행 상황을 찾아봐야 하므로 0.5초마다만 다시 계산합니다.
        if (!watching && (shownTips == null || Time.unscaledTime >= nextTipRefreshTime))
        {
            nextTipRefreshTime = Time.unscaledTime + 0.5f;
            SetTips(inWalk ? BuildWalkTips() : BuildHouseTips());
        }

        if (watching)
        {
            // 경고 중에는 빨갛게 깜빡입니다.
            float pulse = 0.65f + 0.35f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 8f));
            panel.color = Color.Lerp(PanelColor, WarningColor, pulse);
        }

        group.alpha = Mathf.MoveTowards(group.alpha, hidden ? 0f : 1f, Time.unscaledDeltaTime * 6f);
        ApplyCollapse(collapsed && !watching);   // 그녀가 보고 있다는 경고는 접혀 있어도 펼쳐서 보여 줌
    }

    // ─── 방 이름·남은 시간 / [대화 기록]·[종료] 버튼을 행동 요령 띠 바로 아래로 ───
    // (씬 파일은 건드리지 않고 실행 중에 위치만 옮김. 화면 좌표로 계산하므로 캔버스가 달라도 맞음)
    private RectTransform roomNameRect;
    private RectTransform logButtonRect;
    private RectTransform quitButtonRect;

    private void LateUpdate()
    {
        if (panelRect == null)
            return;

        if (roomNameRect == null && SliderManager.Instance != null && SliderManager.Instance.roomNameText != null)
            roomNameRect = SliderManager.Instance.roomNameText.rectTransform;
        if (logButtonRect == null)
            logButtonRect = FindRect("대화 기록 버튼");
        if (quitButtonRect == null)
            quitButtonRect = FindRect("종료 버튼");

        Vector3[] corners = new Vector3[4];
        panelRect.GetWorldCorners(corners);
        float scale = panelRect.lossyScale.y;
        float right = corners[2].x;
        float top = corners[0].y - 12f * scale;   // 띠 아래 12px

        // 방 이름 + 그 아래 남은 시간: 띠 오른쪽 아래
        float rowLeft = right;
        if (roomNameRect != null)
        {
            roomNameRect.GetWorldCorners(corners);
            Vector2 size = corners[2] - corners[0];
            Vector3 center = roomNameRect.position;
            Vector3 offset = center - (corners[0] + corners[2]) * 0.5f;
            roomNameRect.position = new Vector3(right - size.x * 0.5f, top - size.y * 0.5f, center.z) + offset;
            rowLeft = right - size.x - 16f * scale;
        }

        // 버튼은 방 이름 왼쪽에 같은 줄로
        rowLeft = PlaceLeftOf(quitButtonRect, rowLeft, top, scale);
        PlaceLeftOf(logButtonRect, rowLeft, top, scale);

        // 산책 중 "Esc 그만두기" 안내: 방 이름·시간(산책 시간 포함) 바로 아래 오른쪽
        if (walkHintRect == null && Stage2HouseLink.IsInStage2)
            walkHintRect = FindRect("조작");
        if (walkHintRect != null && roomNameRect != null)
        {
            float bottom = float.MaxValue;
            foreach (RectTransform child in roomNameRect.GetComponentsInChildren<RectTransform>(false))
            {
                child.GetWorldCorners(corners);
                bottom = Mathf.Min(bottom, corners[0].y);
            }
            walkHintRect.GetWorldCorners(corners);
            Vector2 size = corners[2] - corners[0];
            Vector3 offset = walkHintRect.position - (corners[0] + corners[2]) * 0.5f;
            walkHintRect.position = new Vector3(right - size.x * 0.5f, bottom - 6f * scale - size.y * 0.5f, walkHintRect.position.z) + offset;
        }
    }

    private RectTransform walkHintRect;

    private static float PlaceLeftOf(RectTransform rect, float rightEdge, float top, float scale)
    {
        if (rect == null)
            return rightEdge;
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        Vector2 size = corners[2] - corners[0];
        Vector3 offset = rect.position - (corners[0] + corners[2]) * 0.5f;
        rect.position = new Vector3(rightEdge - size.x * 0.5f, top - size.y * 0.5f, rect.position.z) + offset;
        return rightEdge - size.x - 16f * scale;
    }

    /// <summary>
    /// 아이템을 눌러 연 확대 화면·키패드·기믹 창이 떠 있으면 true.
    /// 이때 오른쪽 위 [대화 기록](DialogueLog)·[종료](GameMenu) 버튼도 숨깁니다. (사용자 요청 2026-11-02)
    /// </summary>
    public static bool IsCloseUpOpen()
    {
        return (InspectionManager.Instance != null && InspectionManager.Instance.IsOpen)
            || (MemoZoom.Instance != null && MemoZoom.Instance.IsOpen) || Stage1Keypad.IsOpen || MirrorMazeGimmick.IsOpen
            || StrangePotionGimmick.IsOpen || Stage1FridgeGimmick.IsAnyInsideShown;
    }

    private static RectTransform FindRect(string objectName)
    {
        GameObject go = GameObject.Find(objectName);
        return go != null ? go.transform as RectTransform : null;
    }

    private void ApplyCollapse(bool fold)
    {
        float expandedHeight = Mathf.Clamp(
            tipText.GetPreferredValues(tipText.text, ExpandedWidth - 54f, 0f).y + 75f,
            MinExpandedHeight, MaxExpandedHeight);
        Vector2 target = fold ? new Vector2(CollapsedWidth, CollapsedHeight) : new Vector2(ExpandedWidth, expandedHeight);
        panelRect.sizeDelta = Vector2.Lerp(panelRect.sizeDelta, target, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 18f));
        bool showTips = !fold && panelRect.sizeDelta.x > ExpandedWidth * 0.9f;
        if (tipText.gameObject.activeSelf != showTips)
            tipText.gameObject.SetActive(showTips);

        if (!warningShown)
        {
            string mode = GameDifficulty.IsEasy ? " <size=70%><color=#8CD9A6>(이지)</color></size>" : "";
            string title = fold ? Title + mode + "  <size=70%><color=#9A8F93>[Shift] 펼치기</color></size>"
                                : Title + mode + "  <size=70%><color=#9A8F93>[Shift] 접기</color></size>";
            if (titleText.text != title)
                titleText.text = title;
        }
    }

    private bool IsHerWatching()
    {
        WalkGameManager walk = WalkGameManager.Current;
        if (walk == null || !walk.IsRunning)
            return false;

        if (yandere == null)
            yandere = FindFirstObjectByType<WalkYandere>();

        return yandere != null && yandere.IsWatching && !yandere.Paused;
    }

    private void SetWarning(bool on)
    {
        if (warningShown == on)
            return;

        warningShown = on;
        if (on)
        {
            shownTips = null;
            titleText.text = "경고";
            titleText.color = Color.white;
            tipText.text = WatchingWarning;
            tipText.fontStyle = FontStyles.Bold;
            EXODSound.PlayHeartbeat(0.9f, 1.7f, 0.2f);
        }
        else
        {
            panel.color = PanelColor;
            titleText.text = Title;
            titleText.color = TitleColor;
            tipText.fontStyle = FontStyles.Normal;
            EXODSound.StopHeartbeat(0.6f);
        }
    }

    // 첫 줄: 지금 목표 (노란색), 그 아래: 제자리 규칙
    // 1일차 튜토리얼(여자친구 첫 등장)을 본 뒤 추가되는 행동 요령
    private const string HideTip = "여자친구가 올 것 같으면 방에 들어가 있자.";

    // 1일차 남은 20초부터 (사용자 요청 2026-11-03, kojjiburi)
    private const float FirstDayBedTipRemaining = 20f;
    private const string FirstDayBedTip = "일차가 얼마 남지 않았다면 침대를 눌러 다음 일자로 넘어가자.";

    private static string Yellow(string text) => $"<color=#{ColorUtility.ToHtmlStringRGB(GoalColor)}>{text}</color>";

    // 산책 중: 기본 안내 + [이지] 노란 힌트 (EasyHelpTips)
    private static string[] BuildWalkTips()
    {
        List<string> tips = new List<string>();
        foreach (string tip in EasyHelpTips.WalkTips())
            tips.Add(Yellow(tip));
        tips.AddRange(WalkTips);
        return tips.ToArray();
    }

    private static string[] BuildHouseTips()
    {
        List<string> tips = new List<string>();

        bool firstDay = GManager.Instance == null || GManager.Instance.Day <= 1;
        if (firstDay && DayCycleController.Instance != null && DayCycleController.Instance.RemainingSeconds <= FirstDayBedTipRemaining)
            tips.Add(Yellow(FirstDayBedTip));

        string goal = GetCurrentGoal();
        if (!string.IsNullOrEmpty(goal))
            tips.Add($"<color=#{ColorUtility.ToHtmlStringRGB(GoalColor)}>목표: {goal}</color>");

        // [이지] 노란 힌트 (EasyHelpTips)
        foreach (string tip in EasyHelpTips.HouseTips())
            tips.Add(Yellow(tip));

        // 손전등을 얻었고 화장실 금고가 아직 안 열렸으면: 종이를 비춰 보라는 안내 (사용자 요청 2026-11-02)
        if (HasFlashlight() && (RestroomGimmick.Instance == null || !RestroomGimmick.Instance.IsSafeUnlocked))
            tips.Add($"<color=#{ColorUtility.ToHtmlStringRGB(GoalColor)}>{FlashlightTip}</color>");

        tips.Add(HouseRules[0]);
        tips.Add(HouseRules[1]);
        if (DangerLevel.HideHintUnlocked)
            tips.Add(HideTip);
        if (!string.IsNullOrEmpty(goal))
            tips.Add(BedTip);

        return tips.ToArray();
    }

    private const string FlashlightTip = "손전등으로 종이 A~E를 비춰 보자.";

    private static bool HasFlashlight()
    {
        QuickSlotInventory inventory = QuickSlotInventory.Instance;
        if (inventory == null)
            return false;
        foreach (QuickSlotEntry entry in inventory.Entries)
        {
            if (entry != null && entry.ItemData != null && entry.ItemData.ItemId == FlashlightGimmick.FlashlightItemId)
                return true;
        }
        return false;
    }

    // 1스테이지 진행 상황에 맞는 목표
    private static string GetCurrentGoal()
    {
        PasswordDrawer drawer = FindFirstObjectByType<PasswordDrawer>(FindObjectsInactive.Include);
        SliderManager slider = SliderManager.Instance;
        bool bedroomDoorOpen = slider != null && slider.isUnlocked;

        if (!bedroomDoorOpen)
        {
            if (drawer != null && !drawer.IsUnlocked)
                return "서랍장 비밀번호를 찾자. 달력·사진·메모를 살펴봐.";
            if (drawer != null && !drawer.IsKeyCollected)
                return "열린 서랍에서 황금 열쇠를 챙기자.";
            return "황금 열쇠로 방문을 열자. 양옆 화살표를 눌러 봐.";
        }

        BearLimbGimmickManager bear = FindFirstObjectByType<BearLimbGimmickManager>();
        if (bear != null && !bear.IsCompleted)
            return $"곰돌이 팔다리를 찾아 돌려주자. ({bear.CollectedCount}/{BearLimbGimmickManager.LimbCount})";
        if (bear != null && bear.IsRewardPending)
            return "곰돌이가 내일 아침에 보답해 준대. 침대를 눌러 하루를 넘기자.";

        Stage1FridgeGimmick fridge = FindFirstObjectByType<Stage1FridgeGimmick>();
        if (fridge != null && !fridge.IsOpened)
            return "곰돌이가 준 쪽지의 번호로 주방 냉장고를 열자.";

        Stage1FrontDoorGimmick frontDoor = FindFirstObjectByType<Stage1FrontDoorGimmick>();
        if (frontDoor != null && !frontDoor.IsPasswordSolved)
            return "복도 끝 현관 도어락을 열자. 냉장고 메모와 과일 개수가 단서야.";

        return "현관문으로 밖에 나가 보자.";
    }

    private void SetTips(string[] tips)
    {
        string text = string.Join("\n", tips);
        if (text == shownTips)
            return;

        shownTips = text;
        tipText.text = text;
    }

    // ═══════════════════════════════════════════════════
    //  UI 만들기
    // ═══════════════════════════════════════════════════

    private void Build()
    {
        GameObject root = new GameObject("행동 요령", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        root.transform.SetParent(transform, false);

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 610;  // 하트 HUD(600) 위, 키패드(700) 아래

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        group = root.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        // 1.5배 크기 (2026-11-02 사용자 요청): 하트 HUD와 겹치지 않게 오른쪽 위 [대화 기록]·[종료] 버튼 아래에 둠
        GameObject panelObject = new GameObject("안내 띠", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelObject.transform.SetParent(root.transform, false);
        RectTransform rect = panelObject.GetComponent<RectTransform>();
        panelRect = rect;
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        // 화면 오른쪽 맨 위 (사용자 요청 2026-11-02). 방 이름·시간과 [대화 기록]·[종료] 버튼은 이 띠 아래로 옮김 (LateUpdate)
        rect.anchoredPosition = new Vector2(-40f, -20f);
        rect.sizeDelta = collapsed ? new Vector2(CollapsedWidth, CollapsedHeight) : new Vector2(ExpandedWidth, MinExpandedHeight);

        panel = panelObject.GetComponent<Image>();
        panel.color = PanelColor;
        panel.raycastTarget = false;

        titleText = CreateText("제목", panelObject.transform, Title, 33f, TitleColor, TextAlignmentOptions.TopLeft);
        RectTransform titleRect = titleText.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.offsetMin = new Vector2(27f, -54f);
        titleRect.offsetMax = new Vector2(-27f, -12f);

        tipText = CreateText("안내", panelObject.transform, string.Empty, 33f, TipColor, TextAlignmentOptions.TopLeft);
        // 폭이 좁아졌으므로 글자 크기는 고정하고 줄바꿈해서 아래로 길어지게 함
        tipText.fontSize = 28f;
        tipText.enableWordWrapping = true;
        tipText.lineSpacing = 8f;
        RectTransform tipRect = tipText.rectTransform;
        tipRect.anchorMin = Vector2.zero;
        tipRect.anchorMax = Vector2.one;
        tipRect.offsetMin = new Vector2(27f, 12f);
        tipRect.offsetMax = new Vector2(-27f, -57f);
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string value, float size, Color color,
        TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        if (EXODUiAssets.Font != null)
            text.font = EXODUiAssets.Font;
        text.text = value;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }
}
