using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 플레이어 기절과 게임 오버를 맡습니다.
/// - Faint(): 화면 암전 → "플레이어가 기절했다." → 하트 1개 감소 → 다시 밝아짐
/// - 하트(HP)가 0이 되면 이유와 상관없이 게임 오버 창을 띄웁니다.
/// - 게임 오버 창: [다시하기] 지금 씬을 처음부터, [로비로 돌아가기] 시작 화면으로
/// UI는 실행 중에 자동으로 만들어집니다.
/// </summary>
[DefaultExecutionOrder(230)]
public sealed class PlayerFaintController : MonoBehaviour
{
    public static PlayerFaintController Instance { get; private set; }

    private const string LobbySceneName = "StartScenes";

    [Header("=== 기절 연출 ===")]
    [SerializeField] private float fadeDuration = 0.8f;   // 암전·밝아짐에 걸리는 시간
    [SerializeField] private float holdDuration = 1.6f;   // 암전 상태로 문구를 보여주는 시간

    private HeartManager hearts;
    private GameObject overlayRoot;
    private Image blackout;
    private TextMeshProUGUI faintText;
    private GameObject gameOverPanel;
    private bool isFainting;
    private bool isGameOver;

    /// <summary>기절 연출 중이거나 게임 오버 상태인지 여부</summary>
    public bool IsBusy => isFainting || isGameOver;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        hearts = FindFirstObjectByType<HeartManager>(FindObjectsInactive.Include);
        if (hearts == null)
            Debug.LogWarning("[PlayerFaint] HeartManager를 찾을 수 없어 HP가 줄지 않습니다.", this);

        BuildUi();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        // 얀 등장처럼 다른 이유로 HP가 0이 되어도 게임 오버를 띄웁니다.
        if (!isGameOver && !isFainting && hearts != null && hearts.HP <= 0)
            ShowGameOver();
    }

    // ═══════════════════════════════════════════════════
    //  기절
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// 기절 연출을 재생합니다. 깨어난 뒤(HP가 남아 있을 때) onWokeUp을 호출합니다.
    /// </summary>
    public void Faint(Action onWokeUp = null)
    {
        if (IsBusy)
            return;

        StartCoroutine(FaintRoutine(onWokeUp));
    }

    private IEnumerator FaintRoutine(Action onWokeUp)
    {
        isFainting = true;
        CloseOtherUi();

        overlayRoot.SetActive(true);
        gameOverPanel.SetActive(false);
        faintText.gameObject.SetActive(true);
        faintText.text = Stage1Dialogues.FaintText;
        SetAlpha(faintText, 0f);

        yield return Fade(blackout, 0f, 1f, fadeDuration);
        yield return Fade(faintText, 0f, 1f, fadeDuration * 0.5f);
        yield return new WaitForSecondsRealtime(holdDuration);

        if (hearts != null)
            hearts.HPM();

        if (hearts != null && hearts.HP <= 0)
        {
            isFainting = false;
            ShowGameOver();
            yield break;
        }

        yield return Fade(faintText, 1f, 0f, fadeDuration * 0.5f);
        yield return Fade(blackout, 1f, 0f, fadeDuration);

        faintText.gameObject.SetActive(false);
        overlayRoot.SetActive(false);
        isFainting = false;

        onWokeUp?.Invoke();
    }

    // ═══════════════════════════════════════════════════
    //  게임 오버
    // ═══════════════════════════════════════════════════

    private void ShowGameOver()
    {
        isGameOver = true;
        StopAllCoroutines();
        CloseOtherUi();

        overlayRoot.SetActive(true);
        faintText.gameObject.SetActive(false);
        SetAlpha(blackout, 1f);
        gameOverPanel.SetActive(true);
    }

    public void Restart()
    {
        ResetRunState();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void BackToLobby()
    {
        ResetRunState();
        SceneManager.LoadScene(LobbySceneName);
    }

    // 씬을 다시 불러와도 남아 있는 진행 상황을 지웁니다.
    private static void ResetRunState()
    {
        InspectionState.ResetAll();

        if (QuickSlotInventory.Instance != null)
            QuickSlotInventory.Instance.Clear();
    }

    private static void CloseOtherUi()
    {
        if (InspectionManager.Instance != null && InspectionManager.Instance.IsOpen)
            InspectionManager.Instance.Close();

        if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueOpen)
            DialogueManager.Instance.ForceCloseDialogue();
    }

    // ═══════════════════════════════════════════════════
    //  UI 만들기
    // ═══════════════════════════════════════════════════

    private void BuildUi()
    {
        overlayRoot = new GameObject(
            "기절·게임오버 화면",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        // 하트 HUD, 퀵슬롯, 대화창보다 위에 그립니다.
        Canvas canvas = overlayRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        CanvasScaler scaler = overlayRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // 전체 화면 검은 막 (뒤쪽 클릭도 막습니다)
        blackout = CreateImage("암전", overlayRoot.transform, Color.black);
        Stretch(blackout.rectTransform);
        SetAlpha(blackout, 0f);

        faintText = CreateText("기절 문구", overlayRoot.transform, Stage1Dialogues.FaintText, 44f,
            new Color(0.85f, 0.85f, 0.85f, 1f));
        Stretch(faintText.rectTransform);

        BuildGameOverPanel();

        faintText.gameObject.SetActive(false);
        overlayRoot.SetActive(false);
    }

    private void BuildGameOverPanel()
    {
        gameOverPanel = new GameObject("게임 오버", typeof(RectTransform));
        gameOverPanel.transform.SetParent(overlayRoot.transform, false);
        Stretch(gameOverPanel.GetComponent<RectTransform>());

        TextMeshProUGUI title = CreateText("게임 오버 제목", gameOverPanel.transform, "GAME OVER", 120f,
            new Color(0.72f, 0.08f, 0.12f, 1f));
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.sizeDelta = new Vector2(1200f, 180f);
        titleRect.anchoredPosition = new Vector2(0f, 110f);

        CreateButton("다시하기", new Vector2(-190f, -110f), Restart);
        CreateButton("로비로 돌아가기", new Vector2(190f, -110f), BackToLobby);

        gameOverPanel.SetActive(false);
    }

    private void CreateButton(string label, Vector2 position, UnityEngine.Events.UnityAction onClick)
    {
        Image image = CreateImage(label, gameOverPanel.transform, Color.white);
        RectTransform rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(340f, 84f);
        rect.anchoredPosition = position;

        Button button = image.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.20f, 0.14f, 0.14f, 1f);
        colors.highlightedColor = new Color(0.45f, 0.16f, 0.20f, 1f);
        colors.pressedColor = new Color(0.62f, 0.20f, 0.26f, 1f);
        colors.selectedColor = colors.normalColor;
        button.colors = colors;
        button.onClick.AddListener(onClick);

        TextMeshProUGUI text = CreateText("글자", image.transform, label, 34f, Color.white);
        Stretch(text.rectTransform);
        text.raycastTarget = false;
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
        text.text = value;
        text.fontSize = size;
        text.fontStyle = FontStyles.Normal;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.raycastTarget = false;

        // 방 이름 글자와 같은 폰트(Galmuri9)를 사용합니다.
        TextMeshProUGUI roomText = SliderManager.Instance != null ? SliderManager.Instance.roomNameText : null;
        if (roomText != null && roomText.font != null)
            text.font = roomText.font;

        return text;
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

    private static IEnumerator Fade(Graphic graphic, float from, float to, float duration)
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

    // Play 중 Inspector의 ⋮ 메뉴에서 실행해 기절 연출을 확인합니다.
    [ContextMenu("기절 테스트")]
    private void TestFaint()
    {
        Faint();
    }
}
