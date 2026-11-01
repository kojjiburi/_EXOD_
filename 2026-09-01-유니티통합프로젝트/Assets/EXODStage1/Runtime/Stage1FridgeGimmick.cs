using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 주방 냉장고 기믹을 맡습니다.
/// - 냉장고 메모를 누르면 메모 대사 (현관 비밀번호의 순서 단서)
/// - 냉장고(키패드)를 누르면 잠금 대사 후 숫자 키패드가 열립니다.
/// - 곰돌이 쪽지의 비밀번호(Stage1Dialogues.FridgePassword)를 넣으면 냉장고가 열리고,
///   열린 냉장고 안 그림(과일 개수 단서)을 화면에 띄운 채 대사가 나옵니다.
/// - 열린 냉장고를 다시 누르면 안 그림을 다시 보여 줍니다. [뒤로가기] 또는 방을 옮기면 닫힙니다.
/// 클릭 영역은 주방 배경 그림 위의 위치(비율)에 맞춰 실행 중에 만들어집니다.
/// 그림 파일: Assets/Resources/EXODStage1/Fridge_Open.png
/// </summary>
[DefaultExecutionOrder(260)]
public sealed class Stage1FridgeGimmick : MonoBehaviour
{
    private const string InsideResourcePath = "EXODStage1/Fridge_Open";
    private const string DimmerObjectName = "대화 배경 어둡게";

    // 주방 배경 그림(1920x1080) 기준 위치. (왼쪽 아래 x, y, 오른쪽 위 x, y) 비율
    private static readonly Rect FridgeArea = Rect.MinMaxRect(0.409f, 0.208f, 0.600f, 0.767f);
    private static readonly Rect MemoArea = Rect.MinMaxRect(0.470f, 0.575f, 0.515f, 0.650f);
    private static readonly Rect KeypadArea = Rect.MinMaxRect(0.553f, 0.550f, 0.588f, 0.628f);

    private bool isOpened;
    private GameObject insideView;

    public bool IsOpened => isOpened;
    public bool IsShowingInside => insideView != null && insideView.activeSelf;

    /// <summary>열린 냉장고 안 화면이 떠 있는지 (곰돌이 팔다리 등 방 물건을 숨길 때 사용)</summary>
    public static bool IsAnyInsideShown { get; private set; }

    private IEnumerator Start()
    {
        // SliderManager가 방 배경 화면들을 만든 뒤에 찾습니다.
        yield return null;

        RectTransform kitchen = Stage1Room.FindRoomView("주방");
        if (kitchen == null)
        {
            Debug.LogWarning("[Stage1Fridge] 주방 배경 화면을 찾을 수 없어 냉장고 기믹을 만들지 못했습니다.", this);
            yield break;
        }

        Stage1Room.CreateHotspot(kitchen, "냉장고 클릭 영역", FridgeArea, OnFridgeClicked);
        Stage1Room.CreateHotspot(kitchen, "냉장고 키패드 클릭 영역", KeypadArea, OnFridgeClicked);
        Stage1Room.CreateHotspot(kitchen, "냉장고 메모 클릭 영역", MemoArea,
            () => Stage1Room.PlayCutscene(Stage1Dialogues.FridgeMemo()));

        BuildInsideView(kitchen.parent);
        if (SliderManager.Instance != null)
            SliderManager.Instance.RoomChanged += OnRoomChanged;
    }

    private void OnDestroy()
    {
        DialogueDimmer.Suppressed = false;
        IsAnyInsideShown = false;
        if (SliderManager.Instance != null)
            SliderManager.Instance.RoomChanged -= OnRoomChanged;
    }

    private void LateUpdate()
    {
        if (IsShowingInside)
            KeepBehindDialogueUi();
    }

    private void OnFridgeClicked()
    {
        if (isOpened)
        {
            ShowInside();
            Stage1Room.PlayCutscene(Stage1Dialogues.FridgeAlreadyOpen());
            return;
        }

        // 틀리면 키패드에서 다시 입력합니다. (벌칙 없음)
        Stage1Room.PlayCutscene(Stage1Dialogues.FridgeLocked(), () =>
            Stage1Keypad.Show(Stage1Dialogues.FridgePassword, OnUnlocked));
    }

    private void OnUnlocked()
    {
        isOpened = true;
        Stage1Room.PlayCutscene(Stage1Dialogues.FridgeOpened(), () =>
        {
            ShowInside();
            Stage1Room.PlayCutscene(Stage1Dialogues.FridgeInside());
        });
    }

    private void OnRoomChanged(string roomName)
    {
        HideInside();
    }

    // ═══════════════════════════════════════════════════
    //  열린 냉장고 안 화면
    // ═══════════════════════════════════════════════════

    private void ShowInside()
    {
        if (insideView == null)
            return;

        insideView.SetActive(true);
        IsAnyInsideShown = true;
        KeepBehindDialogueUi();
        DialogueDimmer.Suppressed = true;  // 과일이 잘 보이도록 대화 중에도 어둡게 하지 않음
        EXODSound.Play("door_creak", 0.5f);
    }

    private void HideInside()
    {
        if (insideView != null)
            insideView.SetActive(false);
        IsAnyInsideShown = false;
        DialogueDimmer.Suppressed = false;
    }

    private void OnBackClicked()
    {
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueOpen)
            return;

        HideInside();
    }

    // 방 그림·곰돌이 팔다리보다 위, 대화 배경 어둡게·초상화·이름표·대화창보다 아래에 그립니다.
    private void KeepBehindDialogueUi()
    {
        Transform parent = insideView.transform.parent;
        DialogueManager dm = DialogueManager.Instance;
        DialoguePortrait portrait = FindFirstObjectByType<DialoguePortrait>();
        int targetIndex = int.MaxValue;
        foreach (Transform child in parent)
        {
            if (child == insideView.transform)
                continue;

            bool isDialogueUi = child.name == DimmerObjectName
                || (portrait != null && child == portrait.PortraitRect)
                || (dm != null && (child == dm.NamePanelRect || child == dm.DialoguePanelRect));
            if (isDialogueUi)
                targetIndex = Mathf.Min(targetIndex, child.GetSiblingIndex());
        }

        if (targetIndex == int.MaxValue)
            return;

        int myIndex = insideView.transform.GetSiblingIndex();
        if (myIndex != targetIndex - 1)
            insideView.transform.SetSiblingIndex(myIndex < targetIndex ? targetIndex - 1 : targetIndex);
    }

    private void BuildInsideView(Transform canvas)
    {
        Texture2D texture = Resources.Load<Texture2D>(InsideResourcePath);
        if (texture == null)
        {
            Debug.LogWarning($"[Stage1Fridge] 냉장고 안 그림이 없습니다: Resources/{InsideResourcePath}.png");
            return;
        }

        insideView = new GameObject("열린 냉장고 안", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        insideView.transform.SetParent(canvas, false);
        RectTransform rect = insideView.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image backdrop = insideView.GetComponent<Image>();
        backdrop.color = new Color(0.07f, 0.07f, 0.08f, 1f);
        backdrop.raycastTarget = true;  // 뒤의 방 클릭을 막습니다.

        // 아래 퀵슬롯에 가려지지 않도록 퀵슬롯 위 공간에 비율을 지켜 넣습니다.
        GameObject picture = new GameObject("냉장고 안 그림", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        picture.transform.SetParent(insideView.transform, false);
        RectTransform pictureRect = picture.GetComponent<RectTransform>();
        pictureRect.anchorMin = Vector2.zero;
        pictureRect.anchorMax = Vector2.one;
        pictureRect.offsetMin = new Vector2(0f, 230f);
        pictureRect.offsetMax = Vector2.zero;
        Image image = picture.GetComponent<Image>();
        image.sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        image.preserveAspect = true;
        image.raycastTarget = false;

        // 조사 화면과 같은 모양의 [뒤로가기] 버튼 (오른쪽 위 방 이름·타이머·대화 기록 버튼 아래)
        GameObject back = new GameObject("뒤로가기", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        back.transform.SetParent(insideView.transform, false);
        RectTransform backRect = back.GetComponent<RectTransform>();
        backRect.anchorMin = backRect.anchorMax = new Vector2(1f, 1f);
        backRect.pivot = new Vector2(1f, 1f);
        backRect.anchoredPosition = new Vector2(-34f, -228f);
        backRect.sizeDelta = new Vector2(300f, 96f);
        back.GetComponent<Image>().color = new Color(0.45f, 0.04f, 0.04f, 0.92f);
        back.GetComponent<Button>().onClick.AddListener(OnBackClicked);
        EXODSound.AttachUiSounds(back);

        GameObject label = new GameObject("글자", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        label.transform.SetParent(back.transform, false);
        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        TextMeshProUGUI text = label.GetComponent<TextMeshProUGUI>();
        if (EXODUiAssets.Font != null)
            text.font = EXODUiAssets.Font;
        text.text = "뒤로가기";
        text.fontSize = 44f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(0.92f, 0.90f, 0.90f, 1f);
        text.raycastTarget = false;

        insideView.SetActive(false);
    }
}
