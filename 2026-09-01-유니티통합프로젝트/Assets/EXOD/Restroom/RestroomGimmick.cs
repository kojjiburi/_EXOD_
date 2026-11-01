using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 화장실 기믹
/// - 화장실 화면에 수건(RRTowel), 금고(RRSafe), 금고 잠금판(RRSafe_block), 변기 버튼(RRButton)을 놓습니다.
///   위치는 1920x1080 원본 그림 기준 픽셀 좌표(아래 Rect 상수)이며 배경과 같은 비율로 늘어납니다.
/// - 수건은 아이템(towel)입니다. 클릭하면 퀵슬롯에 들어가고, 뒤에 숨겨져 있던 손전등이 드러납니다.
/// - 금고 잠금판을 클릭하면 키패드(RRPass = PassBG + Pass1~9)가 뜹니다.
///   · 숫자를 누르면 그 숫자가 저장되고, 눌린 버튼은 어두워져서 다시 누를 수 없습니다.
///   · [닫기]로 닫았다가 다시 열면 모두 초기화됩니다.
///   · 7 4 1 5 2 3 8 9 6 순서로 누르면 금고 잠금판이 사라집니다.
/// 그림과 아이템 데이터는 Assets/EXOD/Restroom/Resources/EXODRestroom 에 있습니다.
/// 실행할 때 SliderManager가 있는 씬에 자동으로 붙습니다.
/// </summary>
[DefaultExecutionOrder(205)]
public sealed class RestroomGimmick : MonoBehaviour
{
    public static RestroomGimmick Instance { get; private set; }

    public const string TowelItemId = "towel";
    public const string RoomViewName = "화장실 배경 화면"; // SliderManager가 만드는 화장실 화면

    private const string ResourceFolder = "EXODRestroom/";
    private const float ReferenceWidth = 1920f;
    private const float ReferenceHeight = 1080f;

    // 원본 그림(1920x1080)에서의 위치: x0, y0(위), x1, y1(아래)
    private static readonly RectInt TowelRect = FromCorners(526, 0, 785, 72);
    private static readonly RectInt SafeRect = FromCorners(1318, 5, 1518, 184);
    private static readonly RectInt SafeBlockRect = FromCorners(1364, 25, 1518, 183);
    private static readonly RectInt ToiletButtonRect = FromCorners(490, 333, 534, 363);

    // 손전등은 수건 뒤(같은 자리)에 숨겨 둡니다.
    public static readonly RectInt FlashlightRect = FromCorners(596, 6, 716, 66);

    private static readonly RectInt PassBgRect = FromCorners(534, 121, 1385, 973);
    private static readonly RectInt[] PassRects =
    {
        FromCorners(579, 162, 828, 413),   // 1
        FromCorners(825, 163, 1092, 412),  // 2
        FromCorners(1088, 162, 1343, 410), // 3
        FromCorners(579, 411, 829, 671),   // 4
        FromCorners(825, 410, 1093, 672),  // 5
        FromCorners(1089, 408, 1343, 673), // 6
        FromCorners(578, 667, 829, 926),   // 7
        FromCorners(826, 668, 1094, 926),  // 8
        FromCorners(1090, 655, 1344, 926), // 9
    };
    private static readonly RectInt CloseButtonRect = FromCorners(1430, 860, 1690, 940);

    // 정답 순서
    private static readonly int[] Answer = { 7, 4, 1, 5, 2, 3, 8, 9, 6 };

    private const string TowelPickupText = "수건을 얻었다.";

    private ItemData towelItem;
    private bool towelTaken;
    private bool safeUnlocked;
    private bool roomObjectsCreated;

    private GameObject towelButton;
    private GameObject safeBlockButton;

    private GameObject keypadRoot; // RRPass
    private readonly List<RestroomKeypadButton> keypadButtons = new();
    private readonly List<int> enteredNumbers = new();

    private float nextRefreshTime;

    public bool IsKeypadOpen => keypadRoot != null && keypadRoot.activeSelf;
    public bool IsSafeUnlocked => safeUnlocked;
    public IReadOnlyList<int> EnteredNumbers => enteredNumbers;

    // 시작 화면을 거쳐 들어오거나 [다시하기]로 씬을 다시 불러와도 동작하도록 씬이 열릴 때마다 확인합니다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHandler()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (FindFirstObjectByType<RestroomGimmick>() != null)
            return;

        SliderManager slider = FindFirstObjectByType<SliderManager>();
        if (slider != null)
            slider.gameObject.AddComponent<RestroomGimmick>();
    }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (keypadRoot != null)
            Destroy(keypadRoot);

        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        towelItem = Resources.Load<ItemData>(ResourceFolder + "Towel");
        if (towelItem == null)
            Debug.LogError("[Restroom] 수건 아이템을 찾을 수 없습니다: Resources/" + ResourceFolder + "Towel");

        QuickSlotInventory inventory = GetInventory();
        towelTaken = inventory != null && HasItem(inventory, TowelItemId);

        CreateKeypad();
        TryCreateRoomObjects();
    }

    private void Update()
    {
        if (!roomObjectsCreated && Time.unscaledTime >= nextRefreshTime)
        {
            nextRefreshTime = Time.unscaledTime + 0.5f;
            TryCreateRoomObjects();
        }

        if (IsKeypadOpen && Input.GetKeyDown(KeyCode.Escape))
            CloseKeypad();
    }

    // ═══════════════════════════════════════════════════
    //  화장실 안 오브젝트
    // ═══════════════════════════════════════════════════

    private void TryCreateRoomObjects()
    {
        Transform roomView = FindRoomView();
        if (roomView == null)
            return;

        roomObjectsCreated = true;

        // 아래에 만든 것이 위에 그려집니다. (손전등은 FlashlightGimmick이 맨 아래에 깝니다)
        CreateRoomImage(roomView, "RRButton", ToiletButtonRect, null);
        CreateRoomImage(roomView, "RRSafe", SafeRect, null);

        if (!safeUnlocked)
            safeBlockButton = CreateRoomImage(roomView, "RRSafe_block", SafeBlockRect, OpenKeypad);

        if (!towelTaken)
            towelButton = CreateRoomImage(roomView, "RRTowel", TowelRect, TakeTowel);
    }

    private GameObject CreateRoomImage(
        Transform parent,
        string spriteName,
        RectInt pixelRect,
        UnityEngine.Events.UnityAction onClick)
    {
        Sprite sprite = LoadSprite(spriteName);
        if (sprite == null)
            return null;

        GameObject go = new GameObject(spriteName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        PlaceByPixels(go.GetComponent<RectTransform>(), pixelRect);

        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = false;
        image.raycastTarget = onClick != null;

        if (onClick != null)
        {
            Button button = go.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 0.9f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.selectedColor = colors.normalColor;
            button.colors = colors;
            button.onClick.AddListener(onClick);
        }

        return go;
    }

    private void TakeTowel()
    {
        if (towelTaken || IsBusy())
            return;

        QuickSlotInventory inventory = GetInventory();
        if (inventory == null || towelItem == null)
        {
            Debug.LogError("[Restroom] QuickSlotInventory 또는 수건 아이템이 없습니다.", this);
            return;
        }

        if (!inventory.TryAdd(towelItem))
        {
            Think("퀵슬롯이 가득 차서 수건을 집을 수 없어.");
            return;
        }

        towelTaken = true;
        if (towelButton != null)
            Destroy(towelButton);

        EXODSound.Play("item_pickup");
        Think(TowelPickupText);
    }

    // ═══════════════════════════════════════════════════
    //  금고 키패드 (RRPass)
    // ═══════════════════════════════════════════════════

    public void OpenKeypad()
    {
        if (safeUnlocked || IsBusy())
            return;

        if (keypadRoot == null)
            CreateKeypad();

        ResetKeypad();
        keypadRoot.SetActive(true);
        EXODSound.Play("ui_click");
    }

    public void CloseKeypad()
    {
        if (keypadRoot != null)
            keypadRoot.SetActive(false);

        // 닫으면 입력했던 숫자는 지워지고, 다시 열면 모든 버튼을 다시 누를 수 있습니다.
        ResetKeypad();
    }

    private void ResetKeypad()
    {
        enteredNumbers.Clear();
        foreach (RestroomKeypadButton button in keypadButtons)
            button.ResetState();
    }

    internal void OnKeypadPressed(int number)
    {
        enteredNumbers.Add(number);
        EXODSound.Play("keypad_press");

        if (enteredNumbers.Count < Answer.Length)
            return;

        if (IsCorrect())
            UnlockSafe();
        else
            EXODSound.Play("keypad_error");
    }

    private bool IsCorrect()
    {
        if (enteredNumbers.Count != Answer.Length)
            return false;

        for (int i = 0; i < Answer.Length; i++)
        {
            if (enteredNumbers[i] != Answer[i])
                return false;
        }

        return true;
    }

    private void UnlockSafe()
    {
        safeUnlocked = true;
        CloseKeypad();

        if (safeBlockButton != null)
            Destroy(safeBlockButton);

        EXODSound.Play("key_unlock");
        Debug.Log("[Restroom] 금고 비밀번호 정답! RRSafe_block을 지웠습니다.", this);
    }

    private void CreateKeypad()
    {
        if (keypadRoot != null)
            return;

        keypadRoot = new GameObject(
            "RRPass (금고 키패드)",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        // 퀵슬롯·대화창보다 위, 기절·게임 오버 화면(1000)보다는 아래에 그립니다.
        Canvas canvas = keypadRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;

        CanvasScaler scaler = keypadRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
        scaler.matchWidthOrHeight = 0.5f;

        // 뒤쪽 화면 클릭을 막는 어두운 배경
        Image backdrop = CreateImage("어두운 배경", keypadRoot.transform, new Color(0f, 0f, 0f, 0.6f));
        RectTransform backdropRect = backdrop.rectTransform;
        backdropRect.anchorMin = Vector2.zero;
        backdropRect.anchorMax = Vector2.one;
        backdropRect.offsetMin = Vector2.zero;
        backdropRect.offsetMax = Vector2.zero;

        // 원본 그림과 같은 1920x1080 판 위에 픽셀 좌표 그대로 배치합니다.
        GameObject board = new GameObject("키패드 판", typeof(RectTransform));
        board.transform.SetParent(keypadRoot.transform, false);
        RectTransform boardRect = board.GetComponent<RectTransform>();
        boardRect.anchorMin = boardRect.anchorMax = new Vector2(0.5f, 0.5f);
        boardRect.sizeDelta = new Vector2(ReferenceWidth, ReferenceHeight);

        Image passBg = CreateImage("PassBG", board.transform, Color.white);
        passBg.sprite = LoadSprite("PassBG");
        passBg.raycastTarget = true;
        PlaceByPixels(passBg.rectTransform, PassBgRect);

        keypadButtons.Clear();
        for (int number = 1; number <= 9; number++)
        {
            Image key = CreateImage("Pass" + number, board.transform, Color.white);
            key.sprite = LoadSprite("Pass" + number);
            key.raycastTarget = true;
            PlaceByPixels(key.rectTransform, PassRects[number - 1]);

            RestroomKeypadButton keyButton = key.gameObject.AddComponent<RestroomKeypadButton>();
            keyButton.Setup(this, number);
            keypadButtons.Add(keyButton);
        }

        Image closeImage = CreateImage("닫기", board.transform, Color.white);
        PlaceByPixels(closeImage.rectTransform, CloseButtonRect);
        Button closeButton = closeImage.gameObject.AddComponent<Button>();
        ColorBlock colors = closeButton.colors;
        colors.normalColor = new Color(0.20f, 0.14f, 0.14f, 1f);
        colors.highlightedColor = new Color(0.45f, 0.16f, 0.20f, 1f);
        colors.pressedColor = new Color(0.62f, 0.20f, 0.26f, 1f);
        colors.selectedColor = colors.normalColor;
        closeButton.colors = colors;
        closeButton.onClick.AddListener(CloseKeypad);
        EXODSound.AttachUiSounds(closeImage.gameObject);

        TextMeshProUGUI label = CreateText("글자", closeImage.transform, "닫기", 34f);
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        keypadRoot.SetActive(false);
    }

    // ═══════════════════════════════════════════════════
    //  도우미
    // ═══════════════════════════════════════════════════

    private static RectInt FromCorners(int x0, int y0, int x1, int y1)
    {
        return new RectInt(x0, y0, x1 - x0, y1 - y0);
    }

    /// <summary>1920x1080 그림 기준 픽셀 영역(위쪽이 y=0)에 맞게 부모 안에서 비율로 배치합니다.</summary>
    public static void PlaceByPixels(RectTransform rect, RectInt pixelRect)
    {
        rect.anchorMin = new Vector2(pixelRect.xMin / ReferenceWidth, 1f - pixelRect.yMax / ReferenceHeight);
        rect.anchorMax = new Vector2(pixelRect.xMax / ReferenceWidth, 1f - pixelRect.yMin / ReferenceHeight);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    public static Transform FindRoomView()
    {
        foreach (RectTransform rect in FindObjectsByType<RectTransform>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (rect.name == RoomViewName)
                return rect;
        }

        return null;
    }

    private bool IsBusy()
    {
        if (IsKeypadOpen)
            return true;

        if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueOpen)
            return true;

        if (InspectionManager.Instance != null && InspectionManager.Instance.IsOpen)
            return true;

        if (FlashlightGimmick.Instance != null && FlashlightGimmick.Instance.IsViewerOpen)
            return true;

        return PlayerFaintController.Instance != null && PlayerFaintController.Instance.IsBusy;
    }

    private static Sprite LoadSprite(string name)
    {
        Sprite sprite = Resources.Load<Sprite>(ResourceFolder + name);
        if (sprite == null)
            Debug.LogError("[Restroom] 그림을 찾을 수 없습니다: Resources/" + ResourceFolder + name);
        return sprite;
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

    private static bool HasItem(QuickSlotInventory inventory, string itemId)
    {
        foreach (QuickSlotEntry entry in inventory.Entries)
        {
            if (entry?.ItemData != null && entry.ItemData.ItemId == itemId)
                return true;
        }

        return false;
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
}
