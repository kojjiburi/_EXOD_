using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;
using UnityEngine.EventSystems;

/// <summary>
/// 대화창 텍스트 매니저
/// Hierarchy: Canvas > 대화창 > 대화 텍스트 (TMP)
///            Canvas > 이름표  > 이름 텍스트 (TMP)
/// </summary>
public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [Header("=== UI 참조 ===")]
    [SerializeField] private GameObject dialoguePanel;      // 대화창
    [SerializeField] private TextMeshProUGUI dialogueText;     // 대화 텍스트
    [SerializeField] private GameObject namePanel;          // 이름표
    [SerializeField] private TextMeshProUGUI nameText;         // 이름 텍스트

    [Header("=== 타이핑 설정 ===")]
    [SerializeField] private float defaultTypingSpeed = 0.05f; // 기본 타이핑 속도 (초/글자)
    [SerializeField] private float fastTypingSpeed = 0.01f; // 빠른 타이핑 속도

    [Header("=== 입력 설정 ===")]
    [SerializeField] private KeyCode advanceKey = KeyCode.Space;  // 대화 진행 키
    [SerializeField] private KeyCode skipKey = KeyCode.LeftShift; // 타이핑 스킵 키

    [Header("=== 선택지 ===")]
    [SerializeField] private float choiceButtonWidth = 560f;  // 선택지 버튼 너비
    [SerializeField] private float choiceButtonHeight = 64f;  // 선택지 버튼 높이
    [SerializeField] private float choiceSpacing = 12f;       // 버튼 사이 간격, 대화창과의 간격

    [Header("=== 이벤트 ===")]
    public UnityEvent onDialogueStart;      // 대화 시작 시
    public UnityEvent onDialogueEnd;        // 대화 종료 시
    public UnityEvent onLineComplete;       // 한 줄 완료 시

    // ─── 내부 상태 ───────────────────────────────────────────
    private List<DialogueLine> _currentLines = new List<DialogueLine>();
    private int _lineIndex = 0;
    private bool _isTyping = false;
    private bool _isDialogueOpen = false;
    private Coroutine _typingCoroutine;

    // 선택지 상태
    private string[] _choices;
    private Action<int> _onChoiceSelected;
    private bool _isChoosing = false;
    private RectTransform _choiceRoot;
    private readonly List<GameObject> _choiceButtons = new List<GameObject>();

    // ─────────────────────────────────────────────────────────

    void Awake()
    {
        // 싱글톤
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        CloseDialogue();
    }

    // [터치] 대사가 떠 있을 때 화면 아무 데나 눌러도 '다음' 버튼과 같음
    private bool _pressStartedWhileOpen;
    private readonly List<RaycastResult> _clickHits = new List<RaycastResult>();
    /// <summary>화면 클릭으로 대사를 넘긴 프레임 (같은 클릭이 다른 곳에서 또 쓰이지 않게)</summary>
    public static int LastClickAdvanceFrame { get; private set; } = -1;

    void Update()
    {
        if (!_isDialogueOpen) { _pressStartedWhileOpen = false; return; }

        if (Input.GetMouseButtonDown(0)) _pressStartedWhileOpen = true;
        if (Input.GetMouseButtonUp(0) && _pressStartedWhileOpen)
        {
            _pressStartedWhileOpen = false;
            if (!_isChoosing && !IsPointerOnOtherButton(Input.mousePosition))
            {
                LastClickAdvanceFrame = Time.frameCount;
                AdvanceDialogue();
                return;
            }
        }

        // 선택지가 떠 있으면 숫자 키(1, 2, …)로만 고를 수 있습니다.
        if (_isChoosing)
        {
            for (int i = 0; i < _choices.Length && i < 9; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
                {
                    Choose(i);
                    return;
                }
            }
            return;
        }

        // 스킵 키 → 타이핑 즉시 완료
        if (Input.GetKey(skipKey) && _isTyping)
        {
            SkipTyping();
            return;
        }

        // 진행 키 → 다음 줄 or 타이핑 스킵
        if (Input.GetKeyDown(advanceKey))
            AdvanceDialogue();
    }

    // ═══════════════════════════════════════════════════
    //  Public API
    // ═══════════════════════════════════════════════════

    /// <summary>ScriptableObject로 대화 시작</summary>
    public void StartDialogue(DialogueData data)
    {
        if (data == null || data.lines.Count == 0)
        {
            Debug.LogWarning("[DialogueManager] 대화 데이터가 없습니다.");
            return;
        }
        StartDialogue(data.lines);
    }

    /// <summary>직접 리스트로 대화 시작</summary>
    public void StartDialogue(List<DialogueLine> lines)
    {
        // 새 대화가 시작되면 이전 대화에 걸려 있던 선택지는 취소합니다.
        HideChoices();
        _choices = null;
        _onChoiceSelected = null;

        _currentLines = lines;
        _lineIndex = 0;
        _isDialogueOpen = true;

        dialoguePanel.SetActive(true);
        onDialogueStart?.Invoke();

        ShowLine(_currentLines[_lineIndex]);
    }

    /// <summary>
    /// 인물 대화 간편 호출
    /// </summary>
    public void StartCharacterDialogue(string speakerName, string[] texts)
    {
        var lines = new List<DialogueLine>();
        foreach (var t in texts)
            lines.Add(new DialogueLine
            {
                speakerName = speakerName,
                text = t,
                dialogueType = DialogueType.Character
            });
        StartDialogue(lines);
    }

    /// <summary>
    /// 사물 상호작용 간편 호출 (이름표 없음)
    /// </summary>
    public void StartInteractionDialogue(string objectName, string[] texts)
    {
        var lines = new List<DialogueLine>();
        foreach (var t in texts)
            lines.Add(new DialogueLine
            {
                speakerName = objectName,   // 사물 이름 표시
                text = t,
                dialogueType = DialogueType.Interaction
            });
        StartDialogue(lines);
    }

    /// <summary>
    /// 대사를 보여준 뒤 마지막 줄에서 선택지를 띄웁니다.
    /// 선택하면 대화창이 닫히고 onChosen(고른 번호, 0부터)이 호출됩니다.
    /// </summary>
    public void StartChoiceDialogue(List<DialogueLine> lines, string[] choices, Action<int> onChosen)
    {
        if (lines == null || lines.Count == 0 || choices == null || choices.Length == 0)
        {
            Debug.LogWarning("[DialogueManager] 선택지 대화에 대사 또는 선택지가 없습니다.");
            return;
        }

        StartDialogue(lines);
        _choices = choices;
        _onChoiceSelected = onChosen;

        // 마지막 줄이 이미 다 표시된 경우(빈 문장 등)에도 선택지를 띄웁니다.
        TryShowChoices();
    }

    /// <summary>현재 대화 강제 종료</summary>
    public void ForceCloseDialogue()
    {
        if (_typingCoroutine != null) StopCoroutine(_typingCoroutine);
        HideChoices();
        _choices = null;
        _onChoiceSelected = null;
        CloseDialogue();
    }

    public bool IsDialogueOpen => _isDialogueOpen;
    public bool IsChoosing => _isChoosing;

    /// <summary>새 대사 한 줄이 표시되기 시작할 때 호출됩니다. (초상화 등)</summary>
    public event Action<DialogueLine> LineShown;

    /// <summary>타이핑 중 글자가 하나 찍힐 때 호출됩니다. (타이핑 효과음)</summary>
    public event Action<char> CharacterTyped;

    public RectTransform DialoguePanelRect => dialoguePanel != null ? dialoguePanel.transform as RectTransform : null;
    public RectTransform NamePanelRect => namePanel != null ? namePanel.transform as RectTransform : null;

    /// <summary>
    /// 대화창의 '다음' 버튼과 Space 키가 함께 사용하는 진행 기능입니다.
    /// 타이핑 중이면 현재 문장을 즉시 완성하고, 완성된 상태면 다음 줄로 넘어갑니다.
    /// </summary>
    public void AdvanceDialogue()
    {
        if (!_isDialogueOpen || _isChoosing)
            return;

        if (_isTyping)
            SkipTyping();
        else
            NextLine();
    }

    // [터치] 누른 곳 맨 위가 버튼(다음 버튼·선택지·퀵슬롯 등)이면 그 버튼이 처리하도록 화면 클릭으로 치지 않음
    private bool IsPointerOnOtherButton(Vector2 screen)
    {
        EventSystem es = EventSystem.current;
        if (es == null) return false;
        _clickHits.Clear();
        es.RaycastAll(new PointerEventData(es) { position = screen }, _clickHits);
        if (_clickHits.Count == 0) return false;
        Selectable selectable = _clickHits[0].gameObject.GetComponentInParent<Selectable>();
        return selectable != null && selectable.enabled && selectable.IsInteractable();
    }

    // ═══════════════════════════════════════════════════
    //  내부 로직
    // ═══════════════════════════════════════════════════

    private void ShowLine(DialogueLine line)
    {
        // 이름표 처리
        bool showName = !string.IsNullOrEmpty(line.speakerName);
        namePanel.SetActive(showName);
        if (showName)
        {
            string speaker = line.speakerName.Trim();
            if (speaker == "얀데레" || speaker == "여주")
                speaker = "여자친구";

            nameText.text = speaker;
        }

        LineShown?.Invoke(line);

        // 타이핑 시작
        if (_typingCoroutine != null) StopCoroutine(_typingCoroutine);
        float speed = line.typingSpeed > 0 ? line.typingSpeed : defaultTypingSpeed;
        _typingCoroutine = StartCoroutine(TypeText(line.text, speed));
    }

    private IEnumerator TypeText(string text, float speed)
    {
        _isTyping = true;
        dialogueText.text = "";

        foreach (char c in text)
        {
            dialogueText.text += c;
            CharacterTyped?.Invoke(c);
            yield return new WaitForSeconds(speed);
        }

        _isTyping = false;
        onLineComplete?.Invoke();
        TryShowChoices();
    }

    private void SkipTyping()
    {
        if (_typingCoroutine != null) StopCoroutine(_typingCoroutine);
        dialogueText.text = _currentLines[_lineIndex].text;
        _isTyping = false;
        onLineComplete?.Invoke();
        TryShowChoices();
    }

    private void NextLine()
    {
        _lineIndex++;

        if (_lineIndex < _currentLines.Count)
        {
            ShowLine(_currentLines[_lineIndex]);
        }
        else
        {
            CloseDialogue();
        }
    }

    private void CloseDialogue()
    {
        _isDialogueOpen = false;
        dialoguePanel.SetActive(false);
        namePanel.SetActive(false);
        dialogueText.text = "";
        onDialogueEnd?.Invoke();
    }

    // ═══════════════════════════════════════════════════
    //  선택지
    // ═══════════════════════════════════════════════════

    private void TryShowChoices()
    {
        bool isLastLine = _lineIndex == _currentLines.Count - 1;
        if (_choices == null || _isChoosing || _isTyping || !isLastLine || !_isDialogueOpen)
            return;

        EnsureChoiceRoot();

        for (int i = 0; i < _choices.Length; i++)
        {
            if (i >= _choiceButtons.Count)
                _choiceButtons.Add(CreateChoiceButton(i));

            GameObject button = _choiceButtons[i];
            button.GetComponentInChildren<TextMeshProUGUI>().text = $"{i + 1}. {_choices[i]}";
            button.SetActive(true);
        }

        for (int i = _choices.Length; i < _choiceButtons.Count; i++)
            _choiceButtons[i].SetActive(false);

        _choiceRoot.gameObject.SetActive(true);
        _isChoosing = true;
        SetNextButtonsVisible(false);
    }

    private void Choose(int index)
    {
        if (!_isChoosing || _choices == null || index < 0 || index >= _choices.Length)
            return;

        Action<int> callback = _onChoiceSelected;
        HideChoices();
        _choices = null;
        _onChoiceSelected = null;
        CloseDialogue();

        // 콜백 안에서 다음 대화를 바로 시작할 수 있도록 창을 닫은 뒤에 호출합니다.
        callback?.Invoke(index);
    }

    private void HideChoices()
    {
        _isChoosing = false;
        if (_choiceRoot != null)
            _choiceRoot.gameObject.SetActive(false);
        SetNextButtonsVisible(true);
    }

    // 선택지가 떠 있는 동안에는 눌러도 반응하지 않는 '다음' 버튼을 숨깁니다.
    private void SetNextButtonsVisible(bool visible)
    {
        if (dialoguePanel == null)
            return;

        foreach (DialogueNextButton nextButton in dialoguePanel.GetComponentsInChildren<DialogueNextButton>(true))
            nextButton.gameObject.SetActive(visible);
    }

    private void EnsureChoiceRoot()
    {
        if (_choiceRoot != null)
            return;

        // 대화창의 자식으로 만들어 대화창과 함께 맨 앞에 그려지고 함께 숨겨집니다.
        GameObject rootObject = new GameObject(
            "선택지",
            typeof(RectTransform),
            typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter),
            typeof(LayoutElement));
        rootObject.transform.SetParent(dialoguePanel.transform, false);
        rootObject.GetComponent<LayoutElement>().ignoreLayout = true;

        _choiceRoot = rootObject.GetComponent<RectTransform>();
        _choiceRoot.anchorMin = new Vector2(0.5f, 1f);
        _choiceRoot.anchorMax = new Vector2(0.5f, 1f);
        _choiceRoot.pivot = new Vector2(0.5f, 0f);
        _choiceRoot.anchoredPosition = new Vector2(0f, choiceSpacing);
        _choiceRoot.sizeDelta = new Vector2(choiceButtonWidth, 0f);

        VerticalLayoutGroup layout = rootObject.GetComponent<VerticalLayoutGroup>();
        layout.spacing = choiceSpacing;
        layout.childAlignment = TextAnchor.LowerCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        rootObject.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        rootObject.SetActive(false);
    }

    private GameObject CreateChoiceButton(int index)
    {
        GameObject buttonObject = new GameObject(
            $"선택지 {index + 1}",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement));
        buttonObject.transform.SetParent(_choiceRoot, false);
        buttonObject.GetComponent<LayoutElement>().preferredHeight = choiceButtonHeight;

        Image image = buttonObject.GetComponent<Image>();
        image.color = Color.white;

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.13f, 0.10f, 0.10f, 0.88f);
        colors.highlightedColor = new Color(0.45f, 0.16f, 0.20f, 0.95f);
        colors.pressedColor = new Color(0.62f, 0.20f, 0.26f, 1f);
        colors.selectedColor = colors.normalColor;
        button.colors = colors;
        button.onClick.AddListener(() => Choose(index));

        GameObject textObject = new GameObject(
            "선택지 글자",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        textObject.transform.SetParent(buttonObject.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(16f, 6f);
        textRect.offsetMax = new Vector2(-16f, -6f);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = 28f;
        text.fontStyle = FontStyles.Normal;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;

        // 대화 텍스트와 같은 폰트(Galmuri9)를 사용합니다.
        if (dialogueText != null && dialogueText.font != null)
            text.font = dialogueText.font;

        return buttonObject;
    }

    // Play 중 Inspector의 DialogueManager 컴포넌트 ⋮ 메뉴에서 실행해 선택지를 확인합니다.
    [ContextMenu("선택지 테스트")]
    private void TestChoiceDialogue()
    {
        StartChoiceDialogue(
            new List<DialogueLine>
            {
                new DialogueLine { speakerName = "???", text = "무슨 생각해?", dialogueType = DialogueType.Character }
            },
            new[] { "아..아무것도 아냐!", "…너 생각해.." },
            choice => StartCharacterDialogue("???", new[]
            {
                choice == 0 ? "뭐야? 내 생각은 안하는 거야?" : "역시 그렇지?"
            }));
    }
}
