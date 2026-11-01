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

    private static readonly string[] HouseRules =
    {
        "하루가 끝나기 전에 꺼낸 물건은 모두 제자리로.",
        "퀵슬롯의 물건을 원래 자리에 끌어다 놓으면 돼.",
    };

    private static readonly Color GoalColor = new Color(1f, 0.82f, 0.45f, 1f);

    private static readonly string[] WalkTips =
    {
        "그녀와 길을 두 번 왕복하면 끝.",
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
            || (PlayerFaintController.Instance != null && PlayerFaintController.Instance.IsBusy);

        bool inWalk = Stage2HouseLink.IsInStage2;
        bool watching = inWalk && !hidden && IsHerWatching();

        SetWarning(watching);
        // 목표는 진행 상황을 찾아봐야 하므로 0.5초마다만 다시 계산합니다.
        if (!watching && (shownTips == null || Time.unscaledTime >= nextTipRefreshTime))
        {
            nextTipRefreshTime = Time.unscaledTime + 0.5f;
            SetTips(inWalk ? WalkTips : BuildHouseTips());
        }

        if (watching)
        {
            // 경고 중에는 빨갛게 깜빡입니다.
            float pulse = 0.65f + 0.35f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 8f));
            panel.color = Color.Lerp(PanelColor, WarningColor, pulse);
        }

        group.alpha = Mathf.MoveTowards(group.alpha, hidden ? 0f : 1f, Time.unscaledDeltaTime * 6f);
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
    private static string[] BuildHouseTips()
    {
        string goal = GetCurrentGoal();
        if (string.IsNullOrEmpty(goal))
            return HouseRules;

        return new[]
        {
            $"<color=#{ColorUtility.ToHtmlStringRGB(GoalColor)}>목표: {goal}</color>",
            HouseRules[0],
            HouseRules[1],
        };
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

        // 하트 HUD 오른쪽, 방 이름·타이머 왼쪽 빈자리
        GameObject panelObject = new GameObject("안내 띠", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelObject.transform.SetParent(root.transform, false);
        RectTransform rect = panelObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-190f, -22f);
        rect.sizeDelta = new Vector2(640f, 132f);

        panel = panelObject.GetComponent<Image>();
        panel.color = PanelColor;
        panel.raycastTarget = false;

        titleText = CreateText("제목", panelObject.transform, Title, 22f, TitleColor, TextAlignmentOptions.TopLeft);
        RectTransform titleRect = titleText.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.offsetMin = new Vector2(18f, -36f);
        titleRect.offsetMax = new Vector2(-18f, -8f);

        tipText = CreateText("안내", panelObject.transform, string.Empty, 22f, TipColor, TextAlignmentOptions.TopLeft);
        tipText.enableAutoSizing = true;
        tipText.fontSizeMin = 14f;
        tipText.fontSizeMax = 22f;
        RectTransform tipRect = tipText.rectTransform;
        tipRect.anchorMin = Vector2.zero;
        tipRect.anchorMax = Vector2.one;
        tipRect.offsetMin = new Vector2(18f, 8f);
        tipRect.offsetMax = new Vector2(-18f, -38f);
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
