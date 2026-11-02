using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 대화창(초상화·대화창·이름표)을 잠깐 다른 화면 효과보다 위에 그립니다.
/// 세 오브젝트를 같은 크기 규칙(CanvasScaler)을 쓰는 새 화면(Canvas)으로 잠깐 옮겼다가, Lower로 원래 자리·순서에 돌려놓습니다.
/// - 일차 넘김 검은 화면(DayController) 위에서 대사를 보여 줄 때
/// - 들켰을 때 색수차 화면(ScreenAberration) 위에 대사를 또렷하게 보여 줄 때
/// </summary>
public static class DialogueOnTop
{
    private struct Moved
    {
        public Transform target;
        public Transform parent;
        public int siblingIndex;
    }

    private static readonly List<Moved> moved = new List<Moved>();
    private static Canvas topCanvas;
    private static int currentOrder = int.MinValue;

    public static bool IsRaised => currentOrder != int.MinValue;

    /// <summary>대화창을 sortingOrder = order 인 화면으로 올립니다.</summary>
    public static void Raise(int order)
    {
        if (IsRaised)
            RestoreAll();

        DialogueManager dm = DialogueManager.Instance;
        if (dm == null || dm.DialoguePanelRect == null)
            return;

        Canvas source = dm.DialoguePanelRect.GetComponentInParent<Canvas>();
        source = source != null ? source.rootCanvas : null;
        if (source == null)
            return;

        EnsureTopCanvas(source);
        topCanvas.sortingOrder = order;
        topCanvas.gameObject.SetActive(true);
        currentOrder = order;

        DialoguePortrait portrait = Object.FindFirstObjectByType<DialoguePortrait>();
        // 원래 순서대로 옮겨야 앞뒤(초상화 → 대화창 → 이름표)가 그대로 유지됨
        List<Transform> targets = new List<Transform>();
        AddTarget(targets, portrait != null ? portrait.PortraitRect : null);
        AddTarget(targets, dm.DialoguePanelRect);
        AddTarget(targets, dm.NamePanelRect);
        targets.Sort((a, b) => a.GetSiblingIndex().CompareTo(b.GetSiblingIndex()));

        foreach (Transform t in targets)
        {
            moved.Add(new Moved { target = t, parent = t.parent, siblingIndex = t.GetSiblingIndex() });
        }
        foreach (Transform t in targets)
            t.SetParent(topCanvas.transform, false);
    }

    /// <summary>Raise(order)로 올린 것을 원래 자리로 돌립니다. (다른 곳이 그 뒤에 다시 올렸으면 그대로 둠)</summary>
    public static void Lower(int order)
    {
        if (currentOrder != order)
            return;

        RestoreAll();
    }

    private static void RestoreAll()
    {
        // 앞 순서부터 원래 자리에 꽂아야 순서가 맞음
        moved.Sort((a, b) => a.siblingIndex.CompareTo(b.siblingIndex));
        foreach (Moved m in moved)
        {
            if (m.target == null || m.parent == null)
                continue;
            m.target.SetParent(m.parent, false);
            m.target.SetSiblingIndex(Mathf.Min(m.siblingIndex, m.parent.childCount - 1));
        }

        moved.Clear();
        currentOrder = int.MinValue;
        if (topCanvas != null)
            topCanvas.gameObject.SetActive(false);
    }

    private static void AddTarget(List<Transform> list, Transform t)
    {
        if (t != null && !list.Contains(t))
            list.Add(t);
    }

    private static void EnsureTopCanvas(Canvas source)
    {
        if (topCanvas == null)
        {
            GameObject go = new GameObject("대화창 위로 올림", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            // 원래 화면과 같은 씬에 둠 (씬이 바뀌면 같이 사라짐)
            go.transform.SetParent(source.transform.parent, false);
            topCanvas = go.GetComponent<Canvas>();
            topCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        // 크기 규칙을 원래 화면과 똑같이 맞춰 위치·크기가 그대로 보이게
        CanvasScaler from = source.GetComponent<CanvasScaler>();
        CanvasScaler to = topCanvas.GetComponent<CanvasScaler>();
        if (from != null)
        {
            to.uiScaleMode = from.uiScaleMode;
            to.referenceResolution = from.referenceResolution;
            to.screenMatchMode = from.screenMatchMode;
            to.matchWidthOrHeight = from.matchWidthOrHeight;
            to.scaleFactor = from.scaleFactor;
            to.referencePixelsPerUnit = from.referencePixelsPerUnit;
            to.physicalUnit = from.physicalUnit;
            to.fallbackScreenDPI = from.fallbackScreenDPI;
            to.defaultSpriteDPI = from.defaultSpriteDPI;
        }
        topCanvas.additionalShaderChannels = source.additionalShaderChannels;
        topCanvas.pixelPerfect = source.pixelPerfect;
    }
}
