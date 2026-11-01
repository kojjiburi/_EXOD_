using EXOD.Walk;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 위쪽(하트 HUD 오른쪽)에 지금 지켜야 할 행동 요령을 띄웁니다.
/// - 집 안: 하루가 끝나기 전에 물건을 제자리에 돌려놓으라는 안내 (친구의 제자리 점검 규칙)
/// - 산책 중: 그녀가 뒤돌아볼 때 건드리지 말라는 안내 (산책 기믹 규칙)
///   그녀가 뒤돌아보는 동안에는 빨갛게 깜빡이며 경고하고 심장 소리가 빨라집니다.
/// - 대화창이 떠 있을 때와 Tab으로 상단 UI를 숨겼을 때는 같이 숨깁니다.
/// </summary>
[DefaultExecutionOrder(270)]
public sealed class Stage1HelpBar : MonoBehaviour
{
    private const string Title = "행동 요령";

    private static readonly string[] HouseTips =
    {
        "하루가 끝나기 전에 꺼낸 물건은 모두 제자리로.",
        "퀵슬롯의 물건을 원래 자리에 끌어다 놓으면 돼.",
    };

    private static readonly string[] WalkTips =
    {
        "길은 저절로 걷는다.",
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
        if (!watching)
            SetTips(inWalk ? WalkTips : HouseTips);

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
        rect.sizeDelta = new Vector2(640f, 112f);

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
