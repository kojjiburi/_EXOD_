using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 여자친구가 말할 때 대화창 왼쪽 위에 상반신 그림을 띄웁니다.
/// - "???"   : 검은 실루엣 (정체를 숨김)
/// - "여자친구": 원래 그림
/// 그림 아랫부분은 대화창 뒤로 가려지고, 이름표는 그림 앞에 그려집니다.
/// 그림 파일: Assets/Resources/EXODStage1/Girlfriend_Portrait.png (배경 투명)
/// </summary>
[DefaultExecutionOrder(240)]
public sealed class DialoguePortrait : MonoBehaviour
{
    private const string PortraitResourcePath = "EXODStage1/Girlfriend_Portrait";

    [Header("=== 배치 (Canvas 기준 크기) ===")]
    [SerializeField] private float portraitHeight = 640f;  // 그림 전체 높이
    [SerializeField] private float hiddenBelow = 230f;     // 대화창 뒤로 가려지는 높이
    [SerializeField] private float leftInset = 40f;        // 대화창 왼쪽 끝에서 안쪽으로 들어간 거리

    [Header("=== 연출 ===")]
    [SerializeField] private float fadeDuration = 0.2f;
    [SerializeField] private Color silhouetteColor = new Color(0.04f, 0.03f, 0.04f, 1f);

    private Image portrait;
    private CanvasGroup group;
    private Coroutine fadeCoroutine;
    private bool isShown;

    private void Start()
    {
        DialogueManager dm = DialogueManager.Instance;
        if (dm == null || dm.DialoguePanelRect == null)
            return;

        Sprite sprite = LoadPortrait();
        if (sprite == null)
            return;

        CreatePortrait(dm.DialoguePanelRect.parent, sprite);

        dm.LineShown += OnLineShown;
        dm.onDialogueEnd.AddListener(Hide);
    }

    private void OnDestroy()
    {
        DialogueManager dm = DialogueManager.Instance;
        if (dm == null)
            return;

        dm.LineShown -= OnLineShown;
        dm.onDialogueEnd.RemoveListener(Hide);
    }

    private void LateUpdate()
    {
        if (portrait == null || !portrait.gameObject.activeSelf)
            return;

        KeepBehindNamePanel();
        FollowDialoguePanel();
    }

    private void OnLineShown(DialogueLine line)
    {
        string speaker = line.speakerName != null ? line.speakerName.Trim() : string.Empty;

        if (speaker == Stage1Dialogues.Unknown)
            Show(silhouetteColor);
        else if (speaker == Stage1Dialogues.Girlfriend || speaker == "얀데레" || speaker == "여주")
            Show(Color.white);
        else
            Hide();
    }

    private void Show(Color color)
    {
        portrait.color = color;
        if (isShown)
            return;

        isShown = true;
        portrait.gameObject.SetActive(true);
        KeepBehindNamePanel();
        FollowDialoguePanel();
        StartFade(1f);
    }

    private void Hide()
    {
        if (!isShown)
            return;

        isShown = false;
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
        float start = group.alpha;
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / fadeDuration));
            yield return null;
        }

        group.alpha = target;
        if (target <= 0f)
            portrait.gameObject.SetActive(false);

        fadeCoroutine = null;
    }

    // 이름표 바로 뒤(대화창·이름표보다 먼저) 그려지도록 순서를 맞춥니다.
    private void KeepBehindNamePanel()
    {
        RectTransform namePanel = DialogueManager.Instance != null ? DialogueManager.Instance.NamePanelRect : null;
        Transform target = namePanel != null && namePanel.parent == portrait.transform.parent
            ? namePanel
            : DialogueManager.Instance.DialoguePanelRect;

        int targetIndex = target.GetSiblingIndex();
        int myIndex = portrait.transform.GetSiblingIndex();
        if (myIndex != targetIndex - 1)
            portrait.transform.SetSiblingIndex(myIndex < targetIndex ? targetIndex - 1 : targetIndex);
    }

    // 대화창 왼쪽 위 모서리에 맞춰 그림을 놓습니다.
    private void FollowDialoguePanel()
    {
        RectTransform panel = DialogueManager.Instance.DialoguePanelRect;
        RectTransform parent = portrait.rectTransform.parent as RectTransform;
        if (panel == null || parent == null)
            return;

        Vector3[] corners = new Vector3[4];
        panel.GetWorldCorners(corners);
        Vector2 topLeft = parent.InverseTransformPoint(corners[1]);

        // 부모 기준점(가운데) 좌표를 그대로 쓰도록 앵커를 부모 가운데에 둡니다.
        Vector2 parentPivotOffset = new Vector2(
            (0.5f - parent.pivot.x) * parent.rect.width,
            (0.5f - parent.pivot.y) * parent.rect.height);

        portrait.rectTransform.anchoredPosition =
            topLeft - parentPivotOffset + new Vector2(leftInset, -hiddenBelow);
    }

    private void CreatePortrait(Transform parent, Sprite sprite)
    {
        GameObject go = new GameObject(
            "여자친구 초상화",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(CanvasGroup));
        go.transform.SetParent(parent, false);

        portrait = go.GetComponent<Image>();
        portrait.sprite = sprite;
        portrait.preserveAspect = true;
        portrait.raycastTarget = false;

        RectTransform rect = portrait.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = Vector2.zero;
        float aspect = sprite.rect.width / sprite.rect.height;
        rect.sizeDelta = new Vector2(portraitHeight * aspect, portraitHeight);

        group = go.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        go.SetActive(false);
    }

    private static Sprite LoadPortrait()
    {
        Texture2D texture = Resources.Load<Texture2D>(PortraitResourcePath);
        if (texture == null)
        {
            Debug.LogWarning($"[DialoguePortrait] 초상화 이미지가 없습니다: Resources/{PortraitResourcePath}.png");
            return null;
        }

        return Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);
    }
}
