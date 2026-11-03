using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 복도 끝 현관문 기믹과 1스테이지 끝을 맡습니다.
/// 1) 현관문을 누르면 도어락 키패드 (비밀번호: Stage1Dialogues.FrontDoorPassword)
///    - 틀리면 경보 → 여자친구에게 들켜 기절 (하트 -1)
/// 2) 맞히면 보안 질문 두 개 (예/아니요)
///    - 질문 1 '예', 질문 2 '아니요'를 고르면 기절. 다시 문을 누르면 질문부터 다시 시작합니다.
/// 3) 통과하면 현관문이 열리고 → 2스테이지(산책)로 갑니다. (Stage2HouseLink)
///    한 번 통과한 뒤에는 현관문을 누르면 바로 2스테이지로 갑니다. (그날 현관문이 잠겼으면 못 나감)
///    ※ 예전의 "벌써 밤이라고?" 끝 대사와 STAGE 1 CLEAR 화면(ShowStageClear)은 지금은 쓰지 않습니다.
/// </summary>
[DefaultExecutionOrder(260)]
public sealed class Stage1FrontDoorGimmick : MonoBehaviour
{
    private const string LobbySceneName = "StartScenes";

    // 복도 배경 그림(1920x1080) 기준 현관문 위치 비율
    private static readonly Rect DoorArea = Rect.MinMaxRect(0.469f, 0.500f, 0.526f, 0.648f);

    private bool hasLooked;
    private bool isPasswordSolved;
    private bool isCleared;

    public bool IsPasswordSolved => isPasswordSolved;

    /// <summary>일차 끝 점검에 걸리면 현관문은 다시 잠김 → 비밀번호부터 다시 풀어야 함 (Stage1ConfiscateOnFail, 사용자 결정 2026-11-03)</summary>
    public void Relock()
    {
        isPasswordSolved = false;
        isCleared = false;
    }

    private IEnumerator Start()
    {
        // SliderManager가 방 배경 화면들을 만든 뒤에 찾습니다.
        yield return null;

        RectTransform hallway = Stage1Room.FindRoomView("복도");
        if (hallway == null)
        {
            Debug.LogWarning("[Stage1FrontDoor] 복도 배경 화면을 찾을 수 없어 현관 기믹을 만들지 못했습니다.", this);
            yield break;
        }

        Stage1Room.CreateHotspot(hallway, "현관문 클릭 영역", DoorArea, OnDoorClicked);
    }

    private void OnDoorClicked()
    {
        // 이미 한 번 나가 봤으면 바로 2스테이지로 (컷신·잠금 확인은 Stage2HouseLink가 함)
        if (isCleared)
        {
            Stage2HouseLink.RequestEnter();
            return;
        }

        if (isPasswordSolved)
        {
            AskQuestions();
            return;
        }

        if (!hasLooked)
        {
            hasLooked = true;
            Stage1Room.PlayCutscene(Stage1Dialogues.FrontDoorFirstLook(), ShowKeypad);
        }
        else
        {
            ShowKeypad();
        }
    }

    private void ShowKeypad()
    {
        // 틀리면 기믹 실패 +10 대신 +20 (OnPasswordWrong)
        Stage1Keypad.Show(Stage1Dialogues.FrontDoorPassword, OnPasswordCorrect, OnPasswordWrong, false);
    }

    // 경보가 울리고 여자친구에게 들켜 기절합니다.
    // (2026-11-03 사용자 요청) 방 밖에서 들켰을 때와 같은 연출(UI 가림·색수차·컷신 이미지, 여자친구 초상화 없음) 위에서 대사 → 기절
    private void OnPasswordWrong()
    {
        DangerLevel.FrontDoorPasswordWrong();
        ScreenAberration.Play(() => Stage1Room.PlayCutscene(Stage1Dialogues.FrontDoorWrongPassword(), () =>
        {
            ScreenAberration.Stop();
            Stage1Room.Faint();
        }), false, ScreenAberration.HouseCutscene);
    }

    private void OnPasswordCorrect()
    {
        isPasswordSolved = true;
        Stage1Room.PlayCutscene(Stage1Dialogues.FrontDoorUnlocked(), AskQuestions);
    }

    private void AskQuestions()
    {
        Stage1Room.PlayChoiceCutscene(Stage1Dialogues.FrontDoorQuestion1(), Stage1Dialogues.YesNoChoices, first =>
        {
            if (first == 0)
            {
                Stage1Room.PlayCutscene(Stage1Dialogues.FrontDoorQuestion1Fail(), () => Stage1Room.Faint());
                return;
            }

            Stage1Room.PlayChoiceCutscene(Stage1Dialogues.FrontDoorQuestion2(), Stage1Dialogues.YesNoChoices, second =>
            {
                if (second == 1)
                {
                    Stage1Room.PlayCutscene(Stage1Dialogues.FrontDoorQuestion2Fail(), () => Stage1Room.Faint());
                    return;
                }

                Stage1Room.PlayCutscene(Stage1Dialogues.FrontDoorPassed(), LeaveHouse);
            });
        });
    }

    // 현관문을 열고 밖으로 나갑니다 → 2스테이지(산책)
    private void LeaveHouse()
    {
        isCleared = true;
        BoothMode.FrontDoorOpened = true;   // 부스 모드 '운명의 갈림길' [이지] 순응 엔딩 조건
        Stage2HouseLink.RequestEnter();
    }

    // ═══════════════════════════════════════════════════
    //  STAGE 1 CLEAR 화면
    // ═══════════════════════════════════════════════════

    private IEnumerator ShowStageClear()
    {
        GameObject root = new GameObject("스테이지 클리어", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
            typeof(GraphicRaycaster), typeof(CanvasGroup));
        root.transform.SetParent(transform, false);

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 950;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        CanvasGroup group = root.GetComponent<CanvasGroup>();
        group.alpha = 0f;

        Image background = CreateImage("배경", root.transform, new Color(0f, 0f, 0f, 0.92f));
        Stretch(background.rectTransform);

        TextMeshProUGUI title = CreateText("제목", root.transform, "STAGE 1 CLEAR", 110f, new Color(0.72f, 0.08f, 0.12f, 1f));
        Place(title.rectTransform, new Vector2(0f, 120f), new Vector2(1400f, 160f));

        TextMeshProUGUI subtitle = CreateText("부제", root.transform, "To be continued…", 40f, new Color(0.78f, 0.74f, 0.76f, 1f));
        Place(subtitle.rectTransform, new Vector2(0f, 10f), new Vector2(1200f, 70f));

        Image button = CreateImage("로비로 돌아가기", root.transform, Color.white);
        Place(button.rectTransform, new Vector2(0f, -130f), new Vector2(360f, 84f));
        Button uiButton = button.gameObject.AddComponent<Button>();
        ColorBlock colors = uiButton.colors;
        colors.normalColor = new Color(0.20f, 0.14f, 0.14f, 1f);
        colors.highlightedColor = new Color(0.45f, 0.16f, 0.20f, 1f);
        colors.pressedColor = new Color(0.62f, 0.20f, 0.26f, 1f);
        colors.selectedColor = colors.normalColor;
        uiButton.colors = colors;
        uiButton.onClick.AddListener(BackToLobby);
        EXODSound.AttachUiSounds(button.gameObject);

        TextMeshProUGUI label = CreateText("글자", button.transform, "로비로 돌아가기", 34f, Color.white);
        Stretch(label.rectTransform);

        EXODSound.Play("music_box", 0.7f);
        float elapsed = 0f;
        while (elapsed < 1.5f)
        {
            elapsed += Time.unscaledDeltaTime;
            group.alpha = Mathf.Clamp01(elapsed / 1.5f);
            yield return null;
        }
    }

    private static void BackToLobby()
    {
        InspectionState.ResetAll();
        if (QuickSlotInventory.Instance != null)
            QuickSlotInventory.Instance.Clear();

        SceneManager.LoadScene(LobbySceneName);
    }

    // ═══════════════════════════════════════════════════
    //  도우미
    // ═══════════════════════════════════════════════════

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string value, float size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        if (EXODUiAssets.Font != null)
            text.font = EXODUiAssets.Font;
        text.text = value;
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    private static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
