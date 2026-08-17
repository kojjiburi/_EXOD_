using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class ItemPositionRecord
{
    [SerializeField] private string slotId;
    [SerializeField] private Vector3 originalPosition;
    [SerializeField] private string originalItemId;

    [Header("Current State (Runtime)")]
    [SerializeField] private bool isOccupied;
    [SerializeField] private string currentItemId;
    [SerializeField] private WorldItemPickup currentPickup;

    public string SlotId => slotId;
    public Vector3 OriginalPosition => originalPosition;
    public string OriginalItemId => originalItemId;
    public bool IsOccupied => isOccupied && currentPickup != null;
    public string CurrentItemId => IsOccupied ? currentItemId : string.Empty;
    public WorldItemPickup CurrentPickup => IsOccupied ? currentPickup : null;

    public ItemPositionRecord(
        string slotId,
        Vector3 originalPosition,
        string originalItemId,
        WorldItemPickup initialPickup)
    {
        this.slotId = slotId;
        this.originalPosition = originalPosition;
        this.originalItemId = originalItemId ?? string.Empty;
        SetOccupant(initialPickup);
    }

    internal void SetOccupant(WorldItemPickup pickup)
    {
        currentPickup = pickup;
        isOccupied = pickup != null;
        currentItemId = pickup == null || pickup.ItemData == null
            ? string.Empty
            : pickup.ItemData.ItemId;
    }

    internal void RefreshOccupancy()
    {
        if (currentPickup == null)
            SetOccupant(null);
    }
}

[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
public sealed class ItemPositionManager : MonoBehaviour
{
    public static ItemPositionManager Instance { get; private set; }

    [Header("Placement")]
    [SerializeField, Min(0.05f)] private float placementRadius = 0.8f;
    [SerializeField] private int placedItemSortingOrder = 1000;

    [Header("Start Position Records (Runtime)")]
    [SerializeField] private List<ItemPositionRecord> positionRecords =
        new List<ItemPositionRecord>();

    private readonly Dictionary<string, ItemPositionRecord> recordBySlotId =
        new Dictionary<string, ItemPositionRecord>();

    public IReadOnlyList<ItemPositionRecord> PositionRecords => positionRecords;
    public float PlacementRadius => placementRadius;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[ItemPositionManager] Duplicate manager was disabled.", this);
            enabled = false;
            return;
        }

        Instance = this;
        CaptureInitialPositions();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnValidate()
    {
        placementRadius = Mathf.Max(0.05f, placementRadius);
    }

    public static ItemPositionManager GetOrCreate()
    {
        if (Instance != null)
            return Instance;

        ItemPositionManager existing = FindFirstObjectByType<ItemPositionManager>();
        if (existing != null)
            return existing;

        GameObject managerObject = new GameObject("ItemPositionManager");
        return managerObject.AddComponent<ItemPositionManager>();
    }

    public void CaptureInitialPositions()
    {
        positionRecords.Clear();
        recordBySlotId.Clear();

        WorldItemPickup[] pickups = FindObjectsByType<WorldItemPickup>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        Array.Sort(pickups, (left, right) =>
            string.CompareOrdinal(BuildStableSlotId(left.transform), BuildStableSlotId(right.transform)));

        foreach (WorldItemPickup pickup in pickups)
            RegisterInitialPickup(pickup);
    }

    public void RegisterInitialPickup(WorldItemPickup pickup)
    {
        if (pickup == null || pickup.ItemData == null)
            return;

        string existingSlotId = pickup.PositionSlotId;
        if (!string.IsNullOrWhiteSpace(existingSlotId) &&
            recordBySlotId.TryGetValue(existingSlotId, out ItemPositionRecord existingRecord))
        {
            existingRecord.SetOccupant(pickup);
            pickup.BindPositionSlot(
                existingRecord.SlotId,
                existingRecord.OriginalPosition,
                existingRecord.OriginalItemId);
            return;
        }

        string slotId = BuildStableSlotId(pickup.transform);
        if (recordBySlotId.TryGetValue(slotId, out ItemPositionRecord record))
        {
            record.SetOccupant(pickup);
            pickup.BindPositionSlot(
                record.SlotId,
                record.OriginalPosition,
                record.OriginalItemId);
            return;
        }

        Vector3 originalPosition = pickup.transform.position;
        string originalItemId = pickup.ItemData.ItemId;
        ItemPositionRecord newRecord = new ItemPositionRecord(
            slotId,
            originalPosition,
            originalItemId,
            pickup);

        positionRecords.Add(newRecord);
        recordBySlotId.Add(slotId, newRecord);
        pickup.BindPositionSlot(slotId, originalPosition, originalItemId);
    }

    public void NotifyPickedUp(WorldItemPickup pickup)
    {
        if (pickup == null || string.IsNullOrWhiteSpace(pickup.PositionSlotId))
            return;

        if (!recordBySlotId.TryGetValue(
                pickup.PositionSlotId,
                out ItemPositionRecord record))
        {
            return;
        }

        if (record.CurrentPickup == pickup)
            record.SetOccupant(null);
    }

    public bool TryPlaceItem(ItemData itemData, Vector3 droppedWorldPosition)
    {
        return TryPlaceItem(itemData, droppedWorldPosition, out _);
    }

    public bool TryPlaceItem(
        ItemData itemData,
        Vector3 droppedWorldPosition,
        out ItemPositionRecord usedPosition)
    {
        usedPosition = null;

        if (itemData == null)
            return false;

        float closestDistanceSquared = placementRadius * placementRadius;

        foreach (ItemPositionRecord record in positionRecords)
        {
            record.RefreshOccupancy();
            if (record.IsOccupied)
                continue;

            float distanceSquared =
                (record.OriginalPosition - droppedWorldPosition).sqrMagnitude;

            if (distanceSquared > closestDistanceSquared)
                continue;

            closestDistanceSquared = distanceSquared;
            usedPosition = record;
        }

        if (usedPosition == null)
            return false;

        WorldItemPickup placedPickup = CreatePlacedPickup(itemData, usedPosition);
        usedPosition.SetOccupant(placedPickup);
        return true;
    }

    private WorldItemPickup CreatePlacedPickup(
        ItemData itemData,
        ItemPositionRecord positionRecord)
    {
        string objectName = string.IsNullOrWhiteSpace(itemData.ItemId)
            ? "PlacedWorldItem"
            : $"PlacedWorldItem_{itemData.ItemId}";

        GameObject placedObject = new GameObject(objectName);
        placedObject.SetActive(false);
        placedObject.transform.SetParent(transform, true);
        placedObject.transform.position = positionRecord.OriginalPosition;

        SpriteRenderer renderer = placedObject.AddComponent<SpriteRenderer>();
        renderer.sprite = itemData.Icon;
        renderer.sortingOrder = placedItemSortingOrder;

        BoxCollider2D collider = placedObject.AddComponent<BoxCollider2D>();
        if (itemData.Icon != null)
        {
            Vector3 spriteSize = itemData.Icon.bounds.size;
            collider.size = new Vector2(
                Mathf.Max(0.1f, spriteSize.x),
                Mathf.Max(0.1f, spriteSize.y));
        }

        WorldItemPickup pickup = placedObject.AddComponent<WorldItemPickup>();
        pickup.Configure(itemData);
        pickup.BindPositionSlot(
            positionRecord.SlotId,
            positionRecord.OriginalPosition,
            positionRecord.OriginalItemId);

        placedObject.SetActive(true);
        return pickup;
    }

    private static string BuildStableSlotId(Transform itemTransform)
    {
        List<string> pathParts = new List<string>();
        Transform current = itemTransform;

        while (current != null)
        {
            pathParts.Add($"{current.name}[{current.GetSiblingIndex()}]");
            current = current.parent;
        }

        pathParts.Reverse();
        string scenePath = itemTransform.gameObject.scene.path;
        return $"{scenePath}::{string.Join("/", pathParts)}";
    }

    private void OnDrawGizmosSelected()
    {
        foreach (ItemPositionRecord record in positionRecords)
        {
            record.RefreshOccupancy();
            Gizmos.color = record.IsOccupied ? Color.yellow : Color.cyan;
            Gizmos.DrawWireSphere(record.OriginalPosition, placementRadius);
        }
    }
}
