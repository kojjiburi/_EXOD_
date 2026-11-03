using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 손전등 기믹
/// - 화장실 수건(RRTowel) 뒤에 손전등이 숨겨져 있습니다. (놓기·줍기는 RestroomGimmick의 선반이 담당)
/// - 퀵슬롯의 손전등을 클릭 → "비출 물건을 선택하자." → [다음]을 누른 뒤 퀵슬롯 아이템을 하나 클릭합니다.
/// - 종이 A~E를 비추면 Alight~Elight 그림을 화면에 보여 주고 [나가기]로 닫습니다. (아이템으로 얻지 않음)
/// - 다른 아이템을 비추면 "이건 이렇게 사용하는 것이 아닌 것 같다." 대사가 나옵니다.
/// - 고르는 중에 퀵슬롯 우클릭 또는 ESC를 누르면 취소됩니다.
/// 그림과 아이템 데이터는 Assets/EXOD/Flashlight/Resources/EXODFlashlight 에 있습니다.
/// 실행할 때 SliderManager가 있는 씬에 자동으로 붙습니다.
/// </summary>
[DefaultExecutionOrder(210)]
public sealed class FlashlightGimmick : MonoBehaviour
{
    public static FlashlightGimmick Instance { get; private set; }

    public const string FlashlightItemId = "flashlight";

    /// <summary>손전등으로 종이를 비췄을 때 (종이 아이템 ID). 이지 모드 행동 요령 힌트에서 씀 (EasyHelpTips)</summary>
    public static event System.Action<string> PaperLit;

    private const string ResourceFolder = "EXODFlashlight/";

    private const string SelectTargetText = "비출 물건을 선택하자.";
    private const string WrongTargetText = "이건 이렇게 사용하는 것이 아닌 것 같다.";

    // 종이 아이템 ID → 비췄을 때 보여 줄 그림 (Resources/EXODFlashlight/...)
    private static readonly Dictionary<string, string> LightImageByPaperId = new()
    {
        { "paper_a", "Alight" },
        { "paper_b", "Blight" },
        { "paper_c", "Clight" },
        { "paper_d", "Dlight" },
        { "paper_e", "Elight" },
    };


    private bool waitingForSelectDialogue;
    private bool isSelectingTarget;

    private GameObject viewerRoot;
    private Image viewerImage;

    private float nextRefreshTime;

    public bool IsSelectingTarget => isSelectingTarget;
    public bool IsViewerOpen => viewerRoot != null && viewerRoot.activeSelf;

    // 시작 화면을 거쳐 들어오거나 [다시하기]로 씬을 다시 불러와도 동작하도록 씬이 열릴 때마다 확인합니다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHandler()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (FindFirstObjectByType<FlashlightGimmick>() != null)
            return;

        SliderManager slider = FindFirstObjectByType<SliderManager>();
        if (slider != null)
            slider.gameObject.AddComponent<FlashlightGimmick>();
    }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        StopListeningDialogueEnd();

        if (viewerRoot != null)
            Destroy(viewerRoot);

        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        CreateViewer();
        RefreshRuntimeObjects();
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextRefreshTime)
        {
            nextRefreshTime = Time.unscaledTime + 0.5f;
            RefreshRuntimeObjects();
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (IsViewerOpen)
                CloseViewer();
            else
                CancelTargetSelection();
        }
    }

    // 퀵슬롯은 실행 중에 만들어지므로 주기적으로 확인합니다.
    private void RefreshRuntimeObjects()
    {
        foreach (QuickSlotSlotView slotView in FindObjectsByType<QuickSlotSlotView>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (slotView.GetComponent<FlashlightSlotClickHandler>() == null)
                slotView.gameObject.AddComponent<FlashlightSlotClickHandler>();
        }
    }

    // ═══════════════════════════════════════════════════
    //  퀵슬롯 클릭
    // ═══════════════════════════════════════════════════

    public void OnQuickSlotClicked(QuickSlotEntry entry)
    {
        if (entry == null || entry.ItemData == null || IsViewerOpen || IsBusy())
            return;

        string itemId = entry.ItemData.ItemId;

        if (isSelectingTarget)
        {
            isSelectingTarget = false;

            if (LightImageByPaperId.TryGetValue(itemId, out string imageName))
            {
                // [이지] 종이 A만 나오고, 비추면 A~E 그림을 합친 한 장을 한 번에 보여 줌 (GameDifficulty, 2026-11-03)
                if (GameDifficulty.IsEasy && itemId == "paper_a")
                    imageName = "ABCDElight";
                ShowLightImage(imageName);
                PaperLit?.Invoke(itemId);
            }
            else
                Think(WrongTargetText);

            return;
        }

        if (itemId == FlashlightItemId)
            BeginTargetSelection();
    }

    public void CancelTargetSelection()
    {
        isSelectingTarget = false;
    }

    private void BeginTargetSelection()
    {
        DialogueManager dialogue = DialogueManager.Instance;
        if (dialogue == null)
        {
            isSelectingTarget = true;
            return;
        }

        // 대사를 [다음]으로 넘겨 닫은 뒤부터 비출 물건을 고를 수 있습니다.
        StopListeningDialogueEnd();
        dialogue.StartDialogue(Stage1Dialogues.Thought(SelectTargetText));
        dialogue.onDialogueEnd.AddListener(OnSelectDialogueEnd);
        waitingForSelectDialogue = true;
    }

    private void OnSelectDialogueEnd()
    {
        StopListeningDialogueEnd();
        isSelectingTarget = true;
    }

    private void StopListeningDialogueEnd()
    {
        if (!waitingForSelectDialogue)
            return;

        waitingForSelectDialogue = false;
        if (DialogueManager.Instance != null)
            DialogueManager.Instance.onDialogueEnd.RemoveListener(OnSelectDialogueEnd);
    }

    public static QuickSlotEntry GetEntryAtSlot(int slotIndex)
    {
        QuickSlotInventory inventory = GetInventory();
        if (inventory == null || slotIndex < 0)
            return null;

        // QuickSlotController.Refresh와 같은 순서(얻은 순서)로 칸에 표시됩니다.
        return inventory.Entries
            .Where(entry => entry != null)
            .OrderBy(entry => entry.AcquiredSequence)
            .ElementAtOrDefault(slotIndex);
    }

    // ═══════════════════════════════════════════════════
    //  비춘 그림 보기 화면 (아이템으로 얻지 않음)
    // ═══════════════════════════════════════════════════

    private void ShowLightImage(string imageName)
    {
        Sprite sprite = Resources.Load<Sprite>(ResourceFolder + imageName);
        if (sprite == null)
        {
            Debug.LogError("[Flashlight] 그림을 찾을 수 없습니다: Resources/" + ResourceFolder + imageName);
            return;
        }

        if (viewerRoot == null)
            CreateViewer();

        viewerImage.sprite = sprite;
        viewerRoot.SetActive(true);
        EXODSound.Play("ui_click");
    }

    // 여자친구 등장 등으로 강제로 닫을 때도 씀 (GimmickInterrupt)
    public void CloseViewer()
    {
        if (viewerRoot != null)
            viewerRoot.SetActive(false);
    }

    private void CreateViewer()
    {
        if (viewerRoot != null)
            return;

        viewerRoot = new GameObject(
            "손전등 비춘 화면",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        // 퀵슬롯·대화창보다 위, 기절·게임 오버 화면(1000)보다는 아래에 그립니다.
        Canvas canvas = viewerRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;

        CanvasScaler scaler = viewerRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // 뒤쪽 화면 클릭을 막는 어두운 배경
        Image backdrop = CreateImage("어두운 배경", viewerRoot.transform, new Color(0f, 0f, 0f, 0.85f));
        Stretch(backdrop.rectTransform);

        // 비춘 그림 뒤에 까는 회색 정사각형 (화면 가운데)
        Image square = CreateImage("회색 바탕", viewerRoot.transform, new Color(0.5f, 0.5f, 0.5f, 1f));
        square.raycastTarget = false;
        RectTransform squareRect = square.rectTransform;
        squareRect.anchorMin = squareRect.anchorMax = new Vector2(0.5f, 0.5f);
        squareRect.sizeDelta = new Vector2(840f, 840f);
        squareRect.anchoredPosition = Vector2.zero;

        viewerImage = CreateImage("비춘 그림", viewerRoot.transform, Color.white);
        viewerImage.preserveAspect = true;
        viewerImage.raycastTarget = false;
        RectTransform imageRect = viewerImage.rectTransform;
        imageRect.anchorMin = imageRect.anchorMax = new Vector2(0.5f, 0.5f);
        imageRect.sizeDelta = new Vector2(840f, 840f);
        imageRect.anchoredPosition = Vector2.zero;

        Image buttonImage = CreateImage("나가기", viewerRoot.transform, Color.white);
        RectTransform buttonRect = buttonImage.rectTransform;
        buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0.5f, 0f);
        buttonRect.pivot = new Vector2(0.5f, 0f);
        buttonRect.sizeDelta = new Vector2(260f, 80f);
        buttonRect.anchoredPosition = new Vector2(0f, 40f);

        Button button = buttonImage.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.20f, 0.14f, 0.14f, 1f);
        colors.highlightedColor = new Color(0.45f, 0.16f, 0.20f, 1f);
        colors.pressedColor = new Color(0.62f, 0.20f, 0.26f, 1f);
        colors.selectedColor = colors.normalColor;
        button.colors = colors;
        button.onClick.AddListener(CloseViewer);
        EXODSound.AttachUiSounds(buttonImage.gameObject);

        TextMeshProUGUI label = CreateText("글자", buttonImage.transform, "나가기", 34f);
        Stretch(label.rectTransform);

        viewerRoot.SetActive(false);
    }

    // ═══════════════════════════════════════════════════
    //  도우미
    // ═══════════════════════════════════════════════════

    private static bool IsBusy()
    {
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueOpen)
            return true;

        if (InspectionManager.Instance != null && InspectionManager.Instance.IsOpen)
            return true;

        return PlayerFaintController.Instance != null && PlayerFaintController.Instance.IsBusy;
    }

    private static void Think(string text)
    {
        DialogueManager.Instance?.StartDialogue(Stage1Dialogues.Thought(text));
    }

    private static QuickSlotInventory GetInventory()
    {
        return QuickSlotInventory.Instance != null
            ? QuickSlotInventory.Instance
            : FindFirstObjectByType<QuickSlotInventory>();
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string value, float size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.fontStyle = FontStyles.Normal;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
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
}
