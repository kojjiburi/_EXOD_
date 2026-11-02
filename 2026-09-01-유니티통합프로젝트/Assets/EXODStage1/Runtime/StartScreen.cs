using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 시작 화면(StartScenes)을 만듭니다. UI는 실행 중에 자동으로 만들어집니다.
/// - 배경: 어두운 침실, 붉은빛이 새어 나오는 문 (Resources/EXODStage1/StartBackground.png)
/// - 고장 난 형광등처럼 깜빡이고, 가끔 문 앞에 여자친구 실루엣이 스쳐 지나갑니다.
/// - 제목이 가끔 지직거립니다. (제목은 GameTitle, 부제는 Subtitle에서 바꿉니다)
/// - 메뉴: 시작하기 / 설정(음량) / 종료
/// - 시작하기: 암전 → "철컥" → 침실 씬
/// </summary>
public sealed class StartScreen : MonoBehaviour
{
    private const string GameTitle = "Exit or Die";
    private const string Subtitle = "Nemo : No Exit My Love";
    private const string TeamName = "TEAM EXOD";
    private const string StartSceneName = "StartScenes";
    private const string FirstStageSceneName = "SampleScene";

    private static readonly Color TextColor = new Color(0.78f, 0.74f, 0.76f, 1f);
    private static readonly Color HoverColor = new Color(0.88f, 0.12f, 0.18f, 1f);
    private static readonly Color TitleColor = new Color(0.62f, 0.04f, 0.08f, 1f);

    private TMP_FontAsset font;
    private RectTransform canvasRoot;
    private CanvasGroup menuGroup;
    private Image blackout;
    private Image flicker;
    private Image silhouette;
    private TextMeshProUGUI title;
    private TextMeshProUGUI titleGhostRed;
    private TextMeshProUGUI titleGhostCyan;
    private GameObject settingsPanel;
    private GameObject endingsPanel;
    private RectTransform endingsWindow;
    private bool isStarting;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHandler()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == StartSceneName && FindFirstObjectByType<StartScreen>() == null)
            new GameObject("시작 화면").AddComponent<StartScreen>();
    }

    private void Start()
    {
        // 예전 시작 버튼(스프라이트)은 숨기고 새 메뉴를 씁니다.
        GameObject oldButton = GameObject.Find("StartButton");
        if (oldButton != null)
            oldButton.SetActive(false);

        if (FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        font = EXODUiAssets.Font;
        BuildUi();

        EXODSound.PlayAmbience("ambience_drone", 0.5f, 2f);
        EXODSound.PlayHeartbeat(0.6f, 0.85f, 2f);

        StartCoroutine(FadeImage(blackout, 1f, 0f, 1.5f));
        StartCoroutine(FlickerLoop());
        StartCoroutine(TitleGlitchLoop());
    }

    private void Update()
    {
        if (settingsPanel != null && settingsPanel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
            CloseSettings();
        if (endingsPanel != null && endingsPanel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
            CloseEndings();
        if (difficultyPanel != null && difficultyPanel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
            CloseDifficulty();
    }

    // ═══════════════════════════════════════════════════
    //  메뉴 동작
    // ═══════════════════════════════════════════════════

    private void OnStartClicked()
    {
        if (isStarting)
            return;

        // 난이도부터 고름 (이지 / 하드)
        difficultyPanel.SetActive(true);
        menuGroup.interactable = false;
    }

    private void StartWithDifficulty(bool easy)
    {
        if (isStarting)
            return;

        GameDifficulty.IsEasy = easy;
        difficultyPanel.SetActive(false);
        isStarting = true;
        StartCoroutine(StartGameRoutine());
    }

    private void CloseDifficulty()
    {
        difficultyPanel.SetActive(false);
        menuGroup.interactable = true;
    }

    // ─── 난이도 고르기 창 ───────────────────────────────

    private GameObject difficultyPanel;

    private void BuildDifficultyPanel()
    {
        Image dim = CreateImage("난이도", canvasRoot, new Color(0f, 0f, 0f, 0.8f));
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;
        difficultyPanel = dim.gameObject;

        Image window = CreateImage("난이도 창", dim.rectTransform, new Color(0.07f, 0.04f, 0.05f, 1f));
        Place(window.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1200f, 600f), Vector2.zero);
        Outline outline = window.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.45f, 0.06f, 0.1f, 1f);
        outline.effectDistance = new Vector2(3f, -3f);

        TextMeshProUGUI heading = CreateText("제목", window.rectTransform, "난이도 선택", 52f, TitleColor);
        Place(heading.rectTransform, new Vector2(0.5f, 1f), new Vector2(800f, 90f), new Vector2(0f, -70f));

        CreateDifficultyCard(window.rectTransform, new Vector2(-270f, -10f), "이지",
            "처음 하는 사람용\n짧게 끝낼 수 있어요", new Color(0.55f, 0.85f, 0.65f, 1f), () => StartWithDifficulty(true));
        CreateDifficultyCard(window.rectTransform, new Vector2(270f, -10f), "하드",
            "원래 난이도\n모든 단서를 직접 찾아야 해요", new Color(0.88f, 0.12f, 0.18f, 1f), () => StartWithDifficulty(false));

        Image close = CreateImage("닫기", window.rectTransform, Color.white);
        Place(close.rectTransform, new Vector2(0.5f, 0f), new Vector2(200f, 64f), new Vector2(0f, 60f));
        Button closeButton = close.gameObject.AddComponent<Button>();
        ColorBlock colors = closeButton.colors;
        colors.normalColor = new Color(0.20f, 0.14f, 0.14f, 1f);
        colors.highlightedColor = new Color(0.45f, 0.16f, 0.20f, 1f);
        colors.pressedColor = new Color(0.62f, 0.20f, 0.26f, 1f);
        colors.selectedColor = colors.normalColor;
        closeButton.colors = colors;
        closeButton.onClick.AddListener(CloseDifficulty);
        EXODSound.AttachUiSounds(close.gameObject);
        TextMeshProUGUI closeText = CreateText("글자", close.rectTransform, "뒤로", 30f, Color.white);
        Stretch(closeText.rectTransform);

        difficultyPanel.SetActive(false);
    }

    private void CreateDifficultyCard(RectTransform parent, Vector2 position, string name, string desc, Color accent,
        UnityEngine.Events.UnityAction onClick)
    {
        Image card = CreateImage(name, parent, new Color(0.13f, 0.08f, 0.10f, 1f));
        Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(460f, 260f), position);
        Outline outline = card.gameObject.AddComponent<Outline>();
        outline.effectColor = accent;
        outline.effectDistance = new Vector2(3f, -3f);

        Button button = card.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.6f, 1.3f, 1.4f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        colors.selectedColor = colors.normalColor;
        colors.colorMultiplier = 2f;
        button.colors = colors;
        button.onClick.AddListener(onClick);
        EXODSound.AttachUiSounds(card.gameObject);

        TextMeshProUGUI title = CreateText("이름", card.rectTransform, name, 64f, accent);
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(420f, 100f), new Vector2(0f, -70f));
        TextMeshProUGUI description = CreateText("설명", card.rectTransform, desc, 28f, TextColor);
        Place(description.rectTransform, new Vector2(0.5f, 0f), new Vector2(420f, 110f), new Vector2(0f, 75f));
    }

    private IEnumerator StartGameRoutine()
    {
        menuGroup.interactable = false;
        menuGroup.blocksRaycasts = false;
        EXODSound.StopHeartbeat(0.8f);

        yield return FadeImage(blackout, 0f, 1f, 1f);

        EXODSound.Play("door_lock");
        yield return new WaitForSecondsRealtime(1.5f);

        SceneManager.LoadScene(FirstStageSceneName);
    }

    private void OpenSettings()
    {
        settingsPanel.SetActive(true);
        menuGroup.interactable = false;
    }

    private void CloseSettings()
    {
        settingsPanel.SetActive(false);
        menuGroup.interactable = true;
    }

    private void OpenEndings()
    {
        BuildEndingCards();
        endingsPanel.SetActive(true);
        menuGroup.interactable = false;
    }

    private void CloseEndings()
    {
        endingsPanel.SetActive(false);
        menuGroup.interactable = true;
    }

    private static void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ═══════════════════════════════════════════════════
    //  연출
    // ═══════════════════════════════════════════════════

    // 고장 난 형광등처럼 불규칙하게 깜빡입니다. 세 번에 한 번은 문 앞에 실루엣이 비칩니다.
    private IEnumerator FlickerLoop()
    {
        yield return new WaitForSecondsRealtime(4f);

        int count = 0;
        while (!isStarting)
        {
            bool withSilhouette = ++count % 3 == 0;
            float[] pattern = { 0.55f, 0.1f, 0.75f, 0f };
            for (int i = 0; i < pattern.Length; i++)
            {
                SetAlpha(flicker, pattern[i]);
                if (withSilhouette && i == 1)
                    ShowSilhouette(true);
                yield return new WaitForSecondsRealtime(Random.Range(0.04f, 0.11f));
            }

            if (withSilhouette)
            {
                EXODSound.Play("glitch", 0.5f);
                yield return new WaitForSecondsRealtime(0.35f);
                SetAlpha(flicker, 0.8f);
                ShowSilhouette(false);
                yield return new WaitForSecondsRealtime(0.08f);
                SetAlpha(flicker, 0f);
            }

            yield return new WaitForSecondsRealtime(Random.Range(3f, 7f));
        }
    }

    private void ShowSilhouette(bool visible)
    {
        if (silhouette != null)
            silhouette.gameObject.SetActive(visible);
    }

    // 제목은 늘 붉은색·푸른색 잔상으로 살짝 갈라진 채 떨리고, 가끔 크게 지직거립니다.
    private IEnumerator TitleGlitchLoop()
    {
        Vector2 home = title.rectTransform.anchoredPosition;

        while (true)
        {
            // 평소: 약한 글리치를 계속 (가끔 한 번씩 툭 튀기)
            float calmEnd = Time.unscaledTime + Random.Range(1.5f, 4f);
            while (Time.unscaledTime < calmEnd)
            {
                bool jump = Random.value < 0.08f;
                float shake = jump ? 6f : 1.5f;
                title.rectTransform.anchoredPosition = home + new Vector2(Random.Range(-shake, shake), 0f);
                titleGhostRed.rectTransform.anchoredPosition = home + new Vector2(Random.Range(-7f, -3f), Random.Range(-1.5f, 1.5f));
                titleGhostCyan.rectTransform.anchoredPosition = home + new Vector2(Random.Range(3f, 7f), Random.Range(-1.5f, 1.5f));
                SetAlpha(titleGhostRed, Random.Range(0.3f, 0.45f));
                SetAlpha(titleGhostCyan, Random.Range(0.2f, 0.35f));
                title.enabled = true;
                yield return new WaitForSecondsRealtime(Random.Range(0.05f, 0.09f));
            }

            if (Random.value < 0.5f)
                EXODSound.Play("glitch", 0.35f);

            float end = Time.unscaledTime + Random.Range(0.12f, 0.25f);
            while (Time.unscaledTime < end)
            {
                title.rectTransform.anchoredPosition = home + new Vector2(Random.Range(-10f, 10f), 0f);
                titleGhostRed.rectTransform.anchoredPosition = home + new Vector2(Random.Range(-18f, -6f), Random.Range(-3f, 3f));
                titleGhostCyan.rectTransform.anchoredPosition = home + new Vector2(Random.Range(6f, 18f), Random.Range(-3f, 3f));
                SetAlpha(titleGhostRed, 0.7f);
                SetAlpha(titleGhostCyan, 0.5f);
                title.enabled = Random.value > 0.15f;
                yield return new WaitForSecondsRealtime(0.03f);
            }

            title.enabled = true;
            title.rectTransform.anchoredPosition = home;
        }
    }

    // ═══════════════════════════════════════════════════
    //  UI 만들기
    // ═══════════════════════════════════════════════════

    private void BuildUi()
    {
        GameObject root = new GameObject(
            "시작 화면 UI",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasRoot = root.GetComponent<RectTransform>();

        Image background = CreateImage("배경", canvasRoot, Color.black);
        Stretch(background.rectTransform);

        RectTransform room = BuildRoomBackground();
        BuildSilhouette(room);

        flicker = CreateImage("깜빡임", canvasRoot, Color.black);
        Stretch(flicker.rectTransform);
        SetAlpha(flicker, 0f);

        BuildTitle();
        BuildMenu();

        TextMeshProUGUI team = CreateText("팀 이름", canvasRoot, TeamName, 24f, new Color(0.35f, 0.33f, 0.34f, 1f));
        Place(team.rectTransform, new Vector2(1f, 0f), new Vector2(400f, 50f), new Vector2(-230f, 50f));
        team.alignment = TextAlignmentOptions.Right;

        BuildSettingsPanel();
        BuildEndingsPanel();
        BuildDifficultyPanel();

        blackout = CreateImage("암전", canvasRoot, Color.black);
        Stretch(blackout.rectTransform);
        blackout.raycastTarget = false;
    }

    // 화면 비율이 달라도 꽉 차도록(넘치는 부분은 잘림) 배경을 놓습니다.
    private RectTransform BuildRoomBackground()
    {
        Image room = CreateImage("침실", canvasRoot, Color.white);
        Texture2D texture = Resources.Load<Texture2D>("EXODStage1/StartBackground");
        if (texture != null)
        {
            room.sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        }
        else
        {
            room.color = new Color(0.05f, 0.04f, 0.05f, 1f);
            Debug.LogWarning("[StartScreen] 배경 그림이 없습니다: Resources/EXODStage1/StartBackground.png");
        }

        RectTransform rect = room.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        AspectRatioFitter fitter = room.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = 16f / 9f;
        return rect;
    }

    // 문틈(배경 그림 가로 49%, 아래에서 26% 지점) 앞에 서 있는 검은 실루엣
    private void BuildSilhouette(RectTransform room)
    {
        Sprite sprite = DialoguePortrait.LoadPortrait();
        if (sprite == null)
            return;

        silhouette = CreateImage("실루엣", room, new Color(0.02f, 0f, 0.01f, 0.92f));
        silhouette.sprite = sprite;
        silhouette.preserveAspect = true;

        RectTransform rect = silhouette.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.49f, 0.26f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(600f * sprite.rect.width / sprite.rect.height, 600f);
        rect.anchoredPosition = Vector2.zero;
        silhouette.gameObject.SetActive(false);
    }

    private void BuildTitle()
    {
        Vector2 anchor = new Vector2(0.79f, 0.68f);
        Vector2 size = new Vector2(900f, 200f);

        titleGhostCyan = CreateText("제목 잔상(푸른)", canvasRoot, GameTitle, 110f, new Color(0.2f, 0.7f, 0.75f, 0f));
        Place(titleGhostCyan.rectTransform, anchor, size, Vector2.zero);
        titleGhostRed = CreateText("제목 잔상(붉은)", canvasRoot, GameTitle, 110f, new Color(1f, 0.1f, 0.15f, 0f));
        Place(titleGhostRed.rectTransform, anchor, size, Vector2.zero);
        title = CreateText("제목", canvasRoot, GameTitle, 110f, TitleColor);
        Place(title.rectTransform, anchor, size, Vector2.zero);

        TextMeshProUGUI subtitle = CreateText("부제", canvasRoot, Subtitle, 34f, new Color(0.62f, 0.58f, 0.60f, 1f));
        Place(subtitle.rectTransform, new Vector2(0.79f, 0.575f), new Vector2(760f, 50f), Vector2.zero);
        subtitle.characterSpacing = 4f;
    }

    private void BuildMenu()
    {
        GameObject menu = new GameObject("메뉴", typeof(RectTransform), typeof(CanvasGroup));
        menu.transform.SetParent(canvasRoot, false);
        RectTransform rect = menu.GetComponent<RectTransform>();
        Stretch(rect);
        menuGroup = menu.GetComponent<CanvasGroup>();

        CreateMenuButton(rect, "시작하기", new Vector2(0.79f, 0.44f), OnStartClicked);
        CreateMenuButton(rect, "엔딩 목록", new Vector2(0.79f, 0.365f), OpenEndings);
        CreateMenuButton(rect, "설정", new Vector2(0.79f, 0.29f), OpenSettings);
        CreateMenuButton(rect, "종료", new Vector2(0.79f, 0.215f), QuitGame);
    }

    // 글자만 보이는 메뉴 버튼. 마우스를 올리면 붉어지고 떨립니다.
    private void CreateMenuButton(RectTransform parent, string label, Vector2 anchor, UnityEngine.Events.UnityAction onClick)
    {
        Image hitArea = CreateImage(label, parent, new Color(0f, 0f, 0f, 0f));
        Place(hitArea.rectTransform, anchor, new Vector2(360f, 70f), Vector2.zero);

        Button button = hitArea.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(onClick);

        TextMeshProUGUI text = CreateText("글자", hitArea.rectTransform, label, 44f, TextColor);
        Stretch(text.rectTransform);

        Coroutine shake = null;
        EventTrigger trigger = EXODSound.AttachUiSounds(hitArea.gameObject);
        EXODSound.AddTrigger(trigger, EventTriggerType.PointerEnter, () =>
        {
            if (!menuGroup.interactable)
                return;
            text.color = HoverColor;
            text.text = "- " + label + " -";
            shake = StartCoroutine(Shake(text.rectTransform));
        });
        EXODSound.AddTrigger(trigger, EventTriggerType.PointerExit, () =>
        {
            text.color = TextColor;
            text.text = label;
            if (shake != null)
                StopCoroutine(shake);
            text.rectTransform.anchoredPosition = Vector2.zero;
        });
    }

    private static IEnumerator Shake(RectTransform rect)
    {
        while (true)
        {
            rect.anchoredPosition = new Vector2(Random.Range(-2.5f, 2.5f), Random.Range(-1.5f, 1.5f));
            yield return new WaitForSecondsRealtime(0.05f);
        }
    }

    private void BuildSettingsPanel()
    {
        Image dim = CreateImage("설정", canvasRoot, new Color(0f, 0f, 0f, 0.7f));
        Stretch(dim.rectTransform);
        settingsPanel = dim.gameObject;

        Image panel = CreateImage("설정 창", dim.rectTransform, new Color(0.08f, 0.05f, 0.06f, 0.96f));
        Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(760f, 400f), Vector2.zero);
        Outline outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.45f, 0.06f, 0.1f, 1f);
        outline.effectDistance = new Vector2(3f, -3f);

        TextMeshProUGUI heading = CreateText("제목", panel.rectTransform, "설정", 48f, TitleColor);
        Place(heading.rectTransform, new Vector2(0.5f, 1f), new Vector2(600f, 80f), new Vector2(0f, -70f));

        TextMeshProUGUI label = CreateText("음량 글자", panel.rectTransform, "음량", 34f, TextColor);
        Place(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(140f, 60f), new Vector2(-270f, 10f));

        TextMeshProUGUI value = CreateText("음량 값", panel.rectTransform, "", 30f, TextColor);
        Place(value.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(120f, 60f), new Vector2(280f, 10f));

        Slider slider = CreateSlider(panel.rectTransform, new Vector2(0f, 10f));
        slider.value = EXODSound.MasterVolume;
        value.text = Mathf.RoundToInt(slider.value * 100f) + "%";
        slider.onValueChanged.AddListener(v =>
        {
            EXODSound.MasterVolume = v;
            value.text = Mathf.RoundToInt(v * 100f) + "%";
        });

        Image close = CreateImage("닫기", panel.rectTransform, Color.white);
        Place(close.rectTransform, new Vector2(0.5f, 0f), new Vector2(220f, 70f), new Vector2(0f, 70f));
        Button closeButton = close.gameObject.AddComponent<Button>();
        ColorBlock colors = closeButton.colors;
        colors.normalColor = new Color(0.20f, 0.14f, 0.14f, 1f);
        colors.highlightedColor = new Color(0.45f, 0.16f, 0.20f, 1f);
        colors.pressedColor = new Color(0.62f, 0.20f, 0.26f, 1f);
        colors.selectedColor = colors.normalColor;
        closeButton.colors = colors;
        closeButton.onClick.AddListener(CloseSettings);
        EXODSound.AttachUiSounds(close.gameObject);

        TextMeshProUGUI closeText = CreateText("글자", close.rectTransform, "닫기", 32f, Color.white);
        Stretch(closeText.rectTransform);

        settingsPanel.SetActive(false);
    }

    // ─── 엔딩 목록 창 (EndingCollection) ─────────────────

    private void BuildEndingsPanel()
    {
        Image dim = CreateImage("엔딩 목록", canvasRoot, new Color(0f, 0f, 0f, 0.82f));
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;
        endingsPanel = dim.gameObject;

        Image window = CreateImage("엔딩 목록 창", dim.rectTransform, new Color(0.07f, 0.04f, 0.05f, 1f));
        Place(window.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1640f, 800f), Vector2.zero);
        Outline outline = window.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.45f, 0.06f, 0.1f, 1f);
        outline.effectDistance = new Vector2(3f, -3f);
        endingsWindow = window.rectTransform;

        Image close = CreateImage("닫기", window.rectTransform, Color.white);
        Place(close.rectTransform, new Vector2(0.5f, 0f), new Vector2(220f, 70f), new Vector2(0f, 65f));
        Button closeButton = close.gameObject.AddComponent<Button>();
        ColorBlock colors = closeButton.colors;
        colors.normalColor = new Color(0.20f, 0.14f, 0.14f, 1f);
        colors.highlightedColor = new Color(0.45f, 0.16f, 0.20f, 1f);
        colors.pressedColor = new Color(0.62f, 0.20f, 0.26f, 1f);
        colors.selectedColor = colors.normalColor;
        closeButton.colors = colors;
        closeButton.onClick.AddListener(CloseEndings);
        EXODSound.AttachUiSounds(close.gameObject);

        TextMeshProUGUI closeText = CreateText("글자", close.rectTransform, "닫기", 32f, Color.white);
        Stretch(closeText.rectTransform);

        endingsPanel.SetActive(false);
    }

    // 열 때마다 카드를 새로 만듭니다. (기록이 바뀌었을 수 있으므로)
    private void BuildEndingCards()
    {
        Transform old = endingsWindow.Find("내용");
        if (old != null)
            Destroy(old.gameObject);

        RectTransform content = new GameObject("내용", typeof(RectTransform)).GetComponent<RectTransform>();
        content.SetParent(endingsWindow, false);
        Stretch(content);

        EndingCollection.Entry[] entries = EndingCollection.Entries;
        TextMeshProUGUI heading = CreateText("제목", content,
            "엔딩 목록  <size=60%><color=#9A8F93>" + EndingCollection.SeenCount + " / " + entries.Length + "</color></size>",
            52f, TitleColor);
        Place(heading.rectTransform, new Vector2(0.5f, 1f), new Vector2(1000f, 90f), new Vector2(0f, -75f));

        const float cardWidth = 360f;
        const float gap = 30f;
        float startX = -(entries.Length - 1) * (cardWidth + gap) * 0.5f;
        for (int i = 0; i < entries.Length; i++)
            BuildEndingCard(content, entries[i], new Vector2(startX + i * (cardWidth + gap), 15f), new Vector2(cardWidth, 500f));
    }

    private void BuildEndingCard(RectTransform parent, EndingCollection.Entry entry, Vector2 position, Vector2 size)
    {
        bool seen = EndingCollection.IsSeen(entry.Style);
        Color accent = entry.Accent;

        Image card = CreateImage(seen ? entry.Title : "???", parent,
            seen ? new Color(0.13f, 0.08f, 0.10f, 1f) : new Color(0.05f, 0.04f, 0.045f, 1f));
        Place(card.rectTransform, new Vector2(0.5f, 0.5f), size, position);
        Outline outline = card.gameObject.AddComponent<Outline>();
        outline.effectColor = seen ? accent : new Color(0.22f, 0.2f, 0.21f, 1f);
        outline.effectDistance = new Vector2(2f, -2f);

        // 위쪽 색 띠
        Image band = CreateImage("색 띠", card.rectTransform, seen ? accent : new Color(0.16f, 0.15f, 0.16f, 1f));
        band.rectTransform.anchorMin = new Vector2(0f, 1f);
        band.rectTransform.anchorMax = new Vector2(1f, 1f);
        band.rectTransform.pivot = new Vector2(0.5f, 1f);
        band.rectTransform.sizeDelta = new Vector2(0f, 12f);
        band.rectTransform.anchoredPosition = Vector2.zero;

        if (!seen)
        {
            TextMeshProUGUI unknown = CreateText("???", card.rectTransform, "???", 72f, new Color(0.35f, 0.32f, 0.33f, 1f));
            Place(unknown.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(size.x, 120f), new Vector2(0f, 30f));
            TextMeshProUGUI locked = CreateText("아직", card.rectTransform, "아직 보지 못한 엔딩", 26f, new Color(0.32f, 0.29f, 0.30f, 1f));
            Place(locked.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(size.x, 50f), new Vector2(0f, -60f));
            return;
        }

        // 제목: "순응 엔딩 : 따뜻한 감옥" → 두 줄 (큰 글씨 / 부제)
        string main = entry.Title;
        string sub = string.Empty;
        int split = main.IndexOf(" : ");
        if (split >= 0)
        {
            sub = main.Substring(split + 3);
            main = main.Substring(0, split);
        }

        TextMeshProUGUI titleText = CreateText("제목", card.rectTransform, main, 44f, accent);
        Place(titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(size.x - 30f, 70f), new Vector2(0f, -70f));
        if (!string.IsNullOrEmpty(sub))
        {
            TextMeshProUGUI subText = CreateText("부제", card.rectTransform, sub, 32f, Color.white);
            Place(subText.rectTransform, new Vector2(0.5f, 1f), new Vector2(size.x - 30f, 50f), new Vector2(0f, -125f));
        }

        Image line = CreateImage("구분선", card.rectTransform, new Color(accent.r, accent.g, accent.b, 0.5f));
        Place(line.rectTransform, new Vector2(0.5f, 1f), new Vector2(size.x - 80f, 2f), new Vector2(0f, -165f));

        if (!string.IsNullOrEmpty(entry.Subtitle))
        {
            TextMeshProUGUI desc = CreateText("설명", card.rectTransform, entry.Subtitle, 25f, TextColor);
            Place(desc.rectTransform, new Vector2(0.5f, 1f), new Vector2(size.x - 50f, 150f), new Vector2(0f, -250f));
            desc.enableWordWrapping = true;
        }

        if (!string.IsNullOrEmpty(entry.LastLine))
        {
            TextMeshProUGUI quote = CreateText("마지막 대사", card.rectTransform, "“" + entry.LastLine + "”", 26f,
                new Color(0.95f, 0.35f, 0.45f, 1f));
            Place(quote.rectTransform, new Vector2(0.5f, 0f), new Vector2(size.x - 40f, 120f), new Vector2(0f, 80f));
            quote.enableWordWrapping = true;
        }
    }

    private Slider CreateSlider(RectTransform parent, Vector2 position)
    {
        GameObject go = new GameObject("음량 막대", typeof(RectTransform), typeof(Slider));
        go.transform.SetParent(parent, false);
        Place(go.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(380f, 24f), position);

        Image track = CreateImage("바탕", go.transform, new Color(0.18f, 0.13f, 0.14f, 1f));
        Stretch(track.rectTransform);

        RectTransform fillArea = new GameObject("채움 영역", typeof(RectTransform)).GetComponent<RectTransform>();
        fillArea.SetParent(go.transform, false);
        Stretch(fillArea);
        Image fill = CreateImage("채움", fillArea, new Color(0.62f, 0.06f, 0.1f, 1f));
        Stretch(fill.rectTransform);

        RectTransform handleArea = new GameObject("손잡이 영역", typeof(RectTransform)).GetComponent<RectTransform>();
        handleArea.SetParent(go.transform, false);
        Stretch(handleArea);
        Image handle = CreateImage("손잡이", handleArea, TextColor);
        handle.rectTransform.sizeDelta = new Vector2(18f, 44f);

        Slider slider = go.GetComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        return slider;
    }

    // ═══════════════════════════════════════════════════
    //  도우미
    // ═══════════════════════════════════════════════════

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private TextMeshProUGUI CreateText(string name, Transform parent, string value, float size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        if (font != null)
            text.font = font;
        text.text = value;
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    private static void Place(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 position)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetAlpha(Graphic graphic, float alpha)
    {
        Color color = graphic.color;
        color.a = alpha;
        graphic.color = color;
    }

    private static IEnumerator FadeImage(Graphic graphic, float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetAlpha(graphic, Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }

        SetAlpha(graphic, to);
    }
}
