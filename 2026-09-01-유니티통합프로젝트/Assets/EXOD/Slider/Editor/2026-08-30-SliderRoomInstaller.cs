#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SliderRoomInstaller20260830
{
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
    private const string IntegratedScenePath =
        "Assets/Scenes/2026-08-17-EXOD-완전통합.unity";

    private static readonly string[] ScenePaths =
        { IntegratedScenePath, SampleScenePath };

    private const string LivingRoomPath =
        "Assets/EXOD/Backgrounds/2026-08-30-거실보완배경.png";
    private const string KitchenPath =
        "Assets/EXOD/Backgrounds/2026-08-30-주방배경.png";
    private const string HallwayPath =
        "Assets/EXOD/Backgrounds/2026-08-30-복도배경.png";
    private const string BathroomPath =
        "Assets/EXOD/Backgrounds/2026-08-30-화장실배경.png";
    private const string OutdoorPath =
        "Assets/EXOD/Backgrounds/2026-08-30-산책로배경-5760x1080-간격정확보정-v5.png";

    [InitializeOnLoadMethod]
    private static void QueueAutomaticInstall()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            Scene activeScene = EditorSceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !IsTargetScene(activeScene.path))
                return;

            SliderManager sliderManager =
                Object.FindFirstObjectByType<SliderManager>(FindObjectsInactive.Include);
            RoomManager roomManager =
                Object.FindFirstObjectByType<RoomManager>(FindObjectsInactive.Include);

            if (sliderManager != null && roomManager != null &&
                HasAllBackgrounds(sliderManager))
                return;

            Install();
        };
    }

    [MenuItem("EXOD/슬라이더/배경 및 장소 이동 설치")]
    public static void Install()
    {
        ImportBackground(LivingRoomPath);
        ImportBackground(KitchenPath);
        ImportBackground(HallwayPath);
        ImportBackground(BathroomPath);
        ImportBackground(OutdoorPath);

        Scene originalScene = EditorSceneManager.GetActiveScene();
        string originalScenePath = originalScene.IsValid() ? originalScene.path : string.Empty;

        foreach (string scenePath in ScenePaths)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null)
                ConfigureScene(scenePath);
        }

        if (!string.IsNullOrEmpty(originalScenePath) &&
            AssetDatabase.LoadAssetAtPath<SceneAsset>(originalScenePath) != null)
        {
            EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("EXOD: 존재하는 대상 씬에 장소 이동 슬라이더 배경 연결 완료.");
    }

    private static void ConfigureScene(string scenePath)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        SliderManager sliderManager =
            Object.FindFirstObjectByType<SliderManager>(FindObjectsInactive.Include);

        if (sliderManager == null)
            throw new MissingReferenceException(
                $"{scenePath}에서 SliderManager를 찾을 수 없습니다.");

        SerializedObject sliderData = new(sliderManager);
        SetSprite(sliderData, "livingRoomSprite", LivingRoomPath);
        SetSprite(sliderData, "kitchenSprite", KitchenPath);
        SetSprite(sliderData, "hallwaySprite", HallwayPath);
        SetSprite(sliderData, "bathroomSprite", BathroomPath);
        SetSprite(sliderData, "outdoorSprite", OutdoorPath);
        sliderData.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(sliderManager);

        RoomManager roomManager = EnsureRoomManager(scene);
        roomManager.침실_해금 = true;
        roomManager.거실_해금 = true;
        roomManager.주방_해금 = true;
        EditorUtility.SetDirty(roomManager);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    public static void InstallBatch()
    {
        Install();
    }

    private static void ImportBackground(string assetPath)
    {
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
            throw new MissingReferenceException($"배경 이미지 임포터를 찾을 수 없습니다: {assetPath}");

        bool changed = false;
        changed |= SetIfDifferent(importer.textureType, TextureImporterType.Sprite,
            value => importer.textureType = value);
        changed |= SetIfDifferent(importer.spriteImportMode, SpriteImportMode.Single,
            value => importer.spriteImportMode = value);
        changed |= SetIfDifferent(importer.mipmapEnabled, false,
            value => importer.mipmapEnabled = value);
        changed |= SetIfDifferent(importer.alphaIsTransparency, true,
            value => importer.alphaIsTransparency = value);
        changed |= SetIfDifferent(importer.wrapMode, TextureWrapMode.Clamp,
            value => importer.wrapMode = value);
        changed |= SetIfDifferent(importer.filterMode, FilterMode.Bilinear,
            value => importer.filterMode = value);
        changed |= SetIfDifferent(importer.maxTextureSize, 8192,
            value => importer.maxTextureSize = value);

        if (changed)
            importer.SaveAndReimport();
    }

    private static bool SetIfDifferent<T>(
        T current,
        T expected,
        System.Action<T> setter)
    {
        if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(current, expected))
            return false;

        setter(expected);
        return true;
    }

    private static void SetSprite(
        SerializedObject sliderData,
        string propertyName,
        string assetPath)
    {
        SerializedProperty property = sliderData.FindProperty(propertyName);
        if (property == null)
            throw new System.MissingFieldException(
                $"SliderManager에 {propertyName} 필드가 없습니다.");

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sprite == null)
            throw new MissingReferenceException($"배경 Sprite를 불러오지 못했습니다: {assetPath}");

        property.objectReferenceValue = sprite;
    }

    private static bool HasAllBackgrounds(SliderManager sliderManager)
    {
        SerializedObject data = new(sliderManager);
        return HasObject(data, "livingRoomSprite") &&
               HasObject(data, "kitchenSprite") &&
               HasObject(data, "hallwaySprite") &&
               HasObject(data, "bathroomSprite") &&
               HasObject(data, "outdoorSprite");
    }

    private static bool HasObject(SerializedObject data, string propertyName)
    {
        SerializedProperty property = data.FindProperty(propertyName);
        return property != null && property.objectReferenceValue != null;
    }

    private static bool IsTargetScene(string scenePath)
    {
        foreach (string targetPath in ScenePaths)
        {
            if (scenePath == targetPath)
                return true;
        }

        return false;
    }

    private static RoomManager EnsureRoomManager(Scene scene)
    {
        RoomManager existing =
            Object.FindFirstObjectByType<RoomManager>(FindObjectsInactive.Include);
        if (existing != null)
            return existing;

        GameObject managerObject = FindSceneObject(scene, "RoomManager");
        if (managerObject == null)
            managerObject = new GameObject("RoomManager");

        return managerObject.AddComponent<RoomManager>();
    }

    private static GameObject FindSceneObject(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform found = FindRecursive(root.transform, objectName);
            if (found != null)
                return found.gameObject;
        }

        return null;
    }

    private static Transform FindRecursive(Transform current, string objectName)
    {
        if (current.name == objectName)
            return current;

        foreach (Transform child in current)
        {
            Transform found = FindRecursive(child, objectName);
            if (found != null)
                return found;
        }

        return null;
    }
}
#endif
