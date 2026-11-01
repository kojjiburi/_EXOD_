using EXOD.Walk;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 게임 중 [종료] 버튼과 종료 메뉴
/// - 화면 오른쪽 위 [대화 기록 (L)] 버튼 옆의 [종료] 버튼을 누르면 메뉴가 열리고 게임이 잠시 멈춥니다.
/// - 메뉴: 게임 종료 / 타이틀 화면으로 / 다시 시작 / 돌아가기
/// - 다시 시작·타이틀로 갈 때는 퀵슬롯, 조사 기록, 산책 현관문 잠금 같은 남아 있는 진행 상황을 지웁니다.
/// - 1스테이지 씬(SliderManager가 있는 씬)에 자동으로 붙습니다. 2스테이지(산책) 중에도 보입니다.
/// </summary>
[DefaultExecutionOrder(280)]
public sealed class GameMenu : MonoBehaviour
{
    private const string TitleSceneName = "StartScenes";

    private static readonly Color PanelColor = new Color(0.07f, 0.05f, 0.06f, 0.97f);
    private static readonly Color TextColor = new Color(0.90f, 0.87f, 0.88f, 1f);
    private static readonly Color TitleColor = new Color(0.86f, 0.30f, 0.40f, 1f);

    public static GameMenu Instance { get; private set; }
    public static bool IsOpen => Instance != null && Instance.menuRoot != null && Instance.menuRoot.activeSelf;

    private string gameSceneName;
    private GameObject menuRoot;
    private CanvasGroup buttonGroup;
    private float timeScaleBeforeOpen = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHandler()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single || Instance != null)
            return;

        SliderManager slider = FindFirstObjectByType<SliderManager>();
        if (slider == null || slider.gameObject.scene != scene)
            return;

        slider.gameObject.AddComponent<GameMenu>();
    }

    private void Awake()
    {
        Instance = this;
        gameSceneName = gameObject.scene.name;
    }

    private void Start()
    {
        Build();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            if (IsOpen)
                Time.timeScale = timeScaleBeforeOpen;
            Instance = null;
        }
    }

    private void Update()
    {
        if (buttonGroup == null)
            return;

        // 버튼은 상단 UI를 Tab으로 숨겼을 때·메뉴가 열려 있을 때·일차 넘김 페이드 중에는 숨깁니다.
        bool showButton = !HudToggle.IsStatusHidden && !IsOpen && !DayController.IsTransitioning;
        buttonGroup.alpha = showButton ? 1f : 0f;
        buttonGroup.blocksRaycasts = showButton;

        if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
            Close();
    }

    // ═══════════════════════════════════════════════════
    //  메뉴 동작
    // ═══════════════════════════════════════════════════

    public void Open()
    {
        if (menuRoot == null || IsOpen)
            return;

        timeScaleBeforeOpen = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        menuRoot.SetActive(true);
    }

    public void Close()
    {
        if (!IsOpen)
            return;

        menuRoot.SetActive(false);
        Time.timeScale = timeScaleBeforeOpen;
    }

    private void QuitGame()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void GoToTitle()
    {
        ResetRunState();
        SceneManager.LoadScene(TitleSceneName);
    }

    private void RestartGame()
    {
        ResetRunState();
        // 산책(2스테이지) 중이면 지금 활성 씬이 산책 씬이라, 1스테이지 씬 이름으로 다시 불러옵니다.
        SceneManager.LoadScene(gameSceneName);
    }

    /// <summary>씬을 다시 불러와도 남아 있는 진행 상황을 지웁니다.</summary>
    public static void ResetRunState()
    {
        Time.timeScale = 1f;
        InspectionState.ResetAll();
        DayClock.ResetForNewGame();

        if (QuickSlotInventory.Instance != null)
            QuickSlotInventory.Instance.Clear();
    }

    // ═══════════════════════════════════════════════════
    //  UI 만들기
    // ═══════════════════════════════════════════════════

    private void Build()
    {
        GameObject root = new GameObject("종료 메뉴", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1100;  // 기절·게임 오버 화면(1000)보다 위

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        BuildOpenButton(root.transform);
        BuildMenu(root.transform);
    }

    // [대화 기록 (L)] 버튼 오른쪽의 작은 버튼
    private void BuildOpenButton(Transform parent)
    {
        Button button = CreateButton(parent, "종료 버튼", "종료", 22f);
        RectTransform rect = (RectTransform)button.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-40f, -162f);
        rect.sizeDelta = new Vector2(130f, 44f);
        button.onClick.AddListener(Open);
        buttonGroup = button.gameObject.AddComponent<CanvasGroup>();
    }

    private void BuildMenu(Transform parent)
    {
        menuRoot = new GameObject("종료 메뉴 창", typeof(RectTransform));
        menuRoot.transform.SetParent(parent, false);
        Stretch((RectTransform)menuRoot.transform);

        // 뒤쪽 클릭을 막는 어두운 배경
        Image dim = CreateImage("어두운 배경", menuRoot.transform, new Color(0f, 0f, 0f, 0.6f));
        Stretch(dim.rectTransform);

        Image panel = CreateImage("창", menuRoot.transform, PanelColor);
        Place(panel.rectTransform, Vector2.zero, new Vector2(560f, 520f));

        TextMeshProUGUI title = CreateText("제목", panel.transform, "메뉴", 44f, TitleColor);
        Place(title.rectTransform, new Vector2(0f, 190f), new Vector2(500f, 70f));

        string[] labels = { "게임 종료", "타이틀 화면으로", "다시 시작", "돌아가기" };
        UnityEngine.Events.UnityAction[] actions = { QuitGame, GoToTitle, RestartGame, Close };
        for (int i = 0; i < labels.Length; i++)
        {
            Button button = CreateButton(panel.transform, labels[i], labels[i], 32f);
            Place((RectTransform)button.transform, new Vector2(0f, 90f - i * 95f), new Vector2(400f, 76f));
            button.onClick.AddListener(actions[i]);
        }

        menuRoot.SetActive(false);
    }

    private static Button CreateButton(Transform parent, string name, string label, float size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = Color.white;

        Button button = go.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.06f, 0.05f, 0.06f, 0.72f);
        colors.highlightedColor = new Color(0.40f, 0.12f, 0.16f, 0.9f);
        colors.pressedColor = new Color(0.55f, 0.16f, 0.22f, 0.95f);
        colors.selectedColor = colors.normalColor;
        button.colors = colors;
        EXODSound.AttachUiSounds(go);

        TextMeshProUGUI text = CreateText("글자", go.transform, label, size, TextColor);
        Stretch(text.rectTransform);
        return button;
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string value, float size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        if (EXODUiAssets.Font != null)
            text.font = EXODUiAssets.Font;
        text.text = value;
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    private static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
