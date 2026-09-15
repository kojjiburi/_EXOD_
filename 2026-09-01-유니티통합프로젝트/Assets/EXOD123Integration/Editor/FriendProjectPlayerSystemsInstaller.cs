#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class FriendProjectPlayerSystemsInstaller
{
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
    private const string SourceStageScenePath =
        "Assets/EXOD/TeamMerge/Scenes/Stage1.unity";
    private const string StatusPrefabPath =
        "Assets/EXOD123Integration/Generated/EXOD_PlayerStatusSystem.prefab";
    private const string QuickSlotPrefabPath =
        "Assets/EXOD123Integration/Resources/QuickSlotSystem.prefab";
    private const string GoldenKeyAssetPath =
        "Assets/EXOD123Integration/Generated/GoldenKey.asset";
    private const string GoldenKeySpritePath =
        "Assets/EXOD/Gimmick1/Art/06_열쇠_아이콘.png";

    [MenuItem("EXOD123/친구 프로젝트에 플레이어 시스템 통합")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[EXOD123 통합] Play Mode를 종료한 뒤 실행하세요.");
            return;
        }

        if (!File.Exists(SampleScenePath) || !File.Exists(SourceStageScenePath))
        {
            Debug.LogError(
                "[EXOD123 통합] 친구 SampleScene 또는 TeamMerge Stage1을 찾지 못했습니다.");
            return;
        }

        Scene currentScene = SceneManager.GetActiveScene();
        if (currentScene.isDirty)
        {
            Debug.LogError(
                "[EXOD123 통합] 현재 씬에 저장하지 않은 변경이 있습니다. 먼저 저장한 뒤 다시 실행하세요.");
            return;
        }

        BackupSampleScene();
        EnsureGeneratedFolder();
        BuildStatusPrefab();
        PatchQuickSlotPrefab();
        IntegrateSampleScene();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(
            "[EXOD123 통합] 완료: DAY, HP, 얀데레 위험도, 아이템/식별, 퀵슬롯을 SampleScene에 연결했습니다. " +
            "친구 대화·조사·서랍·이동 코드는 변경하지 않았습니다.");
    }

    private static void BackupSampleScene()
    {
        string backupDirectory = Path.Combine(
            Directory.GetParent(Application.dataPath).FullName,
            "Library",
            "EXOD123IntegrationBackup");
        Directory.CreateDirectory(backupDirectory);

        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        string backupPath = Path.Combine(
            backupDirectory,
            $"SampleScene.before-player-systems.{stamp}.unity");
        File.Copy(SampleScenePath, backupPath, false);
        Debug.Log($"[EXOD123 통합] 원본 씬 백업: {backupPath}");
    }

    private static void EnsureGeneratedFolder()
    {
        const string root = "Assets/EXOD123Integration";
        const string generated = root + "/Generated";

        if (!AssetDatabase.IsValidFolder(root))
            AssetDatabase.CreateFolder("Assets", "EXOD123Integration");
        if (!AssetDatabase.IsValidFolder(generated))
            AssetDatabase.CreateFolder(root, "Generated");
    }

    private static void BuildStatusPrefab()
    {
        Scene sourceScene = EditorSceneManager.OpenScene(
            SourceStageScenePath,
            OpenSceneMode.Additive);

        GameObject sourceCanvas = FindRoot(sourceScene, "Canvas");
        GameObject sourceManager = FindRoot(sourceScene, "Manager");
        GameObject sourceCameraObject = FindRoot(sourceScene, "Main Camera");
        Camera sourceCamera = sourceCameraObject != null
            ? sourceCameraObject.GetComponent<Camera>()
            : null;
        if (sourceCanvas == null || sourceManager == null || sourceCamera == null)
        {
            EditorSceneManager.CloseScene(sourceScene, true);
            throw new InvalidOperationException(
                "TeamMerge Stage1에서 Canvas, Manager 또는 Main Camera를 찾지 못했습니다.");
        }

        GameObject exportRoot = new GameObject("__EXOD123_STATUS_EXPORT");
        SceneManager.MoveGameObjectToScene(exportRoot, sourceScene);
        sourceCanvas.transform.SetParent(exportRoot.transform, true);
        sourceManager.transform.SetParent(exportRoot.transform, true);

        GameObject clone = UnityEngine.Object.Instantiate(exportRoot);
        clone.name = "EXOD_PlayerStatusSystem";

        GameObject clonedCanvas = FindChild(clone.transform, "Canvas");
        GameObject clonedManager = FindChild(clone.transform, "Manager");
        RemoveChild(clonedCanvas, "QuickSlot_1");
        RemoveChild(clonedCanvas, "QuickSlotDragLayer");
        ConvertSpriteStatusToUi(clonedCanvas, sourceCamera);

        Canvas statusCanvas = clonedCanvas != null
            ? clonedCanvas.GetComponent<Canvas>()
            : null;
        if (statusCanvas != null)
        {
            statusCanvas.overrideSorting = true;
            statusCanvas.sortingOrder = 600;
        }

        QuickSlotInventory embeddedInventory =
            clonedManager != null ? clonedManager.GetComponent<QuickSlotInventory>() : null;
        if (embeddedInventory != null)
            UnityEngine.Object.DestroyImmediate(embeddedInventory);

        PrefabUtility.SaveAsPrefabAsset(clone, StatusPrefabPath);
        UnityEngine.Object.DestroyImmediate(clone);
        EditorSceneManager.CloseScene(sourceScene, true);
    }

    private static void ConvertSpriteStatusToUi(
        GameObject clonedCanvas,
        Camera sourceCamera)
    {
        if (clonedCanvas == null || sourceCamera == null)
            return;

        const float referenceWidth = 1920f;
        const float referenceHeight = 1080f;
        float referenceAspect = referenceWidth / referenceHeight;
        float pixelsPerWorldUnit =
            referenceHeight / (sourceCamera.orthographicSize * 2f);
        Vector3 canvasRuntimeOffset =
            new Vector3(referenceWidth * 0.5f, referenceHeight * 0.5f, 0f);

        SpriteRenderer[] renderers =
            clonedCanvas.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer.sprite == null)
                continue;

            if (renderer.name == "QuickSlot_1_Legacy")
            {
                renderer.gameObject.SetActive(false);
                continue;
            }

            if (!IsStatusSprite(renderer.name))
                continue;

            Vector3 authoredCanvasPosition = GetAuthoredCanvasPosition(
                renderer.transform,
                clonedCanvas.transform);
            Vector3 runtimeWorldPosition =
                authoredCanvasPosition + canvasRuntimeOffset;
            Vector3 viewPosition =
                sourceCamera.transform.InverseTransformPoint(runtimeWorldPosition);

            float screenX =
                (viewPosition.x /
                 (sourceCamera.orthographicSize * 2f * referenceAspect) + 0.5f) *
                referenceWidth;
            float screenY =
                (viewPosition.y /
                 (sourceCamera.orthographicSize * 2f) + 0.5f) *
                referenceHeight;

            // Center the danger icon inside the square at the far right of
            // the authored top status frame.
            if (renderer.name == "Yan")
                screenX -= 16f;

            Bounds worldBounds = renderer.bounds;
            Vector2 pixelSize = new Vector2(
                Mathf.Abs(worldBounds.size.x) * pixelsPerWorldUnit,
                Mathf.Abs(worldBounds.size.y) * pixelsPerWorldUnit);

            GameObject proxy = new GameObject(
                renderer.name + "_UI",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(SpriteRendererUiMirror));
            proxy.transform.SetParent(clonedCanvas.transform, false);

            RectTransform proxyRect = proxy.GetComponent<RectTransform>();
            proxyRect.anchorMin = Vector2.zero;
            proxyRect.anchorMax = Vector2.zero;
            proxyRect.pivot = new Vector2(0.5f, 0.5f);
            proxyRect.anchoredPosition = new Vector2(screenX, screenY);
            proxyRect.sizeDelta = pixelSize;

            Image image = proxy.GetComponent<Image>();
            image.sprite = renderer.sprite;
            image.color = renderer.color;
            image.preserveAspect = true;
            image.raycastTarget = false;

            SpriteRendererUiMirror mirror =
                proxy.GetComponent<SpriteRendererUiMirror>();
            mirror.Configure(renderer, image);

            // The large status frame is a background. Keep it behind the
            // existing DAY text, while hearts and the danger icon stay on top.
            if (renderer.name == "QuickSlot_0")
                proxy.transform.SetAsFirstSibling();
            else
                proxy.transform.SetAsLastSibling();

            renderer.enabled = false;
        }
    }

    private static bool IsStatusSprite(string objectName)
    {
        return objectName == "QuickSlot_0" ||
               objectName == "Heart1" ||
               objectName == "Heart2" ||
               objectName == "Heart3" ||
               objectName == "Heart4" ||
               objectName == "Heart5" ||
               objectName == "Yan";
    }

    private static Vector3 GetAuthoredCanvasPosition(
        Transform child,
        Transform canvasTransform)
    {
        Vector3 position = Vector3.zero;
        Transform current = child;
        while (current != null && current != canvasTransform)
        {
            RectTransform rect = current as RectTransform;
            if (rect != null)
            {
                position += new Vector3(
                    rect.anchoredPosition.x,
                    rect.anchoredPosition.y,
                    rect.localPosition.z);
            }
            else
            {
                position += current.localPosition;
            }

            current = current.parent;
        }

        return position;
    }

    private static void PatchQuickSlotPrefab()
    {
        ItemData goldenKey = GetOrCreateGoldenKey();
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(QuickSlotPrefabPath);
        try
        {
            Transform canvasTransform = prefabRoot.transform.Find("QuickSlotCanvas");
            PlayerSystemsSceneBridge bridge =
                prefabRoot.GetComponent<PlayerSystemsSceneBridge>();
            if (bridge == null)
                bridge = prefabRoot.AddComponent<PlayerSystemsSceneBridge>();

            bridge.Configure(
                canvasTransform != null ? canvasTransform.gameObject : null,
                goldenKey);
            EditorUtility.SetDirty(bridge);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, QuickSlotPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static ItemData GetOrCreateGoldenKey()
    {
        ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(GoldenKeyAssetPath);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ItemData>();
            AssetDatabase.CreateAsset(item, GoldenKeyAssetPath);
        }

        SerializedObject serializedItem = new SerializedObject(item);
        serializedItem.FindProperty("itemId").stringValue = "golden_key";
        serializedItem.FindProperty("displayName").stringValue = "황금 열쇠";
        serializedItem.FindProperty("icon").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Sprite>(GoldenKeySpritePath);
        serializedItem.FindProperty("consumeOnSuccessfulUse").boolValue = false;
        serializedItem.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(item);
        return item;
    }

    private static void IntegrateSampleScene()
    {
        Scene sampleScene = EditorSceneManager.OpenScene(
            SampleScenePath,
            OpenSceneMode.Single);

        if (FindRoot(sampleScene, "EXOD_PlayerStatusSystem") == null)
        {
            GameObject statusPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(StatusPrefabPath);
            PrefabUtility.InstantiatePrefab(statusPrefab, sampleScene);
        }

        if (FindFirstInScene<QuickSlotPersistentRoot>(sampleScene) == null)
        {
            GameObject quickSlotPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(QuickSlotPrefabPath);
            PrefabUtility.InstantiatePrefab(quickSlotPrefab, sampleScene);
        }

        if (FindFirstInScene<ItemPositionManager>(sampleScene) == null)
        {
            GameObject managerObject = new GameObject("ItemPositionManager");
            SceneManager.MoveGameObjectToScene(managerObject, sampleScene);
            managerObject.AddComponent<ItemPositionManager>();
        }

        EditorSceneManager.MarkSceneDirty(sampleScene);
        EditorSceneManager.SaveScene(sampleScene);
        Selection.activeObject = FindRoot(sampleScene, "EXOD_PlayerStatusSystem");
    }

    private static T FindFirstInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>(true);
            if (component != null)
                return component;
        }

        return null;
    }

    private static GameObject FindRoot(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == objectName)
                return root;
        }

        return null;
    }

    private static GameObject FindChild(Transform root, string objectName)
    {
        if (root == null)
            return null;

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == objectName)
                return child.gameObject;
        }

        return null;
    }

    private static void RemoveChild(GameObject root, string childName)
    {
        if (root == null)
            return;

        Transform child = root.transform.Find(childName);
        if (child != null)
            UnityEngine.Object.DestroyImmediate(child.gameObject);
    }
}
#endif
