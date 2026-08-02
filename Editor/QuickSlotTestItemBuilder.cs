using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public static class QuickSlotTestItemBuilder
{
    private const string RootFolder = "Assets/QuickSlotTest";
    private const string IconFolder = RootFolder + "/Icons";
    private const string DataFolder = RootFolder + "/ItemData";
    private const string ParentName = "QuickSlot_Test_Items";

    private readonly struct TestItemDefinition
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly string Symbol;
        public readonly Color32 Background;

        public TestItemDefinition(string id, string displayName, string symbol, Color32 background)
        {
            Id = id;
            DisplayName = displayName;
            Symbol = symbol;
            Background = background;
        }
    }

    private static readonly TestItemDefinition[] Definitions =
    {
        new TestItemDefinition("test_heart", "Heart", "heart", new Color32(220, 54, 76, 255)),
        new TestItemDefinition("test_1", "Number 1", "1", new Color32(53, 132, 228, 255)),
        new TestItemDefinition("test_2", "Number 2", "2", new Color32(44, 174, 108, 255)),
        new TestItemDefinition("test_3", "Number 3", "3", new Color32(235, 151, 45, 255)),
        new TestItemDefinition("test_4", "Number 4", "4", new Color32(139, 91, 214, 255)),
    };

    private static readonly Dictionary<char, string[]> DigitPatterns = new Dictionary<char, string[]>
    {
        {
            '1', new[]
            {
                "01100",
                "11100",
                "01100",
                "01100",
                "01100",
                "01100",
                "11111",
            }
        },
        {
            '2', new[]
            {
                "11110",
                "00011",
                "00011",
                "01110",
                "11000",
                "11000",
                "11111",
            }
        },
        {
            '3', new[]
            {
                "11110",
                "00011",
                "00011",
                "01110",
                "00011",
                "00011",
                "11110",
            }
        },
        {
            '4', new[]
            {
                "11011",
                "11011",
                "11011",
                "11111",
                "00011",
                "00011",
                "00011",
            }
        },
    };

    [MenuItem("EXOD123/Quick Slot/Create 5 Test Items")]
    public static void CreateFiveTestItems()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[QuickSlotTest] Exit Play Mode before creating test items.");
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || !activeScene.isLoaded)
        {
            Debug.LogError("[QuickSlotTest] No editable scene is open.");
            return;
        }

        EnsureFolder(RootFolder);
        EnsureFolder(IconFolder);
        EnsureFolder(DataFolder);

        Camera worldCamera = Camera.main != null
            ? Camera.main
            : Object.FindFirstObjectByType<Camera>();

        if (worldCamera == null)
        {
            Debug.LogError("[QuickSlotTest] A Camera is required.");
            return;
        }

        EnsurePointerClickSupport(worldCamera);

        GameObject previousParent = GameObject.Find(ParentName);
        if (previousParent != null)
            Undo.DestroyObjectImmediate(previousParent);

        GameObject parent = new GameObject(ParentName);
        Undo.RegisterCreatedObjectUndo(parent, "Create quick-slot test items");
        SceneManager.MoveGameObjectToScene(parent, activeScene);

        float spacing = CalculateSpacing(worldCamera);
        float startX = worldCamera.transform.position.x - spacing * 2f;
        float y = worldCamera.transform.position.y;

        for (int index = 0; index < Definitions.Length; index++)
        {
            TestItemDefinition definition = Definitions[index];
            Sprite icon = CreateOrUpdateIcon(definition);
            ItemData itemData = CreateOrUpdateItemData(definition, icon);
            CreatePickup(parent.transform, definition, itemData, icon, new Vector3(startX + spacing * index, y, 0f));
        }

        Selection.activeGameObject = parent;
        EditorSceneManager.MarkSceneDirty(activeScene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "[QuickSlotTest] Created Heart, 1, 2, 3, and 4 in the active scene. " +
            "Enter Play Mode and left-click them in order to test the quick slots.");
    }

    private static void CreatePickup(
        Transform parent,
        TestItemDefinition definition,
        ItemData itemData,
        Sprite icon,
        Vector3 position)
    {
        GameObject pickup = new GameObject(
            "TestPickup_" + definition.DisplayName.Replace(" ", string.Empty),
            typeof(SpriteRenderer),
            typeof(BoxCollider2D),
            typeof(WorldItemPickup));

        Undo.RegisterCreatedObjectUndo(pickup, "Create " + pickup.name);
        pickup.transform.SetParent(parent, false);
        pickup.transform.position = position;
        pickup.transform.localScale = Vector3.one * 1.15f;

        SpriteRenderer renderer = pickup.GetComponent<SpriteRenderer>();
        renderer.sprite = icon;
        renderer.sortingOrder = 1000;

        BoxCollider2D collider = pickup.GetComponent<BoxCollider2D>();
        collider.size = new Vector2(1.05f, 1.05f);

        SerializedObject pickupObject = new SerializedObject(pickup.GetComponent<WorldItemPickup>());
        pickupObject.FindProperty("itemData").objectReferenceValue = itemData;
        pickupObject.FindProperty("destroyOnPickup").boolValue = true;
        pickupObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static ItemData CreateOrUpdateItemData(
        TestItemDefinition definition,
        Sprite icon)
    {
        string assetPath = DataFolder + "/" + definition.Id + ".asset";
        ItemData itemData = AssetDatabase.LoadAssetAtPath<ItemData>(assetPath);

        if (itemData == null)
        {
            itemData = ScriptableObject.CreateInstance<ItemData>();
            AssetDatabase.CreateAsset(itemData, assetPath);
        }

        SerializedObject serializedData = new SerializedObject(itemData);
        serializedData.FindProperty("itemId").stringValue = definition.Id;
        serializedData.FindProperty("displayName").stringValue = definition.DisplayName;
        serializedData.FindProperty("icon").objectReferenceValue = icon;
        serializedData.FindProperty("consumeOnSuccessfulUse").boolValue = false;
        serializedData.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(itemData);
        return itemData;
    }

    private static Sprite CreateOrUpdateIcon(TestItemDefinition definition)
    {
        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[size * size];
        Color32 transparent = new Color32(0, 0, 0, 0);
        Color32 border = new Color32(24, 27, 34, 255);
        Color32 white = new Color32(250, 250, 250, 255);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - 63.5f;
                float dy = y - 63.5f;
                float radiusSquared = dx * dx + dy * dy;

                if (radiusSquared <= 60f * 60f)
                    pixels[y * size + x] = radiusSquared >= 55f * 55f ? border : definition.Background;
                else
                    pixels[y * size + x] = transparent;
            }
        }

        if (definition.Symbol == "heart")
            DrawHeart(pixels, size, border, white);
        else
            DrawDigit(pixels, size, definition.Symbol[0], border, white);

        texture.SetPixels32(pixels);
        texture.Apply();

        string assetPath = IconFolder + "/" + definition.Id + ".png";
        string absolutePath = Path.GetFullPath(assetPath);
        File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = 100f;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }

    private static void DrawHeart(
        Color32[] pixels,
        int size,
        Color32 shadow,
        Color32 fill)
    {
        for (int y = 22; y <= 96; y++)
        {
            for (int x = 24; x <= 104; x++)
            {
                if (IsHeartPixel(x - 2, y - 2))
                    pixels[y * size + x] = shadow;
            }
        }

        for (int y = 22; y <= 96; y++)
        {
            for (int x = 24; x <= 104; x++)
            {
                if (IsHeartPixel(x, y))
                    pixels[y * size + x] = fill;
            }
        }
    }

    private static bool IsHeartPixel(int x, int y)
    {
        bool leftLobe = (x - 48) * (x - 48) + (y - 80) * (y - 80) <= 18 * 18;
        bool rightLobe = (x - 80) * (x - 80) + (y - 80) * (y - 80) <= 18 * 18;
        bool lowerPoint = y >= 27 && y <= 80 && Mathf.Abs(x - 64) <= (y - 27) * 0.72f;
        return leftLobe || rightLobe || lowerPoint;
    }

    private static void DrawDigit(
        Color32[] pixels,
        int textureSize,
        char digit,
        Color32 shadow,
        Color32 fill)
    {
        if (!DigitPatterns.TryGetValue(digit, out string[] pattern))
            return;

        const int block = 10;
        int width = pattern[0].Length * block;
        int height = pattern.Length * block;
        int originX = (textureSize - width) / 2;
        int originY = (textureSize - height) / 2;

        DrawPattern(pixels, textureSize, pattern, originX + 3, originY - 3, block, shadow);
        DrawPattern(pixels, textureSize, pattern, originX, originY, block, fill);
    }

    private static void DrawPattern(
        Color32[] pixels,
        int textureSize,
        string[] pattern,
        int originX,
        int originY,
        int block,
        Color32 color)
    {
        for (int row = 0; row < pattern.Length; row++)
        {
            for (int column = 0; column < pattern[row].Length; column++)
            {
                if (pattern[row][column] != '1')
                    continue;

                int flippedRow = pattern.Length - 1 - row;
                for (int y = 1; y < block - 1; y++)
                {
                    for (int x = 1; x < block - 1; x++)
                    {
                        int pixelX = originX + column * block + x;
                        int pixelY = originY + flippedRow * block + y;

                        if (pixelX >= 0 && pixelX < textureSize && pixelY >= 0 && pixelY < textureSize)
                            pixels[pixelY * textureSize + pixelX] = color;
                    }
                }
            }
        }
    }

    private static float CalculateSpacing(Camera worldCamera)
    {
        if (!worldCamera.orthographic)
            return 1.8f;

        float worldWidth = worldCamera.orthographicSize * 2f * worldCamera.aspect;
        return Mathf.Clamp(worldWidth / 7f, 1.4f, 2.2f);
    }

    private static void EnsurePointerClickSupport(Camera worldCamera)
    {
        if (worldCamera.GetComponent<Physics2DRaycaster>() == null)
            Undo.AddComponent<Physics2DRaycaster>(worldCamera.gameObject);
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string parent = Path.GetDirectoryName(folderPath)?.Replace("\\", "/");
        string name = Path.GetFileName(folderPath);

        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent, name);
    }
}
