using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ItemPositionManagerSetup
{
    [MenuItem("EXOD123/Quick Slot/Setup Item Position Manager")]
    private static void SetupManager()
    {
        ItemPositionManager manager =
            Object.FindFirstObjectByType<ItemPositionManager>(FindObjectsInactive.Include);

        if (manager == null)
        {
            GameObject managerObject = new GameObject("ItemPositionManager");
            Undo.RegisterCreatedObjectUndo(managerObject, "Create Item Position Manager");
            manager = Undo.AddComponent<ItemPositionManager>(managerObject);
            EditorSceneManager.MarkSceneDirty(managerObject.scene);
        }

        Selection.activeGameObject = manager.gameObject;
        EditorGUIUtility.PingObject(manager.gameObject);
        Debug.Log("[ItemPositionManagerSetup] ItemPositionManager is ready.", manager);
    }

    [MenuItem("EXOD123/Quick Slot/Test Place Last Item In First Empty Position")]
    private static void TestPlaceLastItem()
    {
        ItemPositionManager manager = ItemPositionManager.Instance;
        QuickSlotInventory inventory = QuickSlotInventory.Instance;

        if (manager == null || inventory == null || inventory.Count == 0)
        {
            Debug.LogWarning(
                "[ItemPositionManagerSetup] Enter Play Mode and pick up at least one item first.");
            return;
        }

        QuickSlotEntry entry = inventory.Entries[inventory.Count - 1];
        ItemPositionRecord emptyPosition =
            manager.PositionRecords.FirstOrDefault(record => !record.IsOccupied);

        if (entry == null || entry.ItemData == null || emptyPosition == null)
        {
            Debug.LogWarning(
                "[ItemPositionManagerSetup] No item or empty saved position was found.");
            return;
        }

        string originalItemId = emptyPosition.OriginalItemId;
        string placedItemId = entry.ItemData.ItemId;

        if (!manager.TryPlaceItem(entry.ItemData, emptyPosition.OriginalPosition) ||
            !inventory.TryRemove(entry))
        {
            Debug.LogError(
                "[ItemPositionManagerSetup] Test placement failed.");
            return;
        }

        Debug.Log(
            $"[ItemPositionManagerSetup] Placed '{placedItemId}' in a position " +
            $"originally recorded for '{originalItemId}'.");
    }
}
