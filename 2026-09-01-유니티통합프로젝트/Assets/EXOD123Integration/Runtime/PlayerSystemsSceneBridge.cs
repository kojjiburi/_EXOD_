using System.Linq;
using UnityEngine;

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

    public void Configure(GameObject canvasObject, ItemData keyItem)
    {
        quickSlotCanvas = canvasObject;
        goldenKeyItem = keyItem;
    }

    private void LateUpdate()
    {
        UpdateQuickSlotVisibility();
        UpdateGoldenKeyInventory();
        UpdateDrawerReturnSlot();
    }

    private void OnDestroy()
    {
        DestroyDrawerReturnVisual();
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

    private void DestroyDrawerReturnVisual()
    {
        if (drawerDisplayedItem != null)
            Destroy(drawerDisplayedItem);

        drawerDisplayedItem = null;
        drawerDisplayedPickup = null;
    }
}
