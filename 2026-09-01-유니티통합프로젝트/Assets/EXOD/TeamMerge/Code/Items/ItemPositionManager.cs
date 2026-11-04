using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public sealed class ItemPositionRecord
{
    [SerializeField] private string slotId;
    [SerializeField] private Vector3 originalPosition;
    [SerializeField] private string originalItemId;
    [SerializeField] private ItemData originalItem;

    [Header("Current State (Runtime)")]
    [SerializeField] private bool isOccupied;
    [SerializeField] private string currentItemId;
    [SerializeField] private WorldItemPickup currentPickup;

    public string SlotId => slotId;
    public Vector3 OriginalPosition => originalPosition;
    public string OriginalItemId => originalItemId;
    public ItemData OriginalItem => originalItem;
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
        originalItem = initialPickup != null ? initialPickup.ItemData : null;
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
    [Tooltip("검사에서 제외할 아이템 ID. 황금 열쇠(golden_key)는 점검 대상이라 여기 넣어도 무시됩니다.")]
    [SerializeField] private List<string> ignoredItemIds = new List<string> { "paper_a", "paper_b", "paper_c", "paper_d", "paper_e" };
    [Tooltip("퀵슬롯에 들고 있어도 제자리로 치는 아이템 ID. 종이(paper_)와 기믹에 쓰면 사라지는 아이템은 여기 없어도 항상 괜찮습니다.")]
    [SerializeField] private List<string> inventoryAllowedItemIds = new List<string>();

    // 퀵슬롯에 들고 있어도 제자리 검사에서 제외하는 아이템
    // - 기믹을 푸는 데 쓰면 사라지는 아이템 (곰돌이에게 돌려주는 팔다리)
    // - 냉장고 비밀번호 쪽지 (곰돌이 보답)
    private static readonly string[] AlwaysAllowedInInventoryItemIds =
    {
        "bear_left_arm",
        "bear_right_arm",
        "bear_left_leg",
        "bear_right_leg",
        "fridge_lock_note",
        // 청혼반지: 주우면 바로 청혼 엔딩
        "proposal_ring",
        // 2스테이지 전화 부스 기믹 아이템 (산책에서 얻고, 부스에 쓰면 사라짐 / 주소지는 신고용으로 남음)
        "stage2_booth_key",
        "stage2_insulating_tape",
        "stage2_dial_button_1",
        "stage2_dial_button_2",
        "stage2_address_note",
    };

    private const string GoldenKeyItemId = "golden_key";

    private bool papersRemovedPermanently;

    /// <summary>종이가 영구히 없어졌을 때 (화장실 금고를 연 뒤) 호출됩니다.</summary>
    public event Action PapersRemovedPermanently;

    public bool ArePapersRemovedPermanently => papersRemovedPermanently;
    [Tooltip("하나라도 제자리가 아닐 때 추가로 실행할 기능")]
    [SerializeField] private UnityEvent onItemsMisplaced;
    [Tooltip("모든 아이템이 제자리일 때 실행할 기능")]
    [SerializeField] private UnityEvent onAllItemsInPlace;

    [Header("Day End Reset (일차 종료 시 초기화)")]
    [Tooltip("일차 종료 검사에서 틀린 게 있으면 모든 아이템을 처음 자리로 되돌리고, 퀵슬롯의 종이(paper_)를 없앱니다.")]
    [SerializeField] private bool resetItemsOnDayEnd = true;
    [Tooltip("초기화하지 않을 아이템 ID. 황금 열쇠(golden_key)는 서랍으로 돌아가야 해서 여기 넣어도 무시됩니다.")]
    [SerializeField] private List<string> resetExcludedItemIds = new List<string>();
    [Tooltip("이 글자로 시작하는 아이템은 스테이지마다 1회성이라 일차가 넘어가면 퀵슬롯에서 없어지고 처음 자리에 다시 생깁니다.")]
    [SerializeField] private string paperItemIdPrefix = "paper_";

    /// <summary>일차 종료 초기화가 끝난 뒤 호출됩니다. (화장실 선반처럼 따로 관리하는 기믹이 자기 상태를 되돌릴 때 사용)</summary>
    public event Action ItemsResetForNewDay;

    private readonly Dictionary<string, ItemPositionRecord> recordBySlotId =
        new Dictionary<string, ItemPositionRecord>();

    // 다른 기믹이 직접 관리하는 자리 (검사는 하지만 초기화는 그 기믹이 함)
    private readonly HashSet<string> externallyManagedSlotIds = new HashSet<string>();

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

        // 예전 씬 설정에 남아 있는 황금 열쇠 예외를 지웁니다. 황금 열쇠도 점검·초기화(서랍으로 복귀) 대상입니다.
        ignoredItemIds.RemoveAll(id => id != null && id.Trim() == GoldenKeyItemId);
        resetExcludedItemIds.RemoveAll(id => id != null && id.Trim() == GoldenKeyItemId);

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

        // 금고를 연 뒤에는 종이가 다시 나타나지 않습니다.
        if (papersRemovedPermanently && IsPaperItem(pickup.ItemData.ItemId))
        {
            Destroy(pickup.gameObject);
            return;
        }

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

    /// <summary>
    /// 전화 부스로 경찰 신고에 성공했으면(다음 날 탈출 엔딩 대기) 일차 끝 점검을 하지 않습니다.
    /// (점검에 걸려 초기화되면 경찰을 다시 부를 수 없는 문제, 사용자 요청 2026-11-03)
    /// </summary>
    public static bool SkipDayEndCheck =>
        EXOD.Walk.PhoneBoothProgress.Reported && !EXOD.Walk.PhoneBoothProgress.EndingShown;

    private void HandleDayChanged(int newDay)
    {
        if (SkipDayEndCheck)
            return;

        int misplacedCount = CheckItemsOnDayEnd();

        // 하나라도 틀렸을 때만 초기화합니다. 모두 제자리면 종이 등 퀵슬롯은 그대로 둡니다.
        if (misplacedCount > 0 && resetItemsOnDayEnd)
            ResetItemsForNewDay();
    }

    /// <summary>
    /// 모든 종이(paper_)를 영구히 없앱니다. 퀵슬롯·맵 어디에 있든 사라지고, 일차 초기화로도 돌아오지 않습니다.
    /// (화장실 금고 잠금판이 풀렸을 때 RestroomGimmick이 호출)
    /// </summary>
    public void RemovePapersPermanently()
    {
        papersRemovedPermanently = true;

        QuickSlotInventory inventory = QuickSlotInventory.Instance != null
            ? QuickSlotInventory.Instance
            : FindFirstObjectByType<QuickSlotInventory>();
        if (inventory != null)
        {
            foreach (QuickSlotEntry entry in inventory.Entries.ToArray())
            {
                if (entry?.ItemData != null && IsPaperItem(entry.ItemData.ItemId))
                    inventory.TryRemove(entry);
            }
        }

        foreach (ItemPositionRecord record in positionRecords.ToArray())
        {
            record.RefreshOccupancy();
            if (record.IsOccupied && IsPaperItem(record.CurrentItemId))
            {
                WorldItemPickup paperPickup = record.CurrentPickup;
                record.SetOccupant(null);
                Destroy(paperPickup.gameObject);
            }

            if (IsPaperItem(record.OriginalItemId))
            {
                positionRecords.Remove(record);
                recordBySlotId.Remove(record.SlotId);
            }
        }

        foreach (WorldItemPickup pickup in FindObjectsByType<WorldItemPickup>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (pickup.ItemData != null && IsPaperItem(pickup.ItemData.ItemId))
                Destroy(pickup.gameObject);
        }

        Debug.Log("[ItemPositionManager] 종이를 모두 없앴습니다.", this);
        PapersRemovedPermanently?.Invoke();
    }

    /// <summary>이 자리는 다른 기믹이 직접 관리합니다. 제자리 검사에는 포함되지만 초기화는 하지 않습니다.</summary>
    public void MarkExternallyManaged(string slotId)
    {
        if (!string.IsNullOrWhiteSpace(slotId))
            externallyManagedSlotIds.Add(slotId);
    }

    public bool IsPaperItem(string itemId)
    {
        return !string.IsNullOrEmpty(itemId) &&
               !string.IsNullOrEmpty(paperItemIdPrefix) &&
               itemId.StartsWith(paperItemIdPrefix, StringComparison.Ordinal);
    }

    /// <summary>이 아이템의 처음 자리가 등록되어 있는지 (초기화 때 그 자리로 돌아가는지)</summary>
    public bool HasResettableRecordFor(string itemId)
    {
        foreach (ItemPositionRecord record in positionRecords)
        {
            if (record.OriginalItemId == itemId &&
                !externallyManagedSlotIds.Contains(record.SlotId) &&
                !IsResetExcluded(itemId))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsResetExcluded(string itemId)
    {
        foreach (string excluded in resetExcludedItemIds)
        {
            if (!string.IsNullOrWhiteSpace(excluded) && excluded.Trim() == itemId)
                return true;
        }

        return false;
    }

    /// <summary>
    /// 일차 종료 초기화 (검사에서 틀린 게 있을 때만 실행): 모든 아이템을 처음 자리로 되돌리고(퀵슬롯에서도 빠짐),
    /// 퀵슬롯에 있는 종이(paper_)는 없애서 처음 자리에서 다시 얻을 수 있게 합니다.
    /// </summary>
    [ContextMenu("일차 종료 초기화 테스트")]
    public void ResetItemsForNewDay()
    {
        QuickSlotInventory inventory = QuickSlotInventory.Instance != null
            ? QuickSlotInventory.Instance
            : FindFirstObjectByType<QuickSlotInventory>();
        HashSet<string> restoredItemIds = new HashSet<string>();

        foreach (ItemPositionRecord record in positionRecords.ToArray())
        {
            record.RefreshOccupancy();
            string originalId = record.OriginalItemId;
            if (string.IsNullOrEmpty(originalId) ||
                externallyManagedSlotIds.Contains(record.SlotId) ||
                IsResetExcluded(originalId))
            {
                continue;
            }

            if (record.IsOccupied && record.CurrentItemId == originalId)
                continue;

            // 다른 아이템이 놓여 있으면 치웁니다. 처음 자리가 따로 없는 아이템은 퀵슬롯으로 돌려줍니다.
            if (record.IsOccupied)
            {
                WorldItemPickup wrongPickup = record.CurrentPickup;
                ItemData wrongItem = wrongPickup.ItemData;
                record.SetOccupant(null);
                Destroy(wrongPickup.gameObject);

                if (wrongItem != null && inventory != null &&
                    !HasResettableRecordFor(wrongItem.ItemId) && !IsPaperItem(wrongItem.ItemId))
                {
                    inventory.TryAdd(wrongItem);
                }
            }

            if (record.OriginalItem == null)
                continue;

            WorldItemPickup restored = CreatePlacedPickup(record.OriginalItem, record);
            record.SetOccupant(restored);
            restoredItemIds.Add(originalId);
        }

        // 처음 자리로 돌아간 아이템과 종이는 퀵슬롯에서 뺍니다.
        if (inventory != null)
        {
            foreach (QuickSlotEntry entry in inventory.Entries.ToArray())
            {
                string itemId = entry?.ItemData != null ? entry.ItemData.ItemId : string.Empty;
                if (restoredItemIds.Contains(itemId) || IsPaperItem(itemId))
                    inventory.TryRemove(entry);
            }
        }

        // 처음 자리가 등록되지 않은 점검 대상 아이템은 되돌릴 곳이 없습니다. (새 아이템을 맵에 WorldItemPickup으로 놓지 않은 경우)
        if (inventory != null)
        {
            foreach (QuickSlotEntry entry in inventory.Entries)
            {
                string itemId = entry?.ItemData != null ? entry.ItemData.ItemId : string.Empty;
                if (!string.IsNullOrEmpty(itemId) && !IsPaperItem(itemId) && !IsAllowedInInventory(itemId) &&
                    !HasCheckedRecordFor(itemId) && !IsIgnored(itemId))
                {
                    Debug.LogWarning($"[ItemPositionManager] '{itemId}'은(는) 처음 자리가 등록되지 않아 초기화하지 못했습니다. 맵에 WorldItemPickup으로 놓아 주세요.", this);
                }
            }
        }

        Debug.Log($"[ItemPositionManager] 일차 종료 초기화: {restoredItemIds.Count}개 자리 복구, 종이 초기화", this);
        ItemsResetForNewDay?.Invoke();
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

    private readonly List<string> lastDayEndMisplacedNames = new List<string>();

    /// <summary>마지막 일차 끝 점검에서 제자리에 없던 물건 이름 (일차가 넘어간 뒤 대사로 알려 줄 때 사용)</summary>
    public IReadOnlyList<string> LastDayEndMisplacedNames => lastDayEndMisplacedNames;

    private void RememberMisplacedNames(List<ItemPositionRecord> misplaced, List<string> misplacedInInventory)
    {
        lastDayEndMisplacedNames.Clear();

        foreach (ItemPositionRecord record in misplaced)
            AddMisplacedName(record.OriginalItem != null ? record.OriginalItem.DisplayName : record.OriginalItemId);

        QuickSlotInventory inventory = QuickSlotInventory.Instance != null
            ? QuickSlotInventory.Instance
            : FindFirstObjectByType<QuickSlotInventory>();
        foreach (string itemId in misplacedInInventory)
        {
            string name = itemId;
            if (inventory != null)
            {
                foreach (QuickSlotEntry entry in inventory.Entries)
                {
                    if (entry?.ItemData != null && entry.ItemData.ItemId == itemId)
                    {
                        name = entry.ItemData.DisplayName;
                        break;
                    }
                }
            }
            AddMisplacedName(name);
        }
    }

    private void AddMisplacedName(string name)
    {
        if (!string.IsNullOrWhiteSpace(name) && !lastDayEndMisplacedNames.Contains(name))
            lastDayEndMisplacedNames.Add(name);
    }

    public bool AreAllItemsInOriginalPlace()
    {
        return GetMisplacedRecords().Count == 0 && GetMisplacedInventoryItemIds().Count == 0;
    }

    /// <summary>
    /// 제자리 검사를 하고, 틀린 게 있으면 하트를 줄입니다. 틀린 위치 개수를 돌려줍니다.
    /// 하트가 0이 되면 PlayerFaintController가 게임 오버를 띄웁니다.
    /// </summary>
    public int CheckItemsOnDayEnd()
    {
        List<ItemPositionRecord> misplaced = GetMisplacedRecords();
        List<string> misplacedInInventory = GetMisplacedInventoryItemIds();
        int misplacedCount = misplaced.Count + misplacedInInventory.Count;
        RememberMisplacedNames(misplaced, misplacedInInventory);

        if (misplacedCount == 0)
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

        foreach (string itemId in misplacedInInventory)
            Debug.Log($"[ItemPositionManager] 제자리 아님: '{itemId}' 이(가) 퀵슬롯에 있음", this);

        HeartManager hearts = FindFirstObjectByType<HeartManager>(FindObjectsInactive.Include);
        if (hearts == null)
        {
            Debug.LogWarning("[ItemPositionManager] HeartManager를 찾을 수 없어 하트를 줄이지 못했습니다.", this);
        }
        else
        {
            int penalty = loseHeartPerWrongItem ? misplacedCount : 1;
            for (int i = 0; i < penalty; i++)
                hearts.HPM();
        }

        onItemsMisplaced?.Invoke();
        return misplacedCount;
    }

    /// <summary>
    /// 퀵슬롯에 들고 있는 아이템 중 제자리가 아닌 것으로 치는 아이템 ID 목록입니다.
    /// 종이(paper_), 기믹에 쓰면 사라지는 아이템(곰돌이 팔다리), 냉장고 비밀번호 쪽지, 허용 목록의 아이템만 들고 있어도 괜찮고,
    /// 나머지는 모두 제자리가 아닌 것으로 봅니다.
    /// (처음 자리가 등록된 아이템은 그 자리가 비어 있는 것으로 이미 세므로 여기서는 빼서 두 번 세지 않습니다)
    /// </summary>
    public List<string> GetMisplacedInventoryItemIds()
    {
        List<string> result = new List<string>();
        QuickSlotInventory inventory = QuickSlotInventory.Instance != null
            ? QuickSlotInventory.Instance
            : FindFirstObjectByType<QuickSlotInventory>();
        if (inventory == null)
            return result;

        foreach (QuickSlotEntry entry in inventory.Entries)
        {
            string itemId = entry?.ItemData != null ? entry.ItemData.ItemId : string.Empty;
            if (string.IsNullOrEmpty(itemId) || IsPaperItem(itemId) || IsAllowedInInventory(itemId))
                continue;

            if (HasCheckedRecordFor(itemId))
                continue;

            result.Add(itemId);
        }

        return result;
    }

    private bool HasCheckedRecordFor(string itemId)
    {
        foreach (ItemPositionRecord record in positionRecords)
        {
            if (record.OriginalItemId == itemId && !IsIgnored(itemId))
                return true;
        }

        return false;
    }

    private bool IsAllowedInInventory(string itemId)
    {
        if (Array.IndexOf(AlwaysAllowedInInventoryItemIds, itemId) >= 0)
            return true;

        foreach (string allowed in inventoryAllowedItemIds)
        {
            if (!string.IsNullOrWhiteSpace(allowed) && allowed.Trim() == itemId)
                return true;
        }

        return false;
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
