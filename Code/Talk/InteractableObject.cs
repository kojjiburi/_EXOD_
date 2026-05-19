using UnityEngine;

/// <summary>
/// 사물에 붙이는 상호작용 컴포넌트
/// - 플레이어가 범위 안에 들어오면 E키로 대화 시작
/// </summary>
public class InteractableObject : MonoBehaviour
{
    [Header("=== 상호작용 설정 ===")]
    [SerializeField] private string objectName = "???";         // 이름표에 표시될 이름
    [SerializeField] private DialogueData dialogueData;         // ScriptableObject 연결

    [Header("=== 직접 입력 (ScriptableObject 없을 때) ===")]
    [SerializeField] private string[] directTexts;              // 직접 텍스트 입력

    [Header("=== 입력 키 ===")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    private bool _playerInRange = false;

    void Update()
    {
        if (_playerInRange && Input.GetKeyDown(interactKey))
        {
            Interact();
        }
    }

    private void Interact()
    {
        if (DialogueManager.Instance == null) return;
        if (DialogueManager.Instance.IsDialogueOpen) return;

        // ScriptableObject 우선
        if (dialogueData != null)
        {
            DialogueManager.Instance.StartDialogue(dialogueData);
        }
        else if (directTexts != null && directTexts.Length > 0)
        {
            DialogueManager.Instance.StartInteractionDialogue(objectName, directTexts);
        }
        else
        {
            Debug.LogWarning($"[InteractableObject] '{objectName}'에 대화 데이터가 없습니다.");
        }
    }

    // ─── 충돌 감지 (Collider 2D) ───────────────────────────
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player")) _playerInRange = true;
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player")) _playerInRange = false;
    }

    // 3D 사용 시 아래로 교체
    // void OnTriggerEnter(Collider other)  { if (other.CompareTag("Player")) _playerInRange = true; }
    // void OnTriggerExit(Collider other)   { if (other.CompareTag("Player")) _playerInRange = false; }
}