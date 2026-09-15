using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public sealed class ItemInteractionTarget : MonoBehaviour
{
    [SerializeField] private bool acceptAnyItem;
    [SerializeField] private List<ItemData> acceptedItems = new List<ItemData>();
    [SerializeField] private UnityEvent onItemAccepted;
    [SerializeField] private UnityEvent onItemRejected;

    public event Action<ItemData> ItemAccepted;
    public event Action<ItemData> ItemRejected;

    public bool CanAccept(ItemData itemData)
    {
        return itemData != null && (acceptAnyItem || acceptedItems.Contains(itemData));
    }

    public bool TryInteract(QuickSlotEntry entry)
    {
        if (entry == null || !CanAccept(entry.ItemData))
        {
            NotifyRejected(entry == null ? null : entry.ItemData);
            return false;
        }

        onItemAccepted?.Invoke();
        ItemAccepted?.Invoke(entry.ItemData);
        return true;
    }

    public void NotifyRejected(ItemData itemData)
    {
        onItemRejected?.Invoke();
        ItemRejected?.Invoke(itemData);
    }
}
