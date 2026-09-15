using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 침실 서랍의 세 자리 비밀번호를 검사하고 정답이면 서랍을 엽니다.
/// </summary>
public class PasswordDrawer : MonoBehaviour
{
    [Header("비밀번호 화면")]
    [SerializeField] private GameObject passwordPanel;
    [SerializeField] private TMP_InputField passwordInput;
    [SerializeField] private TextMeshProUGUI resultText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button backButton;

    [Header("정답과 결과")]
    [SerializeField] private string correctPassword = "724";
    [SerializeField] private Sprite openedDrawerSprite;
    [SerializeField] private Sprite emptyOpenedDrawerSprite;

    [Header("획득 아이템 연결")]
    [SerializeField] private string keyItemId = "golden_key";
    [SerializeField] private UnityEvent onKeyCollected;

    private bool isUnlocked;
    private bool isKeyCollected;

    public bool IsUnlocked => isUnlocked;
    public bool IsKeyCollected => isKeyCollected;
    public string KeyItemId => keyItemId;

    private void Awake()
    {
        // 새 플레이는 항상 잠긴 서랍에서 시작합니다.
        // 같은 플레이 도중에는 아래 상태값이 유지되므로 한 번 연 서랍은 다시 잠기지 않습니다.
        isUnlocked = false;
        isKeyCollected = false;

        if (confirmButton != null)
            confirmButton.onClick.AddListener(Submit);

        if (backButton != null)
            backButton.onClick.AddListener(Close);

        if (passwordInput != null)
            passwordInput.onSubmit.AddListener(_ => Submit());

        if (passwordPanel != null)
            passwordPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(Submit);

        if (backButton != null)
            backButton.onClick.RemoveListener(Close);
    }

    public void Open()
    {
        if (isUnlocked)
        {
            ApplyPersistentVisual();

            if (isKeyCollected)
            {
                ShowDialogue(new[] { "열린 서랍이다.", "황금 열쇠는 이미 챙겼다." });
            }
            else
            {
                CollectKey();
            }
            return;
        }

        if (passwordPanel == null || passwordInput == null)
            return;

        passwordPanel.SetActive(true);
        passwordInput.text = string.Empty;
        passwordInput.ActivateInputField();

        if (resultText != null)
            resultText.text = "달력, 사진, 메모에서 찾은 숫자를 입력하자.";
    }

    public void Submit()
    {
        if (passwordInput == null || isUnlocked)
            return;

        string value = passwordInput.text.Trim();
        if (value == correctPassword)
        {
            isUnlocked = true;

            if (InspectionManager.Instance != null)
                InspectionManager.Instance.SetCurrentDetailSprite(openedDrawerSprite);

            if (passwordPanel != null)
                passwordPanel.SetActive(false);

            ShowDialogue(new[]
            {
                "철컥, 잠금장치가 풀렸다.",
                "서랍 안에서 황금 열쇠를 발견했다.",
                "대사가 끝난 뒤 열쇠가 놓인 서랍 그림을 눌러서 줍자."
            });
        }
        else
        {
            if (resultText != null)
                resultText.text = "비밀번호가 맞지 않는다.";

            passwordInput.text = string.Empty;
            passwordInput.ActivateInputField();
        }
    }

    public void Close()
    {
        if (passwordPanel != null)
            passwordPanel.SetActive(false);
    }

    public void Configure(
        GameObject panel,
        TMP_InputField input,
        TextMeshProUGUI result,
        Button confirm,
        Button back,
        Sprite openedSprite,
        Sprite emptyOpenedSprite)
    {
        passwordPanel = panel;
        passwordInput = input;
        resultText = result;
        confirmButton = confirm;
        backButton = back;
        openedDrawerSprite = openedSprite;
        emptyOpenedDrawerSprite = emptyOpenedSprite;
    }

    // 이전 자동 설치 코드와의 호환을 위한 연결 메서드입니다.
    public void Configure(
        GameObject panel,
        TMP_InputField input,
        TextMeshProUGUI result,
        Button confirm,
        Button back,
        Sprite openedSprite)
    {
        Configure(panel, input, result, confirm, back, openedSprite, null);
    }

    public void ApplyPersistentVisual()
    {
        if (!isUnlocked || InspectionManager.Instance == null)
            return;

        Sprite sprite = isKeyCollected && emptyOpenedDrawerSprite != null
            ? emptyOpenedDrawerSprite
            : openedDrawerSprite;
        InspectionManager.Instance.SetCurrentDetailSprite(sprite);
    }

    private void CollectKey()
    {
        isKeyCollected = true;
        ApplyPersistentVisual();
        onKeyCollected?.Invoke();

        ShowDialogue(new[] { "황금 열쇠를 주웠다." });
    }

    private static void ShowDialogue(string[] texts)
    {
        if (DialogueManager.Instance != null)
            DialogueManager.Instance.StartInteractionDialogue("서랍장", texts);
    }
}
