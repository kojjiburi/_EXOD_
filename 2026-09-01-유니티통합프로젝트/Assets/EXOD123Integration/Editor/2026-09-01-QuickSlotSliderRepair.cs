#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class QuickSlotSliderRepair20260901
{
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
    private const string QuickSlotPrefabPath =
        "Assets/EXOD123Integration/Resources/QuickSlotSystem.prefab";

    [MenuItem("EXOD123/2026-09-01 퀵슬롯 및 슬라이더 재적용")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Play Mode를 종료한 뒤 실행해야 합니다.");

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            QuickSlotPrefabPath);
        if (prefab == null)
            throw new MissingReferenceException(
                $"퀵슬롯 프리팹을 찾을 수 없습니다: {QuickSlotPrefabPath}");

        ValidateQuickSlotPrefab(prefab);

        Scene sampleScene = EditorSceneManager.OpenScene(
            SampleScenePath,
            OpenSceneMode.Single);

        QuickSlotPersistentRoot[] existingRoots =
            UnityEngine.Object.FindObjectsByType<QuickSlotPersistentRoot>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        foreach (QuickSlotPersistentRoot existingRoot in existingRoots)
        {
            if (existingRoot != null && existingRoot.gameObject.scene == sampleScene)
                UnityEngine.Object.DestroyImmediate(existingRoot.gameObject);
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(
            prefab,
            sampleScene);
        instance.name = "QuickSlotSystem";

        EditorSceneManager.MarkSceneDirty(sampleScene);
        EditorSceneManager.SaveScene(sampleScene);

        SliderRoomInstaller20260830.Install();

        Debug.Log(
            "EXOD: 0817 퀵슬롯 10칸 프리팹 재배치와 슬라이더 6개 장소 재연결 완료.");
    }

    public static void InstallBatch()
    {
        Install();
    }

    private static void ValidateQuickSlotPrefab(GameObject prefab)
    {
        Transform canvasTransform = prefab.transform.Find("QuickSlotCanvas");
        if (canvasTransform == null)
            throw new MissingReferenceException("QuickSlotCanvas가 없습니다.");

        if (canvasTransform.localScale != Vector3.one)
            throw new InvalidOperationException(
                $"QuickSlotCanvas 배율이 잘못되었습니다: {canvasTransform.localScale}");

        QuickSlotInventory inventory =
            prefab.GetComponentInChildren<QuickSlotInventory>(true);
        if (inventory == null || inventory.Capacity != 10)
            throw new InvalidOperationException("퀵슬롯 인벤토리 용량이 10이 아닙니다.");

        int[] slotIndices = prefab
            .GetComponentsInChildren<QuickSlotSlotView>(true)
            .Select(slot => slot.SlotIndex)
            .OrderBy(index => index)
            .ToArray();

        if (!slotIndices.SequenceEqual(Enumerable.Range(0, 10)))
            throw new InvalidOperationException(
                $"퀵슬롯 번호가 0~9로 구성되지 않았습니다: {string.Join(", ", slotIndices)}");

        if (prefab.GetComponent<QuickSlotPersistentRoot>() == null)
            throw new MissingReferenceException("QuickSlotPersistentRoot가 없습니다.");

        if (prefab.GetComponent<PlayerSystemsSceneBridge>() == null)
            throw new MissingReferenceException("PlayerSystemsSceneBridge가 없습니다.");
    }
}
#endif
