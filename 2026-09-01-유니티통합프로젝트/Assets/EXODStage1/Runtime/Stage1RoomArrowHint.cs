using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 방 이동 화살표(SliderManager 좌우 버튼)에 마우스를 올리면 눌렀을 때 갈 방 이름을 띄웁니다. (편의 기능, 사용자 요청 2026-11-03)
/// 방문이 잠겨 있을 때(침실을 아직 못 나갔을 때)는 띄우지 않습니다.
/// 실행할 때 SliderManager가 있는 씬에 자동으로 붙습니다.
/// </summary>
public sealed class Stage1RoomArrowHint : MonoBehaviour
{
    private static readonly FieldInfo RoomsField =
        typeof(SliderManager).GetField("_unlockedRooms", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo IndexField =
        typeof(SliderManager).GetField("_currentIndex", BindingFlags.NonPublic | BindingFlags.Instance);

    private SliderManager slider;
    private RectTransform label;
    private TextMeshProUGUI labelText;
    private Button hovered;
    private int hoveredDirection;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHandler()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single)
            return;
        SliderManager s = FindFirstObjectByType<SliderManager>();
        if (s != null && s.GetComponent<Stage1RoomArrowHint>() == null)
            s.gameObject.AddComponent<Stage1RoomArrowHint>();
    }

    private void Start()
    {
        slider = GetComponent<SliderManager>();
        if (slider == null)
            return;
        Hook(slider.leftButton, -1);
        Hook(slider.rightButton, +1);
    }

    private void Hook(Button button, int direction)
    {
        if (button == null)
            return;
        EventTrigger trigger = button.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = button.gameObject.AddComponent<EventTrigger>();

        EventTrigger.Entry enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => { hovered = button; hoveredDirection = direction; });
        trigger.triggers.Add(enter);

        EventTrigger.Entry exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => { if (hovered == button) hovered = null; });
        trigger.triggers.Add(exit);
    }

    private void LateUpdate()
    {
        string target = hovered != null && hovered.gameObject.activeInHierarchy && hovered.interactable ? TargetRoom(hoveredDirection) : null;
        if (string.IsNullOrEmpty(target) || Stage1Room.IsBlocked())
        {
            if (label != null && label.gameObject.activeSelf)
                label.gameObject.SetActive(false);
            return;
        }

        if (label == null)
            Build(hovered);
        if (label == null)
            return;

        labelText.text = hoveredDirection < 0 ? "< " + target : target + " >";
        label.gameObject.SetActive(true);

        // 화살표 바로 안쪽(화면 가운데 쪽)에 붙임
        RectTransform arrow = hovered.transform as RectTransform;
        Vector3[] corners = new Vector3[4];
        arrow.GetWorldCorners(corners);
        float scale = label.lossyScale.x;
        Vector2 size = new Vector2(labelText.preferredWidth + 36f, 56f);
        label.sizeDelta = size;
        float midY = (corners[0].y + corners[1].y) * 0.5f;
        float x = hoveredDirection < 0
            ? corners[2].x + 10f * scale + size.x * 0.5f * scale
            : corners[0].x - 10f * scale - size.x * 0.5f * scale;
        label.position = new Vector3(x, midY, label.position.z);
    }

    private string TargetRoom(int direction)
    {
        if (slider == null || !slider.isUnlocked || RoomsField == null || IndexField == null)
            return null;
        List<string> rooms = RoomsField.GetValue(slider) as List<string>;
        if (rooms == null || rooms.Count < 2)
            return null;
        int index = (int)IndexField.GetValue(slider) + direction;
        if (index < 0) index = rooms.Count - 1;
        if (index >= rooms.Count) index = 0;
        return rooms[index];
    }

    private void Build(Button near)
    {
        Canvas canvas = near.GetComponentInParent<Canvas>();
        if (canvas == null)
            return;
        canvas = canvas.rootCanvas;

        GameObject go = new GameObject("갈 방 이름", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Canvas));
        go.transform.SetParent(canvas.transform, false);
        Canvas own = go.GetComponent<Canvas>();
        own.overrideSorting = true;
        own.sortingOrder = 620;
        label = go.GetComponent<RectTransform>();
        label.anchorMin = label.anchorMax = new Vector2(0.5f, 0.5f);
        Image bg = go.GetComponent<Image>();
        bg.color = new Color(0.06f, 0.04f, 0.05f, 1f);
        bg.raycastTarget = false;
        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0.55f, 0.08f, 0.12f, 1f);
        outline.effectDistance = new Vector2(2f, -2f);

        GameObject textGo = new GameObject("글자", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(go.transform, false);
        RectTransform tr = textGo.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = tr.offsetMax = Vector2.zero;
        labelText = textGo.GetComponent<TextMeshProUGUI>();
        if (EXODUiAssets.Font != null)
            labelText.font = EXODUiAssets.Font;
        labelText.fontSize = 30f;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.color = new Color(0.95f, 0.92f, 0.93f, 1f);
        labelText.raycastTarget = false;
        go.SetActive(false);
    }
}
