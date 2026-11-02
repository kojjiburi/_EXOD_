using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 말하는 인물의 그림을 대화창 왼쪽 위에 띄웁니다.
/// - "???"      : 여자친구 검은 실루엣 (정체를 숨김)
/// - "여자친구"  : 여자친구 상반신 (아랫부분은 대화창 뒤로 가려짐)
/// - "곰인형…?" : 팔다리가 찢긴 곰돌이
/// - "곰인형"   : 다 고쳐진 곰돌이
/// 이름표는 그림 앞에 그려집니다.
/// 그림 파일: Assets/Resources/EXODStage1/ (Girlfriend_Portrait, Bear_Portrait_Torn, Bear_Portrait)
/// </summary>
[DefaultExecutionOrder(240)]
public sealed class DialoguePortrait : MonoBehaviour
{
    private const string PortraitResourcePath = "EXODStage1/Girlfriend_Portrait";
    private const string BearTornResourcePath = "EXODStage1/Bear_Portrait_Torn";
    private const string BearResourcePath = "EXODStage1/Bear_Portrait";

    [Header("=== 배치 (Canvas 기준 크기) ===")]
    [SerializeField] private float girlfriendHeight = 780f;      // 여자친구 그림 전체 높이
    [SerializeField] private float girlfriendHiddenBelow = 215f; // 대화창 뒤로 가려지는 높이
    [SerializeField] private float bearHeight = 360f;            // 곰돌이 그림 전체 높이
    [SerializeField] private float bearHiddenBelow = -72f;       // 곰돌이는 이름표 위로 올려 다 보이게
    [SerializeField] private float leftInset = 40f;              // 대화창 왼쪽 끝에서 안쪽으로 들어간 거리

    [Header("=== 연출 ===")]
    [SerializeField] private float fadeDuration = 0.2f;
    [SerializeField] private Color silhouetteColor = new Color(0.04f, 0.03f, 0.04f, 1f);

    private Image portrait;
    private CanvasGroup group;
    private Coroutine fadeCoroutine;
    private bool isShown;
    private float hiddenBelow;

    private Sprite girlfriendSprite;
    private Sprite bearTornSprite;
    private Sprite bearSprite;

    /// <summary>켜 두면 여자친구 대사여도 초상화를 띄우지 않습니다. (들켰을 때 컷신 — ScreenAberration이 켬)</summary>
    public static bool HideGirlfriend { get; set; }

    /// <summary>초상화 그림 (대화 배경 어둡게 효과가 그 뒤에 놓일 때 사용)</summary>
    public RectTransform PortraitRect => portrait != null ? portrait.rectTransform : null;

    private void Start()
    {
        DialogueManager dm = DialogueManager.Instance;
        if (dm == null || dm.DialoguePanelRect == null)
            return;

        girlfriendSprite = LoadPortrait();
        bearTornSprite = LoadSprite(BearTornResourcePath);
        bearSprite = LoadSprite(BearResourcePath);

        CreatePortrait(dm.DialoguePanelRect.parent);

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

        bool isGirlfriend = speaker == Stage1Dialogues.Unknown || speaker == Stage1Dialogues.Girlfriend || speaker == "얀데레" || speaker == "여주";
        if (isGirlfriend && HideGirlfriend)
            Hide();
        else if (speaker == Stage1Dialogues.Unknown)
            Show(girlfriendSprite, silhouetteColor, girlfriendHeight, girlfriendHiddenBelow);
        else if (speaker == Stage1Dialogues.Girlfriend || speaker == "얀데레" || speaker == "여주")
            Show(girlfriendSprite, Color.white, girlfriendHeight, girlfriendHiddenBelow);
        else if (speaker == Stage1Dialogues.BearUnknown)
            Show(bearTornSprite, Color.white, bearHeight, bearHiddenBelow);
        else if (speaker == Stage1Dialogues.Bear)
            Show(bearSprite, Color.white, bearHeight, bearHiddenBelow);
        else
            Hide();
    }

    private void Show(Sprite sprite, Color color, float height, float hidden)
    {
        if (sprite == null)
        {
            Hide();
            return;
        }

        portrait.sprite = sprite;
        portrait.color = color;
        portrait.rectTransform.sizeDelta = new Vector2(height * sprite.rect.width / sprite.rect.height, height);
        hiddenBelow = hidden;
        FollowDialoguePanel();

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

    private void CreatePortrait(Transform parent)
    {
        GameObject go = new GameObject(
            "여자친구 초상화",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(CanvasGroup));
        go.transform.SetParent(parent, false);

        portrait = go.GetComponent<Image>();
        portrait.preserveAspect = true;
        portrait.raycastTarget = false;

        RectTransform rect = portrait.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = Vector2.zero;

        group = go.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        go.SetActive(false);
    }

    /// <summary>여자친구 초상화 (게임 오버·시작 화면 실루엣에서도 사용)</summary>
    internal static Sprite LoadPortrait()
    {
        return LoadSprite(PortraitResourcePath);
    }

    private static Sprite LoadSprite(string resourcePath)
    {
        Texture2D texture = Resources.Load<Texture2D>(resourcePath);
        if (texture == null)
        {
            Debug.LogWarning($"[DialoguePortrait] 초상화 이미지가 없습니다: Resources/{resourcePath}.png");
            return null;
        }

        return Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);
    }
}
