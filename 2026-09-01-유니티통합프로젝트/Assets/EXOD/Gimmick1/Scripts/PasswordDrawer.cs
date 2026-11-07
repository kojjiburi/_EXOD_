using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 침실 서랍의 세 자리 비밀번호를 검사하고 정답이면 서랍을 엽니다.
/// [터치] 입력 상자 오른쪽에 숫자 패드(1~9, ←, 0, 확인)를 붙여 키보드 없이도 입력할 수 있습니다.
///   터치 화면에서는 입력칸을 자동으로 선택하지 않고 화상 키보드도 띄우지 않습니다.
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
    private int lastTypedLength;
    private GameObject numberPad;   // [터치] 숫자 패드 (처음 열 때 만듦)

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
        {
            passwordInput.onSubmit.AddListener(_ => Submit());
            passwordInput.onValueChanged.AddListener(OnPasswordTyped);
        }

        if (passwordPanel != null)
            passwordPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(Submit);

        if (backButton != null)
            backButton.onClick.RemoveListener(Close);

        if (passwordInput != null)
            passwordInput.onValueChanged.RemoveListener(OnPasswordTyped);
    }

    // 숫자를 하나 입력할 때마다 버튼 누르는 소리를 냅니다. (지울 때는 소리 없음)
    private void OnPasswordTyped(string value)
    {
        if (value.Length > lastTypedLength)
            EXODSound.Play("keypad_press");
        lastTypedLength = value.Length;
    }

    public void Open()
    {
        if (isUnlocked)
        {
            ApplyPersistentVisual();

            if (isKeyCollected)
            {
                // 열쇠를 이미 챙긴 뒤 다시 누르면 대사 없음 (사용자 요청 2026-11-03)
            }
            else
            {
                CollectKey();
            }
            return;
        }

        if (passwordPanel == null || passwordInput == null)
            return;

        // [위험도] 틀리면 100이 될 상황이면 경고만 하고 이번엔 열지 않음
        if (DangerLevel.WarnBeforeGimmick())
            return;
        DangerLevel.UiOpened();

        passwordPanel.SetActive(true);
        passwordInput.text = string.Empty;
        EnsureNumberPad();
        FocusInput();

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
            EXODSound.Play("door_lock");
            DangerLevel.GimmickSolved();

            if (InspectionManager.Instance != null)
                InspectionManager.Instance.SetCurrentDetailSprite(openedDrawerSprite);

            if (passwordPanel != null)
                passwordPanel.SetActive(false);

            ShowDialogue(new[]
            {
                "철컥, 잠금장치가 풀렸다.",
                "서랍 안에서 황금 열쇠를 발견했다.",
                "서랍에 놓인 열쇠를 줍자."
            });
        }
        else
        {
            EXODSound.Play("keypad_error");
            DangerLevel.GimmickFailed();
            if (resultText != null)
                resultText.text = "비밀번호가 맞지 않는다.";

            passwordInput.text = string.Empty;
            FocusInput();
        }
    }

    // ─── [터치] 숫자 패드 ───

    // 키보드로도 칠 수 있게 입력칸을 선택. 터치 화면이면 화상 키보드가 뜨지 않게 선택하지 않음.
    private void FocusInput()
    {
        passwordInput.shouldHideSoftKeyboard = true;
        if (!Input.touchSupported)
            passwordInput.ActivateInputField();
    }

    private void PressDigit(int digit)
    {
        if (passwordInput == null || isUnlocked)
            return;
        int limit = passwordInput.characterLimit > 0 ? passwordInput.characterLimit : 3;
        if (passwordInput.text.Length >= limit)
            return;
        passwordInput.text += digit.ToString();   // onValueChanged → 버튼 소리
    }

    private void PressBackspace()
    {
        if (passwordInput == null || isUnlocked || passwordInput.text.Length == 0)
            return;
        passwordInput.text = passwordInput.text.Substring(0, passwordInput.text.Length - 1);
        EXODSound.Play("keypad_press", 0.6f);
    }

    // 입력 상자 오른쪽에 3×4 숫자 패드: 1 2 3 / 4 5 6 / 7 8 9 / ← 0 확인
    private void EnsureNumberPad()
    {
        if (numberPad != null || passwordInput == null)
            return;
        RectTransform box = passwordInput.transform.parent as RectTransform;
        if (box == null)
            return;

        const float key = 96f, gap = 10f, pad = 16f;
        Vector2 padSize = new Vector2(key * 3f + gap * 2f + pad * 2f, key * 4f + gap * 3f + pad * 2f);

        numberPad = new GameObject("숫자 패드 (터치)", typeof(RectTransform), typeof(Image));
        numberPad.transform.SetParent(box, false);
        RectTransform padRect = (RectTransform)numberPad.transform;
        padRect.anchorMin = padRect.anchorMax = new Vector2(1f, 0.5f);
        padRect.pivot = new Vector2(0f, 0.5f);
        padRect.anchoredPosition = new Vector2(24f, 0f);
        padRect.sizeDelta = padSize;
        Image boxImage = box.GetComponent<Image>();
        numberPad.GetComponent<Image>().color = boxImage != null ? boxImage.color : new Color(0.11f, 0.075f, 0.07f, 0.98f);

        TMP_FontAsset font = resultText != null ? resultText.font : null;
        string[] labels = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "←", "0", "확인" };
        for (int i = 0; i < labels.Length; i++)
        {
            int col = i % 3, row = i / 3;
            Vector2 pos = new Vector2(pad + col * (key + gap) + key * 0.5f, -(pad + row * (key + gap) + key * 0.5f));

            GameObject keyObject = new GameObject("키 " + labels[i], typeof(RectTransform), typeof(Image), typeof(Button));
            keyObject.transform.SetParent(numberPad.transform, false);
            RectTransform keyRect = (RectTransform)keyObject.transform;
            keyRect.anchorMin = keyRect.anchorMax = new Vector2(0f, 1f);
            keyRect.pivot = new Vector2(0.5f, 0.5f);
            keyRect.anchoredPosition = pos;
            keyRect.sizeDelta = new Vector2(key, key);

            bool isConfirm = labels[i] == "확인";
            keyObject.GetComponent<Image>().color = isConfirm ? new Color(0.38f, 0.10f, 0.09f, 1f) : new Color(0.92f, 0.88f, 0.80f, 1f);

            GameObject labelObject = new GameObject("글자", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(keyObject.transform, false);
            RectTransform labelRect = (RectTransform)labelObject.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
            if (font != null)
                label.font = font;
            label.text = labels[i];
            label.fontSize = isConfirm ? 30f : 44f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = isConfirm ? Color.white : new Color(0.18f, 0.06f, 0.05f);
            label.raycastTarget = false;

            Button button = keyObject.GetComponent<Button>();
            if (isConfirm)
                button.onClick.AddListener(Submit);
            else if (labels[i] == "←")
                button.onClick.AddListener(PressBackspace);
            else
            {
                int digit = int.Parse(labels[i]);
                button.onClick.AddListener(() => PressDigit(digit));
            }
        }
    }

    /// <summary>비밀번호 창이 열려 있는지 (잠수 방지 Stage1AntiIdle)</summary>
    public bool IsPanelOpen => passwordPanel != null && passwordPanel.activeSelf;

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

    /// <summary>
    /// 돌려놓은 열쇠를 서랍 안에 다시 넣습니다. (퀵슬롯에서 돌려놓기 또는 일차 초기화)
    /// 서랍은 열린 채로 열쇠가 든 그림이 되고, 다시 누르면 처음처럼 주울 수 있습니다.
    /// </summary>
    public void ReturnKey()
    {
        if (!isKeyCollected)
            return;

        isKeyCollected = false;
        if (InspectionManager.Instance != null && InspectionManager.Instance.IsOpen &&
            InspectionManager.Instance.CurrentItemId == "bedroom_drawer")
        {
            ApplyPersistentVisual();
        }
    }

    private void CollectKey()
    {
        isKeyCollected = true;
        EXODSound.Play("item_pickup");
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
