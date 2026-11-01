using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

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

    [Header("Day End Check (일차 종료 시 제자리 검사)")]
    [Tooltip("일차가 넘어갈 때(GManager.NextDay) 아이템이 제자리에 있는지 검사합니다.")]
    [SerializeField] private bool checkOnDayEnd = true;
    [Tooltip("체크하면 틀린 아이템 개수만큼 하트가 줄고, 끄면 하나라도 틀렸을 때 하트 1개만 줄어듭니다.")]
    [SerializeField] private bool loseHeartPerWrongItem = false;
    [Tooltip("검사에서 제외할 아이템 ID (예: 사용하면 사라지는 아이템). 황금 열쇠는 서랍 기믹이 따로 관리해서 기본으로 제외합니다.")]
    [SerializeField] private List<string> ignoredItemIds = new List<string> { "golden_key", "paper_a", "paper_b", "paper_c", "paper_d", "paper_e" };
    [Tooltip("하나라도 제자리가 아닐 때 추가로 실행할 기능")]
    [SerializeField] private UnityEvent onItemsMisplaced;
    [Tooltip("모든 아이템이 제자리일 때 실행할 기능")]
    [SerializeField] private UnityEvent onAllItemsInPlace;

    private readonly Dictionary<string, ItemPositionRecord> recordBySlotId =
        new Dictionary<string, ItemPositionRecord>();

    private GManager subscribedDayManager;

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

    private void Start()
    {
        if (enabled)
            SubscribeToDayManager();
    }

    private void OnDestroy()
    {
        UnsubscribeFromDayManager();

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

    // ═══════════════════════════════════════════════════
    //  일차 종료 시 제자리 검사
    // ═══════════════════════════════════════════════════

    private void SubscribeToDayManager()
    {
        if (!checkOnDayEnd || subscribedDayManager != null)
            return;

        GManager dayManager = GManager.Instance != null
            ? GManager.Instance
            : FindFirstObjectByType<GManager>(FindObjectsInactive.Include);

        if (dayManager == null)
        {
            Debug.LogWarning("[ItemPositionManager] GManager를 찾을 수 없어 일차 종료 검사를 하지 않습니다.", this);
            return;
        }

        dayManager.onDayChanged += HandleDayChanged;
        subscribedDayManager = dayManager;
    }

    private void UnsubscribeFromDayManager()
    {
        if (subscribedDayManager == null)
            return;

        subscribedDayManager.onDayChanged -= HandleDayChanged;
        subscribedDayManager = null;
    }

    private void HandleDayChanged(int newDay)
    {
        CheckItemsOnDayEnd();
    }

    /// <summary>원래 아이템이 원래 자리에 없는 위치 목록을 돌려줍니다.</summary>
    public List<ItemPositionRecord> GetMisplacedRecords()
    {
        List<ItemPositionRecord> misplaced = new List<ItemPositionRecord>();

        foreach (ItemPositionRecord record in positionRecords)
        {
            record.RefreshOccupancy();

            if (IsIgnored(record.OriginalItemId))
                continue;

            // 비어 있거나, 다른 아이템이 놓여 있으면 제자리가 아닙니다.
            if (!record.IsOccupied || record.CurrentItemId != record.OriginalItemId)
                misplaced.Add(record);
        }

        return misplaced;
    }

    public bool AreAllItemsInOriginalPlace()
    {
        return GetMisplacedRecords().Count == 0;
    }

    /// <summary>
    /// 제자리 검사를 하고, 틀린 게 있으면 하트를 줄입니다. 틀린 위치 개수를 돌려줍니다.
    /// 하트가 0이 되면 PlayerFaintController가 게임 오버를 띄웁니다.
    /// </summary>
    public int CheckItemsOnDayEnd()
    {
        List<ItemPositionRecord> misplaced = GetMisplacedRecords();

        if (misplaced.Count == 0)
        {
            Debug.Log("[ItemPositionManager] 일차 종료 검사: 모든 아이템이 제자리입니다.", this);
            onAllItemsInPlace?.Invoke();
            return 0;
        }

        foreach (ItemPositionRecord record in misplaced)
        {
            string current = record.IsOccupied ? record.CurrentItemId : "(비어 있음)";
            Debug.Log(
                $"[ItemPositionManager] 제자리 아님: 원래 '{record.OriginalItemId}', 지금 '{current}' ({record.SlotId})",
                this);
        }

        HeartManager hearts = FindFirstObjectByType<HeartManager>(FindObjectsInactive.Include);
        if (hearts == null)
        {
            Debug.LogWarning("[ItemPositionManager] HeartManager를 찾을 수 없어 하트를 줄이지 못했습니다.", this);
        }
        else
        {
            int penalty = loseHeartPerWrongItem ? misplaced.Count : 1;
            for (int i = 0; i < penalty; i++)
                hearts.HPM();
        }

        onItemsMisplaced?.Invoke();
        return misplaced.Count;
    }

    private bool IsIgnored(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return true;

        foreach (string ignoredId in ignoredItemIds)
        {
            if (!string.IsNullOrWhiteSpace(ignoredId) && ignoredId.Trim() == itemId)
                return true;
        }

        return false;
    }

    // Play 중 Inspector의 ⋮ 메뉴에서 실행해 검사를 확인합니다.
    [ContextMenu("일차 종료 제자리 검사 테스트")]
    private void TestDayEndCheck()
    {
        CheckItemsOnDayEnd();
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
