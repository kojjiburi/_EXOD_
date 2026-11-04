using System;
using UnityEngine;

[Serializable]
public sealed class QuickSlotEntry
{
    [SerializeField] private string instanceId;
    [SerializeField] private ItemData itemData;
    [SerializeField] private long acquiredSequence;

    public string InstanceId => instanceId;
    public ItemData ItemData => itemData;
    public long AcquiredSequence => acquiredSequence;

    public QuickSlotEntry(ItemData itemData, long acquiredSequence)
    {
        instanceId = Guid.NewGuid().ToString("N");
        this.itemData = itemData;
        this.acquiredSequence = acquiredSequence;
    }
}
