using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 화장실 기믹
/// - 화장실 화면에 수건(RRTowel), 금고(RRSafe), 금고 잠금판(RRSafe_block), 변기 버튼(RRButton)을 놓습니다.
///   위치는 1920x1080 원본 그림 기준 픽셀 좌표(아래 Rect 상수)이며 배경과 같은 비율로 늘어납니다.
/// - 수건 자리(선반): 처음에는 손전등 위에 수건이 덮여 있습니다. (아래 → 위: 손전등, 수건)
///   · 맨 위 물건만 클릭해서 주울 수 있습니다. (수건을 치워야 손전등이 보임)
///   · 퀵슬롯에서 아무 아이템이나 끌어다 놓으면 놓은 순서대로 쌓입니다. (막지 않고, 대사도 없음)
///   · "손전등 위에 수건" 상태만 제자리입니다. 이 두 자리는 ItemPositionManager에 등록되어
///     일차 종료 제자리 검사에서 틀리면 하트가 줄어듭니다.
///   · 일차 종료 검사에서 틀린 게 있으면 선반은 처음 상태(손전등 위에 수건)로 돌아가고, 퀵슬롯의 손전등·수건은 빠집니다.
///     선반에 올려 둔 다른 물건은 처음 자리가 있으면 그리로, 없으면 퀵슬롯으로 돌아갑니다. (종이는 초기화)
/// - 금고 잠금판을 클릭하면 키패드(RRPass = PassBG + Pass1~9)가 뜹니다.
///   · 숫자를 누르면 그 숫자가 저장되고, 눌린 버튼은 어두워져서 다시 누를 수 없습니다.
///   · [닫기]로 닫았다가 다시 열면 모두 초기화됩니다.
///   · 7 4 1 5 2 3 8 9 6 순서로 누르면 금고 잠금판이 사라지고, 모든 종이(paper_)가 영구히 없어집니다.
/// - 금고가 열리면 안에 청혼반지(ProposalRing)가 보이고, 누르면 퀵슬롯에 들어간 뒤 청혼 엔딩(ProposalEnding.Trigger)을 부릅니다.
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
    private const string FlashlightResourcePath = "EXODFlashlight/Flashlight";
    private const string RestroomRoomName = "화장실";
    private const float ReferenceWidth = 1920f;
    private const float ReferenceHeight = 1080f;

    // 원본 그림(1920x1080)에서의 위치: x0, y0(위), x1, y1(아래)
    private static readonly RectInt TowelRect = FromCorners(526, 0, 785, 72);
    private static readonly RectInt SafeRect = FromCorners(1318, 5, 1518, 184);
    private static readonly RectInt SafeBlockRect = FromCorners(1364, 25, 1518, 183);
    // 금고 안 청혼반지 (잠금판이 있던 자리 가운데)
    // 반지는 금고 바닥의 반지함(RingBox) 쿠션 틈에 꽂혀 있음 (2026-11-02: 반지만 떠 있어 보이던 것 수정)
    private static readonly RectInt RingBoxRect = FromCorners(1393, 92, 1489, 164);
    private static readonly RectInt RingRect = FromCorners(1414, 94, 1468, 148);
    private static readonly RectInt ToiletButtonRect = FromCorners(490, 333, 534, 363);

    // 손전등과 그 밖의 물건은 수건 자리 가운데에 이 크기로 놓입니다.
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
    private const string FlashlightPickupText = "손전등을 얻었다.";

    // ── 수건 자리(선반)에 쌓인 물건 (0번이 맨 아래) ──
    private sealed class ShelfItem
    {
        public ItemData Item;
        public GameObject View;
    }

    private readonly List<ShelfItem> shelf = new();

    // 제자리 검사용 자리 (ItemPositionManager에 등록됨)
    // 아래 자리: 손전등이 있어야 함 / 위 자리: 수건이 있어야 함
    private sealed class CheckSlot
    {
        public string ExpectedItemId;
        public ItemData ExpectedItem;
        public string SlotId;
        public WorldItemPickup Occupant; // 자리를 채우는 보이지 않는 오브젝트
    }

    private CheckSlot bottomSlot;
    private CheckSlot topSlot;
    private bool checkSlotsReady;
    private Vector3 shelfWorldPosition;
    private ItemPositionManager positionManager;
    private GameObject dropTarget;
    private Transform roomViewTransform;

    private ItemData towelItem;
    private ItemData flashlightItem;
    private bool safeUnlocked;
    private bool roomObjectsCreated;

    private GameObject safeBlockButton;
    private GameObject ringButton;
    private ItemData ringItem;
    private bool ringTaken;

    private GameObject keypadRoot; // RRPass
    private readonly List<RestroomKeypadButton> keypadButtons = new();
    private readonly List<int> enteredNumbers = new();

    private float nextRefreshTime;

    public bool IsKeypadOpen => keypadRoot != null && keypadRoot.activeSelf;
    public bool IsSafeUnlocked => safeUnlocked;
    public IReadOnlyList<int> EnteredNumbers => enteredNumbers;

    /// <summary>수건 자리가 "손전등 위에 수건"인 제자리 상태인지</summary>
    public bool IsShelfCorrect =>
        shelf.Count == 2 &&
        shelf[0].Item != null && shelf[0].Item.ItemId == FlashlightGimmick.FlashlightItemId &&
        shelf[1].Item != null && shelf[1].Item.ItemId == TowelItemId;

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

        if (dropTarget != null)
            Destroy(dropTarget);

        if (positionManager != null)
        {
            positionManager.ItemsResetForNewDay -= ResetShelfForNewDay;
            positionManager.PapersRemovedPermanently -= RemovePapersFromShelf;
        }

        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        towelItem = Resources.Load<ItemData>(ResourceFolder + "Towel");
        flashlightItem = Resources.Load<ItemData>(FlashlightResourcePath);
        ringItem = Resources.Load<ItemData>(ResourceFolder + "ProposalRing");
        if (towelItem == null || flashlightItem == null)
            Debug.LogError("[Restroom] 수건 또는 손전등 아이템을 찾을 수 없습니다.");

        // 처음 상태: 손전등 위에 수건 (이미 퀵슬롯에 있는 것은 빼고)
        QuickSlotInventory inventory = GetInventory();
        if (flashlightItem != null && (inventory == null || !HasItem(inventory, flashlightItem.ItemId)))
            shelf.Add(new ShelfItem { Item = flashlightItem });
        if (towelItem != null && (inventory == null || !HasItem(inventory, TowelItemId)))
            shelf.Add(new ShelfItem { Item = towelItem });

        CreateKeypad();
        TryCreateRoomObjects();
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextRefreshTime)
        {
            nextRefreshTime = Time.unscaledTime + 0.5f;
            if (!roomObjectsCreated)
                TryCreateRoomObjects();
            if (roomObjectsCreated && !checkSlotsReady)
                TrySetupCheckSlots();
        }

        if (checkSlotsReady)
        {
            // 화장실에 있을 때만 수건 자리에 놓을 수 있습니다.
            bool inRestroom = roomViewTransform != null && roomViewTransform.gameObject.activeInHierarchy;
            if (dropTarget != null && dropTarget.activeSelf != inRestroom)
                dropTarget.SetActive(inRestroom);

            ReturnForeignPlacements();
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
        roomViewTransform = roomView;

        CreateRoomImage(roomView, "RRButton", LoadSprite("RRButton"), ToiletButtonRect, null, false);
        CreateRoomImage(roomView, "RRSafe", LoadSprite("RRSafe"), SafeRect, null, false);

        if (!safeUnlocked)
            safeBlockButton = CreateRoomImage(roomView, "RRSafe_block", LoadSprite("RRSafe_block"), SafeBlockRect, OpenKeypad, false);
        else
            CreateRingButton();

        RebuildShelfViews();
    }

    private GameObject CreateRoomImage(
        Transform parent,
        string objectName,
        Sprite sprite,
        RectInt pixelRect,
        UnityEngine.Events.UnityAction onClick,
        bool preserveAspect)
    {
        if (sprite == null)
            return null;

        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        PlaceByPixels(go.GetComponent<RectTransform>(), pixelRect);

        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = preserveAspect;
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

    // ═══════════════════════════════════════════════════
    //  수건 자리(선반): 쌓기 / 줍기 / 제자리 판정
    // ═══════════════════════════════════════════════════

    // 쌓인 순서대로 그림을 다시 만듭니다. 맨 위 물건만 클릭할 수 있습니다.
    private void RebuildShelfViews()
    {
        foreach (ShelfItem entry in shelf)
        {
            if (entry.View != null)
                Destroy(entry.View);
            entry.View = null;
        }

        if (roomViewTransform == null)
            return;

        for (int i = 0; i < shelf.Count; i++)
        {
            ShelfItem entry = shelf[i];
            bool isTop = i == shelf.Count - 1;
            bool isTowel = entry.Item.ItemId == TowelItemId;

            Sprite sprite = isTowel ? LoadSprite("RRTowel") : entry.Item.Icon;
            RectInt rect = isTowel ? TowelRect : FlashlightRect;
            entry.View = CreateRoomImage(
                roomViewTransform,
                "선반_" + entry.Item.ItemId,
                sprite,
                rect,
                isTop ? TakeTopShelfItem : null,
                !isTowel);
        }

        // 대화창·슬라이더 등 다른 UI보다 위로 올라가지 않도록, 금고 등 방 물건 바로 뒤에 둡니다.
        // (방 화면 자식 안에서만 순서가 바뀌므로 다른 UI에는 영향 없음)
    }

    private void TakeTopShelfItem()
    {
        if (shelf.Count == 0 || IsBusy())
            return;

        ShelfItem top = shelf[shelf.Count - 1];
        QuickSlotInventory inventory = GetInventory();
        if (inventory == null || top.Item == null)
            return;

        if (!inventory.TryAdd(top.Item))
        {
            Think("퀵슬롯이 가득 차서 주울 수 없어.");
            return;
        }

        if (top.View != null)
            Destroy(top.View);
        shelf.RemoveAt(shelf.Count - 1);
        RebuildShelfViews();
        SyncCheckSlots();

        EXODSound.Play("item_pickup");
        if (top.Item.ItemId == TowelItemId)
            Think(TowelPickupText);
        else if (top.Item.ItemId == FlashlightGimmick.FlashlightItemId)
            Think(FlashlightPickupText);
    }

    /// <summary>수건 자리 위에 아이템을 놓습니다. 화장실에서는 어떤 아이템이든 순서대로 쌓입니다.</summary>
    private bool TryPlaceOnShelf(ItemData item)
    {
        bool inRestroom = SliderManager.Instance != null && SliderManager.Instance.CurrentRoom == RestroomRoomName;
        if (!inRestroom || item == null)
            return false;

        shelf.Add(new ShelfItem { Item = item });
        RebuildShelfViews();
        SyncCheckSlots();
        EXODSound.Play("item_pickup");
        return true;
    }

    private void TrySetupCheckSlots()
    {
        positionManager = ItemPositionManager.Instance != null
            ? ItemPositionManager.Instance
            : FindFirstObjectByType<ItemPositionManager>();
        Camera worldCamera = Camera.main;
        if (positionManager == null || worldCamera == null || roomViewTransform == null ||
            towelItem == null || flashlightItem == null)
        {
            return;
        }

        if (!TryGetWorldRect(roomViewTransform as RectTransform, TowelRect, worldCamera,
                out Vector3 center, out Vector2 size))
        {
            return;
        }

        shelfWorldPosition = center;
        bottomSlot = RegisterCheckSlot(flashlightItem);
        topSlot = RegisterCheckSlot(towelItem);
        positionManager.MarkExternallyManaged(bottomSlot.SlotId);
        positionManager.MarkExternallyManaged(topSlot.SlotId);
        positionManager.ItemsResetForNewDay += ResetShelfForNewDay;
        positionManager.PapersRemovedPermanently += RemovePapersFromShelf;
        CreateDropTarget(center, size);
        checkSlotsReady = true;
        SyncCheckSlots();
    }

    private CheckSlot RegisterCheckSlot(ItemData expectedItem)
    {
        CheckSlot slot = new CheckSlot { ExpectedItemId = expectedItem.ItemId, ExpectedItem = expectedItem };
        WorldItemPickup pickup = CreateOccupant(slot, expectedItem);
        positionManager.RegisterInitialPickup(pickup);
        slot.SlotId = pickup.PositionSlotId;
        positionManager.NotifyPickedUp(pickup);
        Destroy(pickup.gameObject);
        return slot;
    }

    private WorldItemPickup CreateOccupant(CheckSlot slot, ItemData item)
    {
        GameObject go = new GameObject("화장실 제자리_" + slot.ExpectedItemId, typeof(BoxCollider2D), typeof(WorldItemPickup));
        go.transform.position = shelfWorldPosition;
        go.GetComponent<BoxCollider2D>().enabled = false;

        WorldItemPickup pickup = go.GetComponent<WorldItemPickup>();
        pickup.Configure(item, false);
        if (!string.IsNullOrEmpty(slot.SlotId))
            pickup.BindPositionSlot(slot.SlotId, shelfWorldPosition, slot.ExpectedItemId);
        return pickup;
    }

    private void SetSlotItem(CheckSlot slot, ItemData item)
    {
        if (slot == null || positionManager == null)
            return;

        if (slot.Occupant != null && slot.Occupant.ItemData == item)
            return;

        if (slot.Occupant != null)
        {
            positionManager.NotifyPickedUp(slot.Occupant);
            Destroy(slot.Occupant.gameObject);
            slot.Occupant = null;
        }

        if (item == null)
            return;

        slot.Occupant = CreateOccupant(slot, item);
        positionManager.RegisterInitialPickup(slot.Occupant);
    }

    // 쌓인 상태를 제자리 검사 자리 두 개에 반영합니다.
    // 제자리("손전등 위에 수건")일 때만 두 자리가 모두 맞고, 그 밖에는 하나 이상 틀리게 됩니다.
    private void SyncCheckSlots()
    {
        if (!checkSlotsReady)
            return;

        ItemData bottom = shelf.Count > 0 ? shelf[0].Item : null;
        ItemData top = null;
        if (shelf.Count == 2)
        {
            top = shelf[1].Item;
        }
        else if (shelf.Count > 2)
        {
            // 셋 이상 쌓였으면 수건이 아닌 물건으로 채워서 틀린 상태가 되게 합니다.
            for (int i = 1; i < shelf.Count; i++)
            {
                if (shelf[i].Item.ItemId != TowelItemId)
                {
                    top = shelf[i].Item;
                    break;
                }
            }
        }

        SetSlotItem(bottomSlot, bottom);
        SetSlotItem(topSlot, top);
    }

    // 일차 종료 초기화: 선반을 "손전등 위에 수건"으로 되돌립니다.
    private void ResetShelfForNewDay()
    {
        QuickSlotInventory inventory = GetInventory();

        foreach (ShelfItem entry in shelf)
        {
            string itemId = entry.Item != null ? entry.Item.ItemId : string.Empty;
            if (itemId == TowelItemId || itemId == FlashlightGimmick.FlashlightItemId)
                continue;

            // 처음 자리가 있는 물건은 ItemPositionManager가 이미 되돌렸고, 종이는 초기화되므로 그냥 치웁니다.
            bool hasOwnPlace = positionManager != null &&
                               (positionManager.HasResettableRecordFor(itemId) || positionManager.IsPaperItem(itemId));
            if (!hasOwnPlace && entry.Item != null)
                inventory?.TryAdd(entry.Item);
        }

        foreach (ShelfItem entry in shelf)
        {
            if (entry.View != null)
                Destroy(entry.View);
        }

        shelf.Clear();
        shelf.Add(new ShelfItem { Item = flashlightItem });
        shelf.Add(new ShelfItem { Item = towelItem });
        RemoveFromInventory(FlashlightGimmick.FlashlightItemId);
        RemoveFromInventory(TowelItemId);

        RebuildShelfViews();
        SyncCheckSlots();
    }

    private void RemovePapersFromShelf()
    {
        bool changed = false;
        for (int i = shelf.Count - 1; i >= 0; i--)
        {
            ItemData item = shelf[i].Item;
            if (item == null || positionManager == null || !positionManager.IsPaperItem(item.ItemId))
                continue;

            if (shelf[i].View != null)
                Destroy(shelf[i].View);
            shelf.RemoveAt(i);
            changed = true;
        }

        if (!changed)
            return;

        RebuildShelfViews();
        SyncCheckSlots();
    }

    // 수건 자리 위에 아이템을 끌어다 놓았을 때 (ItemInteractionTarget)
    private void CreateDropTarget(Vector3 center, Vector2 size)
    {
        dropTarget = new GameObject("화장실 수건 자리 (놓기 판정)", typeof(BoxCollider2D), typeof(ItemInteractionTarget));
        dropTarget.transform.position = center;
        BoxCollider2D box = dropTarget.GetComponent<BoxCollider2D>();
        box.size = size;
        box.isTrigger = true;

        ItemInteractionTarget target = dropTarget.GetComponent<ItemInteractionTarget>();
        FieldInfo acceptAny = typeof(ItemInteractionTarget).GetField(
            "acceptAnyItem", BindingFlags.Instance | BindingFlags.NonPublic);
        acceptAny?.SetValue(target, true);
        target.ItemAccepted += OnItemDropped;

        dropTarget.SetActive(false);
    }

    private void OnItemDropped(ItemData item)
    {
        if (item != null && TryPlaceOnShelf(item))
            RemoveFromInventory(item.ItemId);
    }

    // 다른 방에서 같은 화면 위치에 놓아 기존 시스템이 검사 자리에 아이템을 만들어 버린 경우 퀵슬롯으로 되돌립니다.
    private void ReturnForeignPlacements()
    {
        if (positionManager == null)
            return;

        foreach (CheckSlot slot in new[] { bottomSlot, topSlot })
        {
            if (slot == null || string.IsNullOrEmpty(slot.SlotId))
                continue;

            ItemPositionRecord record = null;
            foreach (ItemPositionRecord candidate in positionManager.PositionRecords)
            {
                if (candidate.SlotId == slot.SlotId)
                {
                    record = candidate;
                    break;
                }
            }

            WorldItemPickup placed = record?.CurrentPickup;
            if (placed == null || placed == slot.Occupant)
                continue;

            ItemData placedItem = placed.ItemData;
            positionManager.NotifyPickedUp(placed);
            Destroy(placed.gameObject);

            if (placedItem != null && !TryPlaceOnShelf(placedItem))
                GetInventory()?.TryAdd(placedItem);

            SyncCheckSlots();
        }
    }

    private static void RemoveFromInventory(string itemId)
    {
        QuickSlotInventory inventory = GetInventory();
        if (inventory == null)
            return;

        foreach (QuickSlotEntry entry in inventory.Entries)
        {
            if (entry?.ItemData != null && entry.ItemData.ItemId == itemId)
            {
                inventory.TryRemove(entry);
                return;
            }
        }
    }

    private static bool TryGetWorldRect(
        RectTransform roomView,
        RectInt pixelRect,
        Camera worldCamera,
        out Vector3 center,
        out Vector2 size)
    {
        center = default;
        size = default;
        if (roomView == null)
            return false;

        Canvas canvas = roomView.GetComponentInParent<Canvas>(true);
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        Vector3[] corners = new Vector3[4];
        roomView.GetWorldCorners(corners); // 0: 왼쪽 아래, 2: 오른쪽 위
        Vector2 screenMin = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[0]);
        Vector2 screenMax = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[2]);
        if ((screenMax - screenMin).sqrMagnitude < 1f)
            return false;

        Vector2 PixelToScreen(float x, float y) => new Vector2(
            Mathf.Lerp(screenMin.x, screenMax.x, x / ReferenceWidth),
            Mathf.Lerp(screenMax.y, screenMin.y, y / ReferenceHeight));

        float distance = Mathf.Abs(worldCamera.transform.position.z);
        Vector2 a = PixelToScreen(pixelRect.xMin, pixelRect.yMax);
        Vector2 b = PixelToScreen(pixelRect.xMax, pixelRect.yMin);
        Vector3 worldA = worldCamera.ScreenToWorldPoint(new Vector3(a.x, a.y, distance));
        Vector3 worldB = worldCamera.ScreenToWorldPoint(new Vector3(b.x, b.y, distance));
        worldA.z = worldB.z = 0f;

        center = (worldA + worldB) * 0.5f;
        size = new Vector2(Mathf.Abs(worldB.x - worldA.x), Mathf.Abs(worldB.y - worldA.y));
        return true;
    }

    // ═══════════════════════════════════════════════════
    //  금고 키패드 (RRPass)
    // ═══════════════════════════════════════════════════

    public void OpenKeypad()
    {
        if (safeUnlocked || IsBusy())
            return;

        // [위험도] 틀리면 100이 될 상황이면 경고만 하고 이번엔 열지 않음
        if (DangerLevel.WarnBeforeGimmick())
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
        EXODSound.Play("safe_click");   // 금고 버튼 딸깍딸깍 (Resources/EXODStage1/Sfx/safe_click.wav)

        if (enteredNumbers.Count < Answer.Length)
            return;

        if (IsCorrect())
        {
            UnlockSafe();
        }
        else
        {
            // 9개를 다 눌렀는데 틀림 → 실패 판정 후 비밀번호 창 닫기
            EXODSound.Play("keypad_error");
            DangerLevel.GimmickFailed();
            CloseKeypad();
        }
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

        CreateRingButton();

        EXODSound.Play("key_unlock");
        DangerLevel.GimmickSolved();
        Debug.Log("[Restroom] 금고 비밀번호 정답! RRSafe_block을 지웠습니다.", this);

        // 금고를 열면 종이는 모두 없어집니다. (퀵슬롯·맵·선반 어디에 있든)
        ItemPositionManager manager = positionManager != null ? positionManager : ItemPositionManager.GetOrCreate();
        manager?.RemovePapersPermanently();
    }

    // ═══════════════════════════════════════════════════
    //  금고 안 청혼반지 → 청혼 엔딩
    // ═══════════════════════════════════════════════════

    private void CreateRingButton()
    {
        // 반지함은 반지를 가져가도 금고 안에 남음
        if (ringBox == null && roomViewTransform != null)
            ringBox = CreateRoomImage(roomViewTransform, "반지함", LoadSprite("RingBox"), RingBoxRect, null, true);

        if (ringTaken || ringButton != null || roomViewTransform == null || ringItem == null)
            return;

        ringButton = CreateRoomImage(roomViewTransform, "청혼반지", ringItem.Icon, RingRect, TakeRing, true);
    }

    // 반지를 누르면 먼저 [반지를 집는다]/[그냥 둔다]를 고릅니다. (대사: Stage1Dialogues.ProposalRing*)
    private GameObject ringBox;

    private void TakeRing()
    {
        if (ringTaken || IsBusy())
            return;

        Stage1Room.PlayChoiceCutscene(
            Stage1Dialogues.ProposalRingFound(),
            Stage1Dialogues.ProposalRingChoices,
            choice =>
            {
                if (choice == 0)
                    TakeRingConfirmed();
                else
                    Stage1Room.PlayCutscene(Stage1Dialogues.ProposalRingLeave());
            });
    }

    private void TakeRingConfirmed()
    {
        if (ringTaken)
            return;

        QuickSlotInventory inventory = GetInventory();
        if (inventory != null && !inventory.TryAdd(ringItem))
        {
            Think("퀵슬롯이 가득 차서 주울 수 없어.");
            return;
        }

        ringTaken = true;
        if (ringButton != null)
            Destroy(ringButton);

        EXODSound.Play("item_pickup");
        ProposalEnding.Trigger();
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
