using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
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

        internal LimbDefinition(string id, string name, string room, Vector2 position)
        {
            Id = id;
            Name = name;
            Room = room;
            Position = position;
        }
    }

    private static readonly LimbDefinition[] Limbs =
    {
        new("bear_left_arm", "곰돌이 왼팔", "거실", new Vector2(360f, -150f)),
        new("bear_right_arm", "곰돌이 오른팔", "주방", new Vector2(-360f, -125f)),
        new("bear_left_leg", "곰돌이 왼다리", "복도", new Vector2(-500f, 120f)),
        new("bear_right_leg", "곰돌이 오른다리", "화장실", new Vector2(-560f, -20f))
    };

    private readonly Dictionary<string, ItemData> items = new();
    private readonly Dictionary<string, GameObject> pickupButtons = new();
    private readonly HashSet<string> collected = new();
    private GameObject root;
    private GameObject bearVisual;
    private GameObject bearHitbox;
    private Sprite incompleteBearSprite;
    private Sprite completeBearSprite;
    private ItemData rewardItem;
    private bool completed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
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
        Say("주인공", $"{item.DisplayName}을 찾았다. ({collected.Count}/4)");
        RefreshVisibility();
    }

    public void ReturnLimbs()
    {
        if (completed)
        {
            Say("곰인형", "정말 고마워요!");
            return;
        }

        if (collected.Count < Limbs.Length)
        {
            Say("곰인형…?", $"아직 팔다리가 {Limbs.Length - collected.Count}개 부족해요.");
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

        bool alreadyHasReward = FindEntry(inventory, "door_password_note") != null;
        if (!alreadyHasReward && !inventory.TryAdd(rewardItem))
        {
            Say("곰인형…?", "보상을 받을 자리가 필요해요.");
            return;
        }

        completed = true;
        if (bearVisual != null && completeBearSprite != null)
            bearVisual.GetComponent<Image>().sprite = completeBearSprite;

        DialogueManager.Instance?.StartDialogue(new List<DialogueLine>
        {
            new() { speakerName = "주인공", text = "다 모았다! 여기!", dialogueType = DialogueType.Character },
            new() { speakerName = "곰인형", text = "저의 팔다리를 찾아주시다니 정말 감사할 따름이에요.", dialogueType = DialogueType.Character },
            new() { speakerName = "곰인형", text = "이걸 드릴게요!", dialogueType = DialogueType.Character },
            new() { speakerName = "주인공", text = "종이에 현관 비밀번호 단서가 적혀 있다.", dialogueType = DialogueType.Interaction }
        });
    }

    private void CreateRuntimeItems()
    {
        foreach (LimbDefinition limb in Limbs)
            items[limb.Id] = CreateItem(limb.Id, limb.Name, false, new Color(0.67f, 0.45f, 0.28f, 1f));

        rewardItem = CreateItem("door_password_note", "현관 비밀번호 단서", false, new Color(0.90f, 0.84f, 0.66f, 1f));
    }

    private static ItemData CreateItem(string id, string displayName, bool consume, Color iconColor)
    {
        ItemData item = ScriptableObject.CreateInstance<ItemData>();
        item.name = id;
        SetPrivateField(item, "itemId", id);
        SetPrivateField(item, "displayName", displayName);
        SetPrivateField(item, "icon", CreateIcon(iconColor));
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
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
            return;

        incompleteBearSprite = LoadBearSprite("EXODBearGimmick/2026-09-13-Bear-Incomplete");
        completeBearSprite = LoadBearSprite("EXODBearGimmick/2026-09-13-Bear-Complete");

        root = new GameObject("곰돌이 팔다리 기믹", typeof(RectTransform));
        root.transform.SetParent(canvas.transform, false);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        EnsureRootDrawOrder();

        foreach (LimbDefinition limb in Limbs)
            pickupButtons[limb.Id] = CreateButton(limb.Name, limb.Position, () => TryCollect(limb.Id));

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
            (InspectionManager.Instance != null && InspectionManager.Instance.IsOpen);
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
