using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 손전등 기믹
/// - 화장실 수건(RRTowel) 뒤에 손전등이 숨겨져 있습니다. 수건을 치우면(획득하면) 보이고, 클릭하면 퀵슬롯에 들어갑니다.
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

    private const string ResourceFolder = "EXODFlashlight/";

    private const string PickupText = "손전등을 얻었다.";
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

    private ItemData flashlightItem;
    private GameObject pickupButton;
    private bool pickedUp;

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
        flashlightItem = Resources.Load<ItemData>(ResourceFolder + "Flashlight");
        if (flashlightItem == null)
            Debug.LogError("[Flashlight] 손전등 아이템을 찾을 수 없습니다: Resources/" + ResourceFolder + "Flashlight");

        QuickSlotInventory inventory = GetInventory();
        pickedUp = inventory != null && FindEntry(inventory, FlashlightItemId) != null;

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

    // 화장실 화면과 퀵슬롯은 실행 중에 만들어지므로 주기적으로 확인합니다.
    private void RefreshRuntimeObjects()
    {
        if (!pickedUp && pickupButton == null)
            CreatePickupButton();

        foreach (QuickSlotSlotView slotView in FindObjectsByType<QuickSlotSlotView>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (slotView.GetComponent<FlashlightSlotClickHandler>() == null)
                slotView.gameObject.AddComponent<FlashlightSlotClickHandler>();
        }
    }

    // ═══════════════════════════════════════════════════
    //  화장실에 놓인 손전등
    // ═══════════════════════════════════════════════════

    private void CreatePickupButton()
    {
        if (flashlightItem == null || flashlightItem.Icon == null)
            return;

        Transform roomView = RestroomGimmick.FindRoomView();
        if (roomView == null)
            return;

        Sprite sprite = flashlightItem.Icon;
        pickupButton = new GameObject(
            "손전등 (수건 뒤)",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        pickupButton.transform.SetParent(roomView, false);

        // 화장실 화면의 자식이라 화장실에 있을 때만 보입니다.
        // 맨 아래에 깔아서 수건(RRTowel)이 있는 동안은 가려져 클릭되지 않습니다.
        pickupButton.transform.SetAsFirstSibling();
        RestroomGimmick.PlaceByPixels(pickupButton.GetComponent<RectTransform>(), RestroomGimmick.FlashlightRect);

        Image image = pickupButton.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;

        Button button = pickupButton.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.92f, 0.92f, 0.92f, 1f);
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        colors.selectedColor = colors.normalColor;
        button.colors = colors;
        button.onClick.AddListener(TryPickUp);
    }

    private void TryPickUp()
    {
        if (pickedUp || IsBusy())
            return;

        QuickSlotInventory inventory = GetInventory();
        if (inventory == null || flashlightItem == null)
        {
            Debug.LogError("[Flashlight] QuickSlotInventory 또는 손전등 아이템이 없습니다.", this);
            return;
        }

        if (!inventory.TryAdd(flashlightItem))
        {
            Think("퀵슬롯이 가득 차서 손전등을 주울 수 없어.");
            return;
        }

        pickedUp = true;
        if (pickupButton != null)
            Destroy(pickupButton);

        EXODSound.Play("item_pickup");
        Think(PickupText);
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
                ShowLightImage(imageName);
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

    private void CloseViewer()
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

        viewerImage = CreateImage("비춘 그림", viewerRoot.transform, Color.white);
        viewerImage.preserveAspect = true;
        viewerImage.raycastTarget = false;
        RectTransform imageRect = viewerImage.rectTransform;
        imageRect.anchorMin = imageRect.anchorMax = new Vector2(0.5f, 0.5f);
        imageRect.sizeDelta = new Vector2(880f, 880f);
        imageRect.anchoredPosition = new Vector2(0f, 40f);

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

    private static QuickSlotEntry FindEntry(QuickSlotInventory inventory, string itemId)
    {
        foreach (QuickSlotEntry entry in inventory.Entries)
        {
            if (entry?.ItemData != null && entry.ItemData.ItemId == itemId)
                return entry;
        }

        return null;
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
