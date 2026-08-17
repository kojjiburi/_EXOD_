using UnityEngine;

[CreateAssetMenu(fileName = "NewItem", menuName = "EXOD123/Items/Item Data")]
public sealed class ItemData : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string itemId;
    [SerializeField] private string displayName;

    [Header("Quick Slot")]
    [SerializeField] private Sprite icon;
    [SerializeField] private bool consumeOnSuccessfulUse = true;

    public string ItemId => itemId == null ? string.Empty : itemId.Trim();
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public Sprite Icon => icon;
    public bool ConsumeOnSuccessfulUse => consumeOnSuccessfulUse;
}
