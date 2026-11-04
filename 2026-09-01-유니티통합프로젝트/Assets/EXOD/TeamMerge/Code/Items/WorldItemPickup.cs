using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class WorldItemPickup : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private ItemData itemData;
    [SerializeField] private bool destroyOnPickup = true;
    [SerializeField] private UnityEvent onPickedUp;
    [SerializeField] private UnityEvent onInventoryFull;

    [Header("Recorded Start Position (Runtime)")]
    [SerializeField] private string positionSlotId;
    [SerializeField] private Vector3 originalPosition;
    [SerializeField] private string originalItemId;

    public ItemData ItemData => itemData;
    public string PositionSlotId => positionSlotId;
    public Vector3 OriginalPosition => originalPosition;
    public string OriginalItemId => originalItemId;

    private void Start()
    {
        if (!Application.isPlaying)
            return;

        ItemPositionManager.GetOrCreate()?.RegisterInitialPickup(this);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            TryPickup();
    }

    public bool TryPickup()
    {
        QuickSlotInventory inventory = QuickSlotInventory.Instance;
        if (inventory == null)
            inventory = FindFirstObjectByType<QuickSlotInventory>();

        if (inventory == null)
        {
            Debug.LogError("[WorldItemPickup] QuickSlotInventory was not found.", this);
            return false;
        }

        if (itemData == null)
        {
            Debug.LogError("[WorldItemPickup] ItemData is not assigned.", this);
            return false;
        }

        if (!inventory.TryAdd(itemData))
        {
            onInventoryFull?.Invoke();
            return false;
        }

        ItemPositionManager.Instance?.NotifyPickedUp(this);
        onPickedUp?.Invoke();

        if (destroyOnPickup)
            Destroy(gameObject);
        else
            gameObject.SetActive(false);

        return true;
    }

    public void Configure(ItemData data, bool shouldDestroyOnPickup = true)
    {
        itemData = data;
        destroyOnPickup = shouldDestroyOnPickup;
    }

    internal void BindPositionSlot(
        string slotId,
        Vector3 startPosition,
        string startItemId)
    {
        positionSlotId = slotId;
        originalPosition = startPosition;
        originalItemId = startItemId ?? string.Empty;
    }
}
