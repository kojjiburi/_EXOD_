using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class QuickSlotInventory : MonoBehaviour
{
    public const int DefaultCapacity = 10;

    public static QuickSlotInventory Instance { get; private set; }

    [SerializeField, Min(1)] private int capacity = DefaultCapacity;
    [SerializeField] private List<QuickSlotEntry> entries = new List<QuickSlotEntry>();
    [SerializeField] private long nextAcquiredSequence;

    public event Action Changed;

    public int Capacity => capacity;
    public int Count => entries.Count;
    public IReadOnlyList<QuickSlotEntry> Entries => entries;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[QuickSlotInventory] Duplicate inventory was disabled.", this);
            enabled = false;
            return;
        }

        Instance = this;
        RemoveInvalidEntries();
        SortByAcquiredSequence();

        if (entries.Count > 0)
        {
            long nextFromData = entries.Max(entry => entry.AcquiredSequence) + 1;
            nextAcquiredSequence = Math.Max(nextAcquiredSequence, nextFromData);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnValidate()
    {
        capacity = Mathf.Max(1, capacity);
        nextAcquiredSequence = Math.Max(0, nextAcquiredSequence);
    }

    public bool TryAdd(ItemData itemData)
    {
        return TryAdd(itemData, out _);
    }

    public bool TryAdd(ItemData itemData, out QuickSlotEntry addedEntry)
    {
        addedEntry = null;

        if (itemData == null || entries.Count >= capacity)
            return false;

        addedEntry = new QuickSlotEntry(itemData, nextAcquiredSequence++);
        entries.Add(addedEntry);
        SortByAcquiredSequence();
        Changed?.Invoke();
        return true;
    }

    public bool TryRemove(QuickSlotEntry entry)
    {
        if (entry == null)
            return false;

        int index = entries.FindIndex(candidate =>
            candidate != null && candidate.InstanceId == entry.InstanceId);

        if (index < 0)
            return false;

        entries.RemoveAt(index);
        SortByAcquiredSequence();
        Changed?.Invoke();
        return true;
    }

    public bool Contains(QuickSlotEntry entry)
    {
        return entry != null && entries.Any(candidate =>
            candidate != null && candidate.InstanceId == entry.InstanceId);
    }

    public void Clear()
    {
        if (entries.Count == 0)
            return;

        entries.Clear();
        Changed?.Invoke();
    }

    private void SortByAcquiredSequence()
    {
        entries.Sort((left, right) => left.AcquiredSequence.CompareTo(right.AcquiredSequence));
    }

    private void RemoveInvalidEntries()
    {
        entries.RemoveAll(entry => entry == null || entry.ItemData == null);
    }
}
