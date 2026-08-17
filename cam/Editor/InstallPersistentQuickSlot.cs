#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class InstallPersistentQuickSlot
{
    private const string Stage1Path = "Assets/Scenes/Stage1.unity";
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
    private const string ResourcesFolder = "Assets/Resources";
    private const string PrefabPath = ResourcesFolder + "/QuickSlotSystem.prefab";

    [MenuItem("Tools/EXOD/Install Persistent Quick Slot")]
    public static void Install()
    {
        if (!System.IO.File.Exists(Stage1Path) || !System.IO.File.Exists(SampleScenePath))
        {
            Debug.LogError("[QuickSlot Installer] Stage1 또는 SampleScene을 찾지 못했습니다.");
            return;
        }

        Scene stage1 = EditorSceneManager.OpenScene(Stage1Path, OpenSceneMode.Single);
        QuickSlotController sourceController = Object.FindFirstObjectByType<QuickSlotController>();
        if (sourceController == null)
        {
            Debug.LogError("[QuickSlot Installer] Stage1에서 QuickSlotController를 찾지 못했습니다.");
            return;
        }

        GameObject prefab = CreateOrUpdatePrefab(sourceController);
        ReplaceSceneQuickSlot(stage1, prefab);
        EditorSceneManager.SaveScene(stage1);

        Scene sampleScene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
        ReplaceSceneQuickSlot(sampleScene, prefab);
        EditorSceneManager.SaveScene(sampleScene);

        EnableSampleSceneInBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
        Debug.Log("[QuickSlot Installer] Stage1, SampleScene 및 이후 씬용 영구 퀵슬롯 설치 완료.");
    }

    private static GameObject CreateOrUpdatePrefab(QuickSlotController sourceController)
    {
        EnsureFolder(ResourcesFolder);

        GameObject temporaryRoot = new GameObject(
            "QuickSlotSystem",
            typeof(QuickSlotPersistentRoot));

        GameObject canvasObject = new GameObject(
            "QuickSlotCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(temporaryRoot.transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 500;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject quickSlotObject = Object.Instantiate(sourceController.gameObject, canvasObject.transform, false);
        quickSlotObject.name = "QuickSlot_1";
        QuickSlotController controller = quickSlotObject.GetComponent<QuickSlotController>();

        RectTransform sourceDragLayer = GetObjectReference<RectTransform>(sourceController, "dragLayer");
        GameObject dragLayerObject;
        if (sourceDragLayer != null)
        {
            dragLayerObject = Object.Instantiate(sourceDragLayer.gameObject, canvasObject.transform, false);
            dragLayerObject.name = "QuickSlotDragLayer";
        }
        else
        {
            dragLayerObject = new GameObject("QuickSlotDragLayer", typeof(RectTransform));
            dragLayerObject.transform.SetParent(canvasObject.transform, false);
            RectTransform generatedDragLayer = (RectTransform)dragLayerObject.transform;
            generatedDragLayer.anchorMin = Vector2.zero;
            generatedDragLayer.anchorMax = Vector2.one;
            generatedDragLayer.offsetMin = Vector2.zero;
            generatedDragLayer.offsetMax = Vector2.zero;
        }

        GameObject inventoryObject = new GameObject("QuickSlotInventory");
        inventoryObject.transform.SetParent(temporaryRoot.transform, false);
        QuickSlotInventory inventory = inventoryObject.AddComponent<QuickSlotInventory>();

        RectTransform slotContainer = GetObjectReference<RectTransform>(controller, "slotContainer");
        controller.Configure(
            inventory,
            slotContainer,
            (RectTransform)dragLayerObject.transform,
            null);

        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(temporaryRoot, PrefabPath);
        Object.DestroyImmediate(temporaryRoot);
        return savedPrefab;
    }

    private static void ReplaceSceneQuickSlot(Scene scene, GameObject prefab)
    {
        foreach (QuickSlotPersistentRoot root in FindSceneObjects<QuickSlotPersistentRoot>(scene))
            Object.DestroyImmediate(root.gameObject);

        foreach (QuickSlotController controller in FindSceneObjects<QuickSlotController>(scene))
            Object.DestroyImmediate(controller.gameObject);

        foreach (QuickSlotInventory inventory in FindSceneObjects<QuickSlotInventory>(scene))
            Object.DestroyImmediate(inventory);

        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            RectTransform[] rects = rootObject.GetComponentsInChildren<RectTransform>(true);
            foreach (RectTransform rect in rects.Where(rect => rect.name == "QuickSlotDragLayer").ToArray())
                Object.DestroyImmediate(rect.gameObject);
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.name = "QuickSlotSystem";
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static T GetObjectReference<T>(Object target, string propertyName) where T : Object
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        return property == null ? null : property.objectReferenceValue as T;
    }

    private static IEnumerable<T> FindSceneObjects<T>(Scene scene) where T : Component
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true));
    }

    private static void EnableSampleSceneInBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
        int index = scenes.FindIndex(scene => scene.path == SampleScenePath);

        if (index >= 0)
            scenes[index] = new EditorBuildSettingsScene(SampleScenePath, true);
        else
            scenes.Add(new EditorBuildSettingsScene(SampleScenePath, true));

        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string parent = System.IO.Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
        string name = System.IO.Path.GetFileName(folderPath);
        if (!string.IsNullOrEmpty(parent))
            EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent, name);
    }
}
#endif
