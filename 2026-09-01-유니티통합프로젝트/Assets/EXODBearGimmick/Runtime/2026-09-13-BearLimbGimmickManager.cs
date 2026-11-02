using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DefaultExecutionOrder(200)]
internal sealed class BearLimbGimmickManager : MonoBehaviour
{
    private sealed class LimbDefinition
    {
        internal readonly string Id;
        internal readonly string Name;
        internal readonly string Room;
        internal readonly Vector2 Position;
        internal readonly string ImagePath;
        internal readonly float Rotation;
        internal readonly bool Nailed;

        internal LimbDefinition(string id, string name, string room, Vector2 position, string imagePath, float rotation, bool nailed = false)
        {
            Id = id;
            Name = name;
            Room = room;
            Position = position;
            ImagePath = imagePath;
            Rotation = rotation;
            Nailed = nailed;
        }
    }

    private static readonly LimbDefinition[] Limbs =
    {
        // 왼팔: 소파 쿠션 사이 / 오른팔: 주방 전자레인지 안 / 왼다리: 복도 빈 보드에 못으로 박힘 / 오른다리: 화장실 변기 속
        // (Position은 화면 가운데 기준 1920x1080 좌표, Rotation은 그림 회전 각도)
        new("bear_left_arm", "곰돌이 왼팔", "거실", new Vector2(-62f, -30f), "EXODBearGimmick/2026-11-01-Bear-LeftArm", -28f),
        new("bear_right_arm", "곰돌이 오른팔", "주방", new Vector2(-333f, -45f), "EXODBearGimmick/2026-11-01-Bear-RightArm", 90f),
        new("bear_left_leg", "곰돌이 왼다리", "복도", new Vector2(620f, 70f), "EXODBearGimmick/2026-11-01-Bear-LeftLeg", -8f, nailed: true),
        new("bear_right_leg", "곰돌이 오른다리", "화장실", new Vector2(-560f, -20f), "EXODBearGimmick/2026-11-01-Bear-RightLeg", 0f)
    };

    private const string NailImagePath = "EXODBearGimmick/2026-11-01-Bear-Nail";
    private const string FridgeNoteImagePath = "EXODBearGimmick/2026-11-01-Fridge-Note";
    private const float PickupImageHeight = 120f;

    private readonly Dictionary<string, ItemData> items = new();
    private readonly Dictionary<string, Sprite> limbSprites = new();
    private readonly Dictionary<string, GameObject> pickupButtons = new();
    private readonly HashSet<string> collected = new();
    private GameObject root;
    private GameObject bearVisual;
    private GameObject bearHitbox;
    private Sprite incompleteBearSprite;
    private Sprite completeBearSprite;
    private Sprite givingBearSprite;
    private ItemData rewardItem;
    private bool completed;
    private bool rewardPending;

    internal bool IsCompleted => completed;
    internal int CollectedCount => collected.Count;
    internal static int LimbCount => Limbs.Length;

    // 시작 화면을 거쳐 들어오거나 [다시하기]로 씬을 다시 불러와도 동작하도록 씬이 열릴 때마다 확인합니다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHandler()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (FindFirstObjectByType<BearLimbGimmickManager>() != null)
            return;

        SliderManager slider = FindFirstObjectByType<SliderManager>();
        if (slider != null)
            slider.gameObject.AddComponent<BearLimbGimmickManager>();
    }

    private void Start()
    {
        CreateRuntimeItems();
        CreateUi();
    }

    private void Update()
    {
        RefreshVisibility();
    }

    private void LateUpdate()
    {
        EnsureRootDrawOrder();
    }

    internal void TryCollect(string itemId)
    {
        if (completed || collected.Contains(itemId) || !items.TryGetValue(itemId, out ItemData item))
            return;

        QuickSlotInventory inventory = QuickSlotInventory.Instance ?? FindFirstObjectByType<QuickSlotInventory>();
        if (inventory == null)
        {
            Debug.LogError("[BearGimmick] QuickSlotInventory를 찾을 수 없습니다.", this);
            return;
        }

        if (!inventory.TryAdd(item))
        {
            Say("곰인형…?", "퀵슬롯이 가득 차서 팔다리를 주울 수 없어.");
            return;
        }

        collected.Add(itemId);
        EXODSound.Play("item_pickup");

        // 대사: Stage1Dialogues.BearLimbFound / BearAllCollected
        List<DialogueLine> lines = Stage1Dialogues.Thought(
            $"{Stage1Dialogues.BearLimbFound(itemId)} ({collected.Count}/{Limbs.Length})");
        if (collected.Count == Limbs.Length)
            lines.AddRange(Stage1Dialogues.BearAllCollected());
        DialogueManager.Instance?.StartDialogue(lines);

        RefreshVisibility();
    }

    /// <summary>팔다리는 다 돌려줬지만 보답 쪽지는 아직 못 받음 (다음 날 아침 컷신에서 받음)</summary>
    internal bool IsRewardPending => completed && rewardPending;

    public void ReturnLimbs()
    {
        if (completed)
        {
            // 아침 컷신 때 퀵슬롯이 꽉 차서 못 받았으면 여기서 받기
            if (rewardPending && DayEndReport.BearGiftDelivered)
            {
                GiveRewardNote();
                return;
            }

            if (rewardPending)
                DialogueManager.Instance?.StartDialogue(Stage1Dialogues.BearRewardWaiting());
            else
                Say(Stage1Dialogues.Bear, "정말 고마워!");
            return;
        }

        if (collected.Count < Limbs.Length)
        {
            Say(Stage1Dialogues.BearUnknown, $"아직 팔다리가 {Limbs.Length - collected.Count}개 부족해…");
            return;
        }

        QuickSlotInventory inventory = QuickSlotInventory.Instance ?? FindFirstObjectByType<QuickSlotInventory>();
        if (inventory == null)
            return;

        foreach (LimbDefinition limb in Limbs)
        {
            QuickSlotEntry entry = FindEntry(inventory, limb.Id);
            if (entry != null)
                inventory.TryRemove(entry);
        }

        // 보답(냉장고 비밀번호 쪽지)은 다음 날 아침 컷신에서 줌 (DayEndReport → GiveRewardNote)
        completed = true;
        rewardPending = FindEntry(inventory, Stage1Dialogues.FridgeNoteItemId) == null;
        EXODSound.Play("music_box", 0.8f);
        DangerLevel.GimmickSolved();
        SetBearSprite(completeBearSprite);

        // 대사: Stage1Dialogues.BearReturn
        List<DialogueLine> lines = Stage1Dialogues.BearReturn();
        if (Stage1StoryController.Instance != null)
            Stage1StoryController.Instance.PlayCutscene(lines);
        else
            DialogueManager.Instance?.StartDialogue(lines);
    }

    /// <summary>보답 쪽지를 퀵슬롯에 넣습니다. 넣었으면 true (자리가 없으면 곰인형이 말하고 false).</summary>
    internal bool GiveRewardNote()
    {
        if (!rewardPending)
            return true;

        QuickSlotInventory inventory = QuickSlotInventory.Instance ?? FindFirstObjectByType<QuickSlotInventory>();
        if (inventory == null)
            return false;

        if (FindEntry(inventory, Stage1Dialogues.FridgeNoteItemId) == null && !inventory.TryAdd(rewardItem))
        {
            Say(Stage1Dialogues.Bear, "보답할 게 있는데… 받을 자리가 없네요. 퀵슬롯을 조금 비우고 거실의 저를 눌러 주세요.");
            return false;
        }

        rewardPending = false;
        EXODSound.Play("item_pickup");
        return true;
    }

    private void SetBearSprite(Sprite sprite)
    {
        if (bearVisual != null && sprite != null)
            bearVisual.GetComponent<Image>().sprite = sprite;
    }

    private void CreateRuntimeItems()
    {
        foreach (LimbDefinition limb in Limbs)
        {
            limbSprites[limb.Id] = LoadBearSprite(limb.ImagePath);
            items[limb.Id] = CreateItem(limb.Id, limb.Name, false, limbSprites[limb.Id], new Color(0.67f, 0.45f, 0.28f, 1f));
        }

        rewardItem = CreateItem(
            Stage1Dialogues.FridgeNoteItemId,
            Stage1Dialogues.FridgeNoteItemName,
            false,
            LoadBearSprite(FridgeNoteImagePath),
            new Color(0.90f, 0.84f, 0.66f, 1f));
    }

    // 그림이 없으면 색 네모 아이콘으로 대신합니다.
    private static ItemData CreateItem(string id, string displayName, bool consume, Sprite icon, Color fallbackColor)
    {
        ItemData item = ScriptableObject.CreateInstance<ItemData>();
        item.name = id;
        SetPrivateField(item, "itemId", id);
        SetPrivateField(item, "displayName", displayName);
        SetPrivateField(item, "icon", icon != null ? icon : CreateIcon(fallbackColor));
        SetPrivateField(item, "consumeOnSuccessfulUse", consume);
        return item;
    }

    private static Sprite CreateIcon(Color color)
    {
        const int size = 48;
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false);
        texture.name = "BearGimmickRuntimeIcon";
        texture.filterMode = FilterMode.Point;
        Color transparent = new(0f, 0f, 0f, 0f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool inside = x >= 7 && x < size - 7 && y >= 7 && y < size - 7;
                bool border = inside && (x < 10 || x >= size - 10 || y < 10 || y >= size - 10);
                texture.SetPixel(x, y, !inside ? transparent : border ? new Color(0.12f, 0.09f, 0.08f, 1f) : color);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field?.SetValue(target, value);
    }

    private void CreateUi()
    {
        // 방 화면(SliderPanel)이 있는 메인 캔버스에 붙입니다.
        // (아무 캔버스나 고르면 하트 HUD 캔버스에 붙어서, Tab으로 UI를 숨길 때 팔다리까지 사라졌음 — 2026-11-02 수정)
        Canvas canvas = SliderManager.Instance != null && SliderManager.Instance.sliderPanel != null
            ? SliderManager.Instance.sliderPanel.GetComponentInParent<Canvas>(true)
            : null;
        if (canvas != null)
            canvas = canvas.rootCanvas;
        else
            canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
            return;

        incompleteBearSprite = LoadBearSprite("EXODBearGimmick/2026-09-13-Bear-Incomplete");
        completeBearSprite = LoadBearSprite("EXODBearGimmick/2026-09-13-Bear-Complete");
        givingBearSprite = LoadBearSprite("EXODBearGimmick/2026-09-13-Bear-Giving");

        root = new GameObject("곰돌이 팔다리 기믹", typeof(RectTransform));
        root.transform.SetParent(canvas.transform, false);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        EnsureRootDrawOrder();

        foreach (LimbDefinition limb in Limbs)
        {
            limbSprites.TryGetValue(limb.Id, out Sprite sprite);
            pickupButtons[limb.Id] = sprite != null
                ? CreateImageButton(limb.Name, sprite, limb.Position, limb.Rotation, () => TryCollect(limb.Id))
                : CreateButton(limb.Name, limb.Position, () => TryCollect(limb.Id));

            if (limb.Nailed)
                AddNail(pickupButtons[limb.Id]);
        }

        CreateBearVisual();
    }

    private void EnsureRootDrawOrder()
    {
        if (root == null || root.transform.parent == null)
            return;

        Transform parent = root.transform.parent;
        Transform namePanel = parent.Find("이름표");
        Transform sliderPanelTransform = parent.Find("SliderPanel");
        Transform dialoguePanel = parent.Find("대화창");

        // 방 배경과 조사 영역 위에 기믹을 놓고, 대화 관련 UI 세 개는 다시 맨 앞으로 보냅니다.
        root.transform.SetAsLastSibling();
        if (namePanel != null)
            namePanel.SetAsLastSibling();
        if (sliderPanelTransform != null)
            sliderPanelTransform.SetAsLastSibling();
        if (dialoguePanel != null)
            dialoguePanel.SetAsLastSibling();
    }

    private static Sprite LoadBearSprite(string resourcePath)
    {
        Texture2D texture = Resources.Load<Texture2D>(resourcePath);
        if (texture == null)
        {
            Debug.LogError($"[BearGimmick] 곰돌이 이미지를 찾을 수 없습니다: Resources/{resourcePath}");
            return null;
        }

        return Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);
    }

    private void CreateBearVisual()
    {
        bearVisual = new GameObject("곰돌이 이미지", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bearVisual.transform.SetParent(root.transform, false);
        bearVisual.transform.SetAsFirstSibling();

        RectTransform visualRect = bearVisual.GetComponent<RectTransform>();
        visualRect.anchorMin = Vector2.zero;
        visualRect.anchorMax = Vector2.one;
        visualRect.offsetMin = Vector2.zero;
        visualRect.offsetMax = Vector2.zero;

        Image visualImage = bearVisual.GetComponent<Image>();
        visualImage.sprite = incompleteBearSprite;
        visualImage.color = Color.white;
        visualImage.preserveAspect = true;
        visualImage.raycastTarget = false;

        bearHitbox = new GameObject("곰돌이 클릭 영역", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        bearHitbox.transform.SetParent(root.transform, false);
        RectTransform hitboxRect = bearHitbox.GetComponent<RectTransform>();
        hitboxRect.anchorMin = hitboxRect.anchorMax = new Vector2(0.5f, 0.5f);
        hitboxRect.anchoredPosition = new Vector2(0f, 85f);
        hitboxRect.sizeDelta = new Vector2(230f, 260f);

        Image hitboxImage = bearHitbox.GetComponent<Image>();
        hitboxImage.color = new Color(1f, 1f, 1f, 0f);
        bearHitbox.GetComponent<Button>().onClick.AddListener(ReturnLimbs);
    }

    // 팔다리 위쪽에 못 머리를 그려 보드에 박혀 있는 것처럼 보이게 합니다. (팔다리를 주우면 같이 사라짐)
    private static void AddNail(GameObject limbButton)
    {
        Sprite nailSprite = LoadBearSprite(NailImagePath);
        if (nailSprite == null || limbButton == null)
            return;

        GameObject nail = new("못", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        nail.transform.SetParent(limbButton.transform, false);
        RectTransform rect = nail.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -22f);
        rect.sizeDelta = new Vector2(30f, 30f);

        Image image = nail.GetComponent<Image>();
        image.sprite = nailSprite;
        image.raycastTarget = false;
    }

    // 방에 떨어져 있는 팔다리 그림. 마우스를 올리면 살짝 밝아집니다.
    private GameObject CreateImageButton(string label, Sprite sprite, Vector2 position, float rotation, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = new(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(root.transform, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(PickupImageHeight * sprite.rect.width / sprite.rect.height, PickupImageHeight);
        rect.localEulerAngles = new Vector3(0f, 0f, rotation);

        Image image = buttonObject.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.92f, 0.92f, 0.92f, 1f);
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        colors.selectedColor = colors.normalColor;
        button.colors = colors;
        button.onClick.AddListener(action);

        return buttonObject;
    }

    private GameObject CreateButton(string label, Vector2 position, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = new(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(root.transform, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(180f, 74f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.13f, 0.10f, 0.10f, 0.82f);
        Button button = buttonObject.GetComponent<Button>();
        button.onClick.AddListener(action);

        GameObject textObject = new("표시", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(buttonObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 6f);
        textRect.offsetMax = new Vector2(-8f, -6f);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 24f;
        text.fontStyle = FontStyles.Normal;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        TextMeshProUGUI roomText = SliderManager.Instance != null ? SliderManager.Instance.roomNameText : null;
        if (roomText != null && roomText.font != null)
            text.font = roomText.font;

        return buttonObject;
    }

    private void RefreshVisibility()
    {
        if (root == null || SliderManager.Instance == null)
            return;

        bool blocked =
            (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueOpen) ||
            (InspectionManager.Instance != null && InspectionManager.Instance.IsOpen) ||
            Stage1FridgeGimmick.IsAnyInsideShown;
        string room = SliderManager.Instance.CurrentRoom;
        if (string.IsNullOrEmpty(room))
            room = "침실";

        foreach (LimbDefinition limb in Limbs)
        {
            if (pickupButtons.TryGetValue(limb.Id, out GameObject button))
                button.SetActive(!blocked && !completed && !collected.Contains(limb.Id) && room == limb.Room);
        }

        bool showBear = !blocked && SliderManager.Instance.isUnlocked && room == "침실";
        if (bearVisual != null)
            bearVisual.SetActive(showBear);
        if (bearHitbox != null)
            bearHitbox.SetActive(showBear);
    }

    private static QuickSlotEntry FindEntry(QuickSlotInventory inventory, string itemId)
    {
        foreach (QuickSlotEntry entry in inventory.Entries)
        {
            if (entry?.ItemData != null && string.Equals(entry.ItemData.ItemId, itemId, StringComparison.Ordinal))
                return entry;
        }
        return null;
    }

    private static void Say(string speaker, string text)
    {
        DialogueManager.Instance?.StartCharacterDialogue(speaker, new[] { text });
    }
}
