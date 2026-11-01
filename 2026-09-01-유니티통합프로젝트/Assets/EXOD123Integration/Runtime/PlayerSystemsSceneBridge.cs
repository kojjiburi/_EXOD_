using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class PlayerSystemsSceneBridge : MonoBehaviour
{
    [Header("Quick Slot Visibility")]
    [SerializeField] private GameObject quickSlotCanvas;

    [Header("Bedroom Key Integration")]
    [SerializeField] private ItemData goldenKeyItem;

    [Header("Bedroom Drawer Return Slot")]
    [SerializeField] private Vector2 drawerReturnedItemOffset = new Vector2(0f, 70f);
    [SerializeField, Min(32f)] private float drawerReturnedItemSize = 72f;

    private bool isQuickSlotSuppressed;
    private bool quickSlotWasActive;
    private bool goldenKeyWasAdded;
    private ItemPositionManager drawerPositionManager;
    private string drawerPositionSlotId;
    private RectTransform drawerPositionAnchor;
    private WorldItemPickup drawerDisplayedPickup;
    private GameObject drawerDisplayedItem;
    private GameObject drawerDropZone;

    public void Configure(GameObject canvasObject, ItemData keyItem)
    {
        quickSlotCanvas = canvasObject;
        goldenKeyItem = keyItem;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // 이 오브젝트는 씬을 넘어가도 유지되므로, 새 게임([다시하기]·로비에서 시작)마다 열쇠 상태를 처음으로 돌립니다.
    // (산책 씬처럼 위에 추가로 여는 씬은 같은 게임이므로 그대로 둡니다)
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single)
            return;

        goldenKeyWasAdded = false;
        DestroyDrawerReturnVisual();
        drawerPositionManager = null;
        drawerPositionAnchor = null;
        drawerPositionSlotId = string.Empty;
        if (drawerDropZone != null)
            drawerDropZone.SetActive(false);
    }

    private void LateUpdate()
    {
        UpdateQuickSlotVisibility();
        UpdateGoldenKeyInventory();
        UpdateDrawerReturnSlot();
        UpdateDrawerDropZone();
    }

    private void OnDestroy()
    {
        DestroyDrawerReturnVisual();

        if (drawerDropZone != null)
            Destroy(drawerDropZone);
    }

    private void UpdateQuickSlotVisibility()
    {
        bool inspectionOpen =
            InspectionManager.Instance != null && InspectionManager.Instance.IsOpen;
        bool dialogueOpen =
            DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueOpen;
        PasswordDrawer drawer = FindFirstObjectByType<PasswordDrawer>(
            FindObjectsInactive.Include);
        bool collectedKeyFromDrawer =
            inspectionOpen &&
            InspectionManager.Instance.CurrentItemId == "bedroom_drawer" &&
            drawer != null &&
            drawer.IsKeyCollected;
        bool shouldSuppress =
            dialogueOpen || (inspectionOpen && !collectedKeyFromDrawer);

        if (shouldSuppress && !isQuickSlotSuppressed)
        {
            quickSlotWasActive = quickSlotCanvas != null && quickSlotCanvas.activeSelf;
            if (quickSlotCanvas != null)
                quickSlotCanvas.SetActive(false);

            isQuickSlotSuppressed = true;
        }
        else if (!shouldSuppress && isQuickSlotSuppressed)
        {
            if (quickSlotCanvas != null)
            {
                quickSlotCanvas.SetActive(quickSlotWasActive);

                if (quickSlotWasActive)
                {
                    QuickSlotController controller =
                        quickSlotCanvas.GetComponentInChildren<QuickSlotController>(true);
                    if (controller != null)
                        controller.Refresh();
                }
            }

            isQuickSlotSuppressed = false;
        }
    }

    private void UpdateGoldenKeyInventory()
    {
        if (goldenKeyWasAdded || goldenKeyItem == null)
            return;

        PasswordDrawer drawer = FindFirstObjectByType<PasswordDrawer>(
            FindObjectsInactive.Include);
        if (drawer == null || !drawer.IsKeyCollected)
            return;

        QuickSlotInventory inventory = QuickSlotInventory.Instance;
        if (inventory == null)
            inventory = FindFirstObjectByType<QuickSlotInventory>();
        if (inventory == null)
            return;

        bool alreadyStored = inventory.Entries.Any(entry =>
            entry != null &&
            entry.ItemData != null &&
            (entry.ItemData == goldenKeyItem ||
             entry.ItemData.ItemId == goldenKeyItem.ItemId));

        if (alreadyStored || inventory.TryAdd(goldenKeyItem))
            goldenKeyWasAdded = true;
    }

    private void UpdateDrawerReturnSlot()
    {
        if (goldenKeyItem == null)
            return;

        PasswordDrawer drawer = FindFirstObjectByType<PasswordDrawer>(
            FindObjectsInactive.Include);
        if (drawer == null || !drawer.IsKeyCollected)
            return;

        ItemPositionManager manager = ItemPositionManager.Instance;
        if (manager == null)
            manager = FindFirstObjectByType<ItemPositionManager>();

        RectTransform drawerAnchor = FindDrawerPositionAnchor();
        if (manager == null || drawerAnchor == null)
            return;

        if (drawerPositionManager != manager ||
            drawerPositionAnchor != drawerAnchor ||
            string.IsNullOrWhiteSpace(drawerPositionSlotId))
        {
            RegisterDrawerPosition(manager, drawerAnchor);
        }

        SyncDrawerReturnVisual();
    }

    private RectTransform FindDrawerPositionAnchor()
    {
        if (drawerPositionAnchor != null)
            return drawerPositionAnchor;

        InspectableItem[] items = FindObjectsByType<InspectableItem>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        InspectableItem drawerItem = items.FirstOrDefault(item =>
            item != null && item.ItemId == "bedroom_drawer");
        return drawerItem == null
            ? null
            : drawerItem.transform as RectTransform;
    }

    private void RegisterDrawerPosition(
        ItemPositionManager manager,
        RectTransform drawerAnchor)
    {
        if (!TryGetDrawerWorldPosition(drawerAnchor, out Vector3 worldPosition))
            return;

        DestroyDrawerReturnVisual();
        drawerPositionManager = manager;
        drawerPositionAnchor = drawerAnchor;
        drawerPositionSlotId = string.Empty;

        GameObject slotSource = new GameObject(
            "DrawerKeyReturnSlotSource",
            typeof(BoxCollider2D),
            typeof(WorldItemPickup));
        slotSource.transform.position = worldPosition;
        slotSource.GetComponent<BoxCollider2D>().enabled = false;

        WorldItemPickup pickup = slotSource.GetComponent<WorldItemPickup>();
        pickup.Configure(goldenKeyItem, false);
        manager.RegisterInitialPickup(pickup);
        drawerPositionSlotId = pickup.PositionSlotId;
        manager.NotifyPickedUp(pickup);
        Destroy(slotSource);
    }

    private bool TryGetDrawerWorldPosition(
        RectTransform drawerAnchor,
        out Vector3 worldPosition)
    {
        worldPosition = default;
        Camera worldCamera = Camera.main;
        if (drawerAnchor == null || worldCamera == null)
            return false;

        Canvas canvas = drawerAnchor.GetComponentInParent<Canvas>();
        Camera uiCamera = canvas != null &&
                          canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        Vector3 localPoint = drawerAnchor.rect.center + drawerReturnedItemOffset;
        Vector3 anchorWorldPoint = drawerAnchor.TransformPoint(localPoint);
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(
            uiCamera,
            anchorWorldPoint);

        float cameraDistance = Mathf.Abs(worldCamera.transform.position.z);
        worldPosition = worldCamera.ScreenToWorldPoint(
            new Vector3(screenPoint.x, screenPoint.y, cameraDistance));
        worldPosition.z = 0f;
        return true;
    }

    private void SyncDrawerReturnVisual()
    {
        if (drawerPositionManager == null ||
            drawerPositionAnchor == null ||
            string.IsNullOrWhiteSpace(drawerPositionSlotId))
        {
            return;
        }

        ItemPositionRecord record = drawerPositionManager.PositionRecords
            .FirstOrDefault(position => position.SlotId == drawerPositionSlotId);
        WorldItemPickup pickup = record == null ? null : record.CurrentPickup;

        if (pickup == drawerDisplayedPickup && drawerDisplayedItem != null)
            return;

        DestroyDrawerReturnVisual();
        if (pickup == null || pickup.ItemData == null || pickup.ItemData.Icon == null)
            return;

        SpriteRenderer worldRenderer = pickup.GetComponent<SpriteRenderer>();
        if (worldRenderer != null)
            worldRenderer.enabled = false;

        drawerDisplayedPickup = pickup;
        drawerDisplayedItem = new GameObject(
            $"DrawerReturnedItem_{pickup.ItemData.ItemId}",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(UnityEngine.UI.Image),
            typeof(DrawerReturnedItemUi));
        drawerDisplayedItem.layer = drawerPositionAnchor.gameObject.layer;

        RectTransform rect = drawerDisplayedItem.GetComponent<RectTransform>();
        rect.SetParent(drawerPositionAnchor, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = drawerReturnedItemOffset;
        float size = Mathf.Max(32f, drawerReturnedItemSize);
        rect.sizeDelta = new Vector2(size, size);

        UnityEngine.UI.Image image =
            drawerDisplayedItem.GetComponent<UnityEngine.UI.Image>();
        image.sprite = pickup.ItemData.Icon;
        image.preserveAspect = true;
        image.raycastTarget = true;

        drawerDisplayedItem.GetComponent<DrawerReturnedItemUi>()
            .Configure(pickup);
    }

    // ═══════════════════════════════════════════════════
    //  황금 열쇠 돌려놓기 판정 영역
    //  제자리 판정 반경(ItemPositionManager.PlacementRadius)이 화면에서 아주 작아서
    //  정확한 한 점에 놓아야만 돌려놓기가 되던 문제를 보완합니다.
    //  서랍 확대 화면이 열려 있으면 그 화면 전체, 아니면 침실 서랍장 위 어디에 놓아도 제자리로 돌아갑니다.
    // ═══════════════════════════════════════════════════

    private void UpdateDrawerDropZone()
    {
        bool canReturn = TryGetDrawerDropScreenRect(out Rect screenRect) &&
                         FindGoldenKeyEntry() != null &&
                         FindEmptyDrawerRecord() != null;

        if (!canReturn || !TryGetWorldRect(screenRect, out Vector3 center, out Vector2 size))
        {
            if (drawerDropZone != null && drawerDropZone.activeSelf)
                drawerDropZone.SetActive(false);
            return;
        }

        if (drawerDropZone == null)
            CreateDrawerDropZone();

        drawerDropZone.transform.position = center;
        drawerDropZone.GetComponent<BoxCollider2D>().size = size;
        if (!drawerDropZone.activeSelf)
            drawerDropZone.SetActive(true);
    }

    private bool TryGetDrawerDropScreenRect(out Rect screenRect)
    {
        screenRect = default;
        InspectionManager inspection = InspectionManager.Instance;

        // 서랍 확대 화면: 아래 퀵슬롯을 뺀 화면 전체
        if (inspection != null && inspection.IsOpen)
        {
            if (inspection.CurrentItemId != "bedroom_drawer")
                return false;

            screenRect = new Rect(0f, Screen.height * 0.22f, Screen.width, Screen.height * 0.78f);
            return true;
        }

        // 침실 화면: 서랍장 클릭 영역 (Screen Space Overlay라 월드 좌표가 곧 화면 좌표)
        RectTransform anchor = FindDrawerPositionAnchor();
        if (anchor == null || !anchor.gameObject.activeInHierarchy)
            return false;

        Vector3[] corners = new Vector3[4];
        anchor.GetWorldCorners(corners);
        screenRect = Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        return true;
    }

    private static bool TryGetWorldRect(Rect screenRect, out Vector3 center, out Vector2 size)
    {
        center = default;
        size = default;
        Camera worldCamera = Camera.main;
        if (worldCamera == null)
            return false;

        float distance = Mathf.Abs(worldCamera.transform.position.z);
        Vector3 min = worldCamera.ScreenToWorldPoint(new Vector3(screenRect.xMin, screenRect.yMin, distance));
        Vector3 max = worldCamera.ScreenToWorldPoint(new Vector3(screenRect.xMax, screenRect.yMax, distance));
        center = new Vector3((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f, 0f);
        size = new Vector2(Mathf.Abs(max.x - min.x), Mathf.Abs(max.y - min.y));
        return true;
    }

    private void CreateDrawerDropZone()
    {
        drawerDropZone = new GameObject("서랍 열쇠 돌려놓기 영역", typeof(BoxCollider2D), typeof(ItemInteractionTarget));
        drawerDropZone.GetComponent<BoxCollider2D>().isTrigger = true;

        ItemInteractionTarget target = drawerDropZone.GetComponent<ItemInteractionTarget>();
        FieldInfo acceptedField = typeof(ItemInteractionTarget).GetField(
            "acceptedItems", BindingFlags.Instance | BindingFlags.NonPublic);
        if (acceptedField?.GetValue(target) is List<ItemData> accepted)
            accepted.Add(goldenKeyItem);
        target.ItemAccepted += OnKeyDroppedOnDrawer;

        drawerDropZone.SetActive(false);
    }

    private void OnKeyDroppedOnDrawer(ItemData item)
    {
        ItemPositionRecord record = FindEmptyDrawerRecord();
        QuickSlotEntry entry = FindGoldenKeyEntry();
        if (record == null || entry == null || drawerPositionManager == null)
            return;

        // 서랍 자리의 정확한 위치에 놓은 것으로 처리합니다.
        if (!drawerPositionManager.TryPlaceItem(goldenKeyItem, record.OriginalPosition))
            return;

        QuickSlotInventory inventory = QuickSlotInventory.Instance != null
            ? QuickSlotInventory.Instance
            : FindFirstObjectByType<QuickSlotInventory>();
        inventory?.TryRemove(entry);
        EXODSound.Play("item_pickup");
    }

    private QuickSlotEntry FindGoldenKeyEntry()
    {
        if (goldenKeyItem == null)
            return null;

        QuickSlotInventory inventory = QuickSlotInventory.Instance != null
            ? QuickSlotInventory.Instance
            : FindFirstObjectByType<QuickSlotInventory>();
        return inventory?.Entries.FirstOrDefault(entry =>
            entry?.ItemData != null && entry.ItemData.ItemId == goldenKeyItem.ItemId);
    }

    private ItemPositionRecord FindEmptyDrawerRecord()
    {
        if (drawerPositionManager == null || string.IsNullOrWhiteSpace(drawerPositionSlotId))
            return null;

        ItemPositionRecord record = drawerPositionManager.PositionRecords
            .FirstOrDefault(position => position.SlotId == drawerPositionSlotId);
        if (record == null)
            return null;

        record.RefreshOccupancy();
        return record.IsOccupied ? null : record;
    }

    private void DestroyDrawerReturnVisual()
    {
        if (drawerDisplayedItem != null)
            Destroy(drawerDisplayedItem);

        drawerDisplayedItem = null;
        drawerDisplayedPickup = null;
    }
}
