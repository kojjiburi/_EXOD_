using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
public sealed class QuickSlotController : MonoBehaviour
{
    [SerializeField] private QuickSlotInventory inventory;
    [SerializeField] private RectTransform slotContainer;
    [SerializeField] private RectTransform dragLayer;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private ItemPositionManager itemPositionManager;
    [SerializeField] private LayerMask interactionLayerMask = ~0;

    private QuickSlotSlotView[] slotViews;
    private GameObject dragVisual;
    private Image dragImage;

    public QuickSlotInventory Inventory => inventory;

    private void Awake()
    {
        ResolveReferences();
        CacheSlotViews();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (inventory != null)
            inventory.Changed += Refresh;
    }

    private void Start()
    {
        Refresh();
    }

    private void OnDisable()
    {
        if (inventory != null)
            inventory.Changed -= Refresh;

        CancelDragVisual();
    }

    public void Configure(
        QuickSlotInventory quickSlotInventory,
        RectTransform slots,
        RectTransform itemDragLayer,
        Camera itemWorldCamera)
    {
        inventory = quickSlotInventory;
        slotContainer = slots;
        dragLayer = itemDragLayer;
        worldCamera = itemWorldCamera;
        CacheSlotViews();
    }

    public void Refresh()
    {
        ResolveReferences();

        if (slotViews == null || slotViews.Length == 0)
            CacheSlotViews();

        if (slotViews == null)
            return;

        IReadOnlyList<QuickSlotEntry> orderedEntries = inventory == null
            ? new List<QuickSlotEntry>()
            : inventory.Entries.OrderBy(entry => entry.AcquiredSequence).ToList();

        for (int index = 0; index < slotViews.Length; index++)
        {
            QuickSlotEntry entry = index < orderedEntries.Count ? orderedEntries[index] : null;
            slotViews[index].Bind(entry);
        }
    }

    public bool BeginDrag(QuickSlotEntry entry, Sprite sprite, Vector2 sourceSize, PointerEventData eventData)
    {
        if (entry == null || sprite == null || dragLayer == null)
            return false;

        CancelDragVisual();

        dragVisual = new GameObject(
            "DraggedQuickSlotItem",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));

        RectTransform rect = dragVisual.GetComponent<RectTransform>();
        rect.SetParent(dragLayer, false);

        float iconSize = Mathf.Clamp(Mathf.Min(sourceSize.x, sourceSize.y) * 0.72f, 48f, 128f);
        rect.sizeDelta = new Vector2(iconSize, iconSize);

        dragImage = dragVisual.GetComponent<Image>();
        dragImage.sprite = sprite;
        dragImage.preserveAspect = true;
        dragImage.raycastTarget = false;
        dragImage.color = new Color(1f, 1f, 1f, 0.92f);

        UpdateDragPosition(eventData.position);
        return true;
    }

    public void UpdateDragPosition(Vector2 screenPosition)
    {
        if (dragVisual == null || dragLayer == null)
            return;

        Canvas canvas = dragLayer.GetComponentInParent<Canvas>();
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                dragLayer,
                screenPosition,
                uiCamera,
                out Vector2 localPoint))
        {
            dragVisual.GetComponent<RectTransform>().anchoredPosition = localPoint;
        }
    }

    public bool CompleteDrag(QuickSlotEntry entry, Vector2 screenPosition)
    {
        bool used = TryInteractWithWorld(entry, screenPosition);

        if (!used)
            used = TryPlaceInSavedPosition(entry, screenPosition);

        CancelDragVisual();
        Refresh();
        return used;
    }

    public void CancelDragVisual()
    {
        if (dragVisual != null)
            Destroy(dragVisual);

        dragVisual = null;
        dragImage = null;
    }

    private bool TryInteractWithWorld(QuickSlotEntry entry, Vector2 screenPosition)
    {
        if (entry == null || inventory == null || !inventory.Contains(entry))
            return false;

        if (worldCamera == null)
            worldCamera = Camera.main;

        if (worldCamera == null)
            return false;

        if (!TryGetWorldPosition(screenPosition, out Vector3 worldPosition))
            return false;

        Collider2D[] hits = Physics2D.OverlapPointAll(worldPosition, interactionLayerMask);
        if (hits.Length == 0)
            return false;

        List<ItemInteractionTarget> targets = new List<ItemInteractionTarget>();
        foreach (Collider2D hit in hits)
        {
            ItemInteractionTarget target = hit.GetComponentInParent<ItemInteractionTarget>();
            if (target != null && !targets.Contains(target))
                targets.Add(target);
        }

        foreach (ItemInteractionTarget target in targets)
        {
            if (!target.CanAccept(entry.ItemData))
                continue;

            if (!target.TryInteract(entry))
                continue;

            if (entry.ItemData.ConsumeOnSuccessfulUse)
                inventory.TryRemove(entry);

            return true;
        }

        if (targets.Count > 0)
            targets[0].NotifyRejected(entry.ItemData);

        return false;
    }

    private bool TryPlaceInSavedPosition(QuickSlotEntry entry, Vector2 screenPosition)
    {
        if (entry == null ||
            entry.ItemData == null ||
            inventory == null ||
            !inventory.Contains(entry))
        {
            return false;
        }

        if (itemPositionManager == null)
            itemPositionManager = ItemPositionManager.GetOrCreate();

        if (itemPositionManager == null ||
            !TryGetWorldPosition(screenPosition, out Vector3 worldPosition) ||
            !itemPositionManager.TryPlaceItem(entry.ItemData, worldPosition))
        {
            return false;
        }

        return inventory.TryRemove(entry);
    }

    private bool TryGetWorldPosition(Vector2 screenPosition, out Vector3 worldPosition)
    {
        worldPosition = default;

        if (worldCamera == null)
            worldCamera = Camera.main;

        if (worldCamera == null)
            return false;

        Ray ray = worldCamera.ScreenPointToRay(screenPosition);
        if (Mathf.Abs(ray.direction.z) < 0.0001f)
            return false;

        float distance = -ray.origin.z / ray.direction.z;
        if (distance < 0f)
            return false;

        worldPosition = ray.GetPoint(distance);
        return true;
    }

    private void ResolveReferences()
    {
        if (inventory == null)
            inventory = QuickSlotInventory.Instance;

        if (inventory == null)
            inventory = FindFirstObjectByType<QuickSlotInventory>();

        if (worldCamera == null)
            worldCamera = Camera.main;

        if (itemPositionManager == null)
            itemPositionManager = ItemPositionManager.Instance;

        if (itemPositionManager == null)
            itemPositionManager = FindFirstObjectByType<ItemPositionManager>();
    }

    private void CacheSlotViews()
    {
        if (slotContainer == null)
        {
            slotViews = new QuickSlotSlotView[0];
            return;
        }

        slotViews = slotContainer
            .GetComponentsInChildren<QuickSlotSlotView>(true)
            .OrderBy(view => view.transform.GetSiblingIndex())
            .ToArray();

        for (int index = 0; index < slotViews.Length; index++)
            slotViews[index].Configure(this, index, slotViews[index].IconImage);
    }
}
