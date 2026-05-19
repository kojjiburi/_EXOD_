using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;

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

    void Update()
    {
        if (!_isDialogueOpen) return;

        // 스킵 키 → 타이핑 즉시 완료
        if (Input.GetKey(skipKey) && _isTyping)
        {
            SkipTyping();
            return;
        }

        // 진행 키 → 다음 줄 or 타이핑 스킵
        if (Input.GetKeyDown(advanceKey))
        {
            if (_isTyping)
                SkipTyping();
            else
                NextLine();
        }
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

    /// <summary>현재 대화 강제 종료</summary>
    public void ForceCloseDialogue()
    {
        if (_typingCoroutine != null) StopCoroutine(_typingCoroutine);
        CloseDialogue();
    }

    public bool IsDialogueOpen => _isDialogueOpen;

    // ═══════════════════════════════════════════════════
    //  내부 로직
    // ═══════════════════════════════════════════════════

    private void ShowLine(DialogueLine line)
    {
        // 이름표 처리
        bool showName = !string.IsNullOrEmpty(line.speakerName);
        namePanel.SetActive(showName);
        if (showName)
            nameText.text = line.speakerName;

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
            yield return new WaitForSeconds(speed);
        }

        _isTyping = false;
        onLineComplete?.Invoke();
    }

    private void SkipTyping()
    {
        if (_typingCoroutine != null) StopCoroutine(_typingCoroutine);
        dialogueText.text = _currentLines[_lineIndex].text;
        _isTyping = false;
        onLineComplete?.Invoke();
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
}