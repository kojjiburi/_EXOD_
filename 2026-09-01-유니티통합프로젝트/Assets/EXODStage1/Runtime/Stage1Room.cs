using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 방 배경 그림 위에 클릭 영역을 만들고, 이야기 대사를 재생하는 기믹 공용 도우미입니다.
/// </summary>
public static class Stage1Room
{
    /// <summary>SliderManager가 만든 방 배경 화면 (예: "주방 배경 화면")</summary>
    public static RectTransform FindRoomView(string roomName)
    {
        string objectName = roomName + " 배경 화면";
        foreach (RectTransform rect in UnityEngine.Object.FindObjectsByType<RectTransform>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (rect.name == objectName)
                return rect;
        }

        return null;
    }

    /// <summary>
    /// 방 배경 위에 보이지 않는 클릭 영역을 만듭니다.
    /// area는 배경 그림 기준 비율입니다. (왼쪽 아래 x, y ~ 오른쪽 위 x, y)
    /// 방 배경의 자식이라 그 방에 있을 때만 보입니다.
    /// </summary>
    public static void CreateHotspot(RectTransform roomView, string name, Rect area, Action onClick)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(roomView, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = area.min;
        rect.anchorMax = area.max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0f);
        Button button = go.GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() =>
        {
            if (!IsBlocked())
                onClick();
        });
    }

    /// <summary>대화·조사 창·기절 연출·키패드가 떠 있으면 방 클릭을 무시합니다.</summary>
    public static bool IsBlocked()
    {
        return (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueOpen)
            || (InspectionManager.Instance != null && InspectionManager.Instance.IsOpen)
            || (PlayerFaintController.Instance != null && PlayerFaintController.Instance.IsBusy)
            || Stage1Keypad.IsOpen;
    }

    public static void PlayCutscene(List<DialogueLine> lines, Action onFinished = null)
    {
        if (Stage1StoryController.Instance != null)
            Stage1StoryController.Instance.PlayCutscene(lines, onFinished);
        else
            DialogueManager.Instance?.StartDialogue(lines);
    }

    public static void PlayChoiceCutscene(List<DialogueLine> lines, string[] choices, Action<int> onChosen)
    {
        if (Stage1StoryController.Instance != null)
            Stage1StoryController.Instance.PlayChoiceCutscene(lines, choices, onChosen);
        else
            DialogueManager.Instance?.StartChoiceDialogue(lines, choices, onChosen);
    }

    /// <summary>기절시킨 뒤(하트 -1) 깨어나면 onWokeUp을 호출합니다.</summary>
    public static void Faint(Action onWokeUp = null)
    {
        if (PlayerFaintController.Instance != null)
            PlayerFaintController.Instance.Faint(onWokeUp);
        else
            onWokeUp?.Invoke();
    }
}
