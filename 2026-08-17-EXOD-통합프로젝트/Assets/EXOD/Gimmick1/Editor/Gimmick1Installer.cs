#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class Gimmick1Installer
{
    private const string RootName = "EXOD_기믹1";
    private const string AssetRoot = "Assets/EXOD/Gimmick1";

    [MenuItem("EXOD/기믹 1 조사 시스템 자동 설치")]
    public static void Install()
    {
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("EXOD", "현재 씬에서 Canvas를 찾지 못했습니다.", "확인");
            return;
        }

        GameObject oldRoot = GameObject.Find(RootName);
        if (oldRoot != null)
        {
            bool replace = Application.isBatchMode || EditorUtility.DisplayDialog(
                "EXOD",
                "이미 설치된 기믹 1 오브젝트가 있습니다. 기존 기믹 1만 지우고 다시 설치할까요?",
                "다시 설치",
                "취소");
            if (!replace) return;
            Undo.DestroyObjectImmediate(oldRoot);
        }

        EnsureEventSystem();

        Sprite bedroom = LoadSprite("08_침실_기믹적용_640x360.png");
        Sprite calendar = LoadSprite("01_달력_단서7.png");
        Sprite photo = LoadSprite("02_사진_단서2.png");
        Sprite memo = LoadSprite("03_메모_단서4.png");
        Sprite drawer = LoadSprite("04_잠긴_서랍.png");

        GameObject root = CreateUiObject(RootName, canvas.transform);
        Stretch(root.GetComponent<RectTransform>());
        root.transform.SetAsFirstSibling();

        GameObject room = CreateUiObject("침실 기믹 화면", root.transform);
        Stretch(room.GetComponent<RectTransform>());

        Image roomImage = room.AddComponent<Image>();
        roomImage.sprite = bedroom;
        roomImage.preserveAspect = false;
        roomImage.raycastTarget = false;

        CreateHotspot(room.transform, "달력 조사 영역", "bedroom_calendar", "달력", calendar,
            new Vector2(82f, 270f), new Vector2(105f, 125f),
            new[] { "오래된 달력이다.", "특정 날짜에 붉은 원이 그려져 있다." },
            new[] { "붉은 원 안에 숫자 '7'이 보인다." },
            new[] { "숫자 '7'에 붉은 원이 그려진 달력이다." });

        CreateHotspot(room.transform, "베개와 사진 조사 영역", "bedroom_photo", "베개", photo,
            new Vector2(305f, 205f), new Vector2(175f, 90f),
            new[] { "하얀 베개다.", "아래에 무언가 끼어 있는 것 같다." },
            new[] { "베개 아래에서 오래된 사진을 발견했다.", "사진 뒷면에 숫자 '2'가 적혀 있다." },
            new[] { "사진이 들어 있던 베개다.", "더 이상 특별한 것은 없다." });

        CreateHotspot(room.transform, "메모 조사 영역", "bedroom_memo", "메모", memo,
            new Vector2(492f, 225f), new Vector2(95f, 75f),
            new[] { "화병 아래에 종이 한 장이 깔려 있다." },
            new[] { "종이에는 숫자 '4'가 적혀 있다." },
            new[] { "숫자 '4'가 적힌 메모다." });

        CreateHotspot(room.transform, "서랍장 조사 영역", "bedroom_drawer", "서랍장", drawer,
            new Vector2(506f, 155f), new Vector2(130f, 145f),
            new[] { "오른쪽 서랍장이다.", "첫 번째 서랍에 잠금장치가 달려 있다." },
            new[] { "세 자리 숫자를 입력해야 열 수 있을 것 같다." },
            new[] { "세 자리 비밀번호가 필요한 서랍이다." });

        GameObject managerObject = new GameObject("조사 화면 관리자", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(managerObject, "EXOD 기믹 1 설치");
        managerObject.transform.SetParent(root.transform, false);
        Stretch(managerObject.GetComponent<RectTransform>());
        InspectionManager manager = managerObject.AddComponent<InspectionManager>();

        GameObject panel = CreateUiObject("자세히 보기 화면", managerObject.transform);
        Stretch(panel.GetComponent<RectTransform>());

        Image dim = panel.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.82f);
        dim.raycastTarget = true;

        GameObject detailObject = CreateUiObject("물건 확대 이미지", panel.transform);
        RectTransform detailRect = detailObject.GetComponent<RectTransform>();
        detailRect.anchorMin = new Vector2(0.5f, 0.5f);
        detailRect.anchorMax = new Vector2(0.5f, 0.5f);
        detailRect.sizeDelta = new Vector2(760f, 540f);
        detailRect.anchoredPosition = Vector2.zero;
        Image detailImage = detailObject.AddComponent<Image>();
        detailImage.color = Color.white;
        detailImage.preserveAspect = true;
        Button detailButton = detailObject.AddComponent<Button>();

        Button closeButton = CreateTextButton(panel.transform, "닫기 버튼", "닫기  ×", new Vector2(-90f, -55f));

        SerializedObject managerData = new SerializedObject(manager);
        managerData.FindProperty("inspectionPanel").objectReferenceValue = panel;
        managerData.FindProperty("detailImage").objectReferenceValue = detailImage;
        managerData.FindProperty("detailButton").objectReferenceValue = detailButton;
        managerData.FindProperty("closeButton").objectReferenceValue = closeButton;

        GameObject sliderPanel = GameObject.Find("SliderPanel");
        if (sliderPanel != null)
        {
            SerializedProperty hidden = managerData.FindProperty("hideWhileInspecting");
            hidden.arraySize = 1;
            hidden.GetArrayElementAtIndex(0).objectReferenceValue = sliderPanel;
        }
        managerData.ApplyModifiedPropertiesWithoutUndo();

        panel.SetActive(false);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = root;

        if (!Application.isBatchMode)
        {
            EditorUtility.DisplayDialog(
                "EXOD",
                "기믹 1 조사 시스템 설치가 끝났습니다.\n\n재생 후 달력, 베개, 메모, 오른쪽 서랍을 클릭해 확인하세요.",
                "확인");
        }
    }

    public static void InstallFromCommandLine()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
        Install();
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    private static void CreateHotspot(
        Transform parent, string objectName, string itemId, string itemName, Sprite detailSprite,
        Vector2 referencePosition, Vector2 referenceSize,
        string[] opening, string[] first, string[] repeat)
    {
        GameObject hotspot = CreateUiObject(objectName, parent);
        RectTransform rect = hotspot.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(referencePosition.x / 640f, referencePosition.y / 360f);
        rect.anchorMax = rect.anchorMin;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = referenceSize;

        Image image = hotspot.AddComponent<Image>();
        image.color = new Color(1f, 0.82f, 0.18f, 0f);
        image.raycastTarget = true;

        InspectableItem item = hotspot.AddComponent<InspectableItem>();
        SerializedObject data = new SerializedObject(item);
        data.FindProperty("itemId").stringValue = itemId;
        data.FindProperty("itemName").stringValue = itemName;
        data.FindProperty("detailSprite").objectReferenceValue = detailSprite;
        SetStringArray(data.FindProperty("openingTexts"), opening);
        SetStringArray(data.FindProperty("firstExamineTexts"), first);
        SetStringArray(data.FindProperty("repeatExamineTexts"), repeat);
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetStringArray(SerializedProperty property, string[] values)
    {
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            property.GetArrayElementAtIndex(i).stringValue = values[i];
    }

    private static Button CreateTextButton(Transform parent, string name, string label, Vector2 offset)
    {
        GameObject buttonObject = CreateUiObject(name, parent);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.one;
        rect.anchorMax = Vector2.one;
        rect.pivot = Vector2.one;
        rect.sizeDelta = new Vector2(150f, 64f);
        rect.anchoredPosition = offset;

        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.22f, 0.12f, 0.09f, 0.96f);
        Button button = buttonObject.AddComponent<Button>();

        GameObject textObject = CreateUiObject("글자", buttonObject.transform);
        Stretch(textObject.GetComponent<RectTransform>());
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 28f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        return button;
    }

    private static Sprite LoadSprite(string fileName)
    {
        string path = AssetRoot + "/Art/" + fileName;
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject result = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(result, "EXOD 기믹 1 설치");
        result.transform.SetParent(parent, false);
        return result;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;
        GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        Undo.RegisterCreatedObjectUndo(eventSystem, "EXOD EventSystem 생성");
    }
}

#endif
