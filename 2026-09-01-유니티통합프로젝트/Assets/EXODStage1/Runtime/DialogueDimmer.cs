using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 대화가 열려 있는 동안 방 배경을 어둡게 덮어 대사에 집중되게 합니다.
/// 어두운 막은 방 그림 위, 초상화·이름표·대화창 아래에 그려집니다.
/// </summary>
[DefaultExecutionOrder(245)]
public sealed class DialogueDimmer : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] private float dimAlpha = 0.65f;  // 어두운 정도 (1이면 완전히 검정)
    [SerializeField] private float fadeDuration = 0.25f;

    /// <summary>켜 두면 대화 중에도 어둡게 하지 않습니다. (냉장고 안 그림처럼 보여 줘야 하는 화면)</summary>
    public static bool Suppressed { get; set; }

    private Image dim;
    private DialoguePortrait portrait;
    private Coroutine fadeCoroutine;

    private void Start()
    {
        DialogueManager dm = DialogueManager.Instance;
        if (dm == null || dm.DialoguePanelRect == null)
            return;

        portrait = FindFirstObjectByType<DialoguePortrait>();
        CreateDim(dm.DialoguePanelRect.parent);
        dm.onDialogueStart.AddListener(Show);
        dm.onDialogueEnd.AddListener(Hide);

        if (dm.IsDialogueOpen)
            Show();
    }

    private void OnDestroy()
    {
        DialogueManager dm = DialogueManager.Instance;
        if (dm == null)
            return;

        dm.onDialogueStart.RemoveListener(Show);
        dm.onDialogueEnd.RemoveListener(Hide);
    }

    private void LateUpdate()
    {
        if (dim != null && dim.gameObject.activeSelf)
            KeepBehindDialogue();
    }

    private void Show()
    {
        if (Suppressed)
            return;

        dim.gameObject.SetActive(true);
        KeepBehindDialogue();
        StartFade(dimAlpha);
    }

    private void Hide()
    {
        if (dim.gameObject.activeSelf)
            StartFade(0f);
    }

    private void StartFade(float target)
    {
        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        fadeCoroutine = StartCoroutine(FadeTo(target));
    }

    private IEnumerator FadeTo(float target)
    {
        Color color = dim.color;
        float start = color.a;
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            color.a = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / fadeDuration));
            dim.color = color;
            yield return null;
        }

        color.a = target;
        dim.color = color;
        if (target <= 0f)
            dim.gameObject.SetActive(false);

        fadeCoroutine = null;
    }

    // 초상화(없으면 이름표, 대화창) 중 가장 앞 순서인 것 바로 뒤에 놓습니다.
    private void KeepBehindDialogue()
    {
        Transform parent = dim.transform.parent;
        int targetIndex = int.MaxValue;
        foreach (Transform child in parent)
        {
            if (child == dim.transform)
                continue;

            if ((portrait != null && child == portrait.PortraitRect)
                || child == DialogueManager.Instance.NamePanelRect
                || child == DialogueManager.Instance.DialoguePanelRect)
            {
                targetIndex = Mathf.Min(targetIndex, child.GetSiblingIndex());
            }
        }

        if (targetIndex == int.MaxValue)
            return;

        int myIndex = dim.transform.GetSiblingIndex();
        if (myIndex != targetIndex - 1)
            dim.transform.SetSiblingIndex(myIndex < targetIndex ? targetIndex - 1 : targetIndex);
    }

    private void CreateDim(Transform parent)
    {
        GameObject go = new GameObject("대화 배경 어둡게", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        dim = go.GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0f);
        dim.raycastTarget = false;
        go.SetActive(false);
    }
}
