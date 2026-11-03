using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 화장실 선반의 손거울 → 거울 미로(MirrorMazeGimmick, 친구 main 기믹) → 전화 부스 열쇠 (2026-11-02 사용자 결정)
/// - 화장실 선반 오른쪽 칸(금고 왼쪽)에 분홍 손거울 그림을 세워 두고, 누르면 거울 미로가 열립니다.
/// - 출구에 도착하면 미로 화면 선반 위로 부스 열쇠가 떨어지고, 눌러서 주우면 퀵슬롯에 들어옵니다.
///   (2026-11-02 친구 main 99c6166 "미로기믹 수정본" 방식, MirrorMazeReward가 퀵슬롯에 넣음 — 이 파일은 열쇠를 따로 주지 않음)
///   미로를 닫으면 "열쇠고리에 'TELEPHONE'…" 대사. (일차 끝 점검 예외 아이템: ItemPositionManager.AlwaysAllowedInInventoryItemIds)
/// - 벽에 3번 부딪히면 닫히고 다시 도전할 수 있습니다.
/// - 친구 위험도 규칙: 열기 전 WarnBeforeGimmick·UiOpened, 성공 GimmickSolved(+5), 실패 GimmickFailed(+10)
/// 대사: Stage1Dialogues.MirrorKeyFound / MirrorFailed / MirrorEmpty
/// 실행할 때 SliderManager가 있는 씬에 자동으로 붙습니다.
/// </summary>
[DefaultExecutionOrder(230)]
public sealed class Stage1MirrorBoothKey : MonoBehaviour
{
    // 화장실 그림(1920x1080, 위쪽이 y=0) 기준 손거울 자리: 선반 오른쪽 칸, 금고(1318~) 왼쪽
    private static readonly RectInt MirrorRect = new RectInt(1110, 38, 120, 150);
    private const string MirrorImagePath = "EXODStage1/HandMirror";
    private const string KeyIconPath = "EXODStage1/BoothKeyIcon";

    private bool keyGiven;
    private bool opened;
    private ItemData boothKey;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHandler()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single)
            return;

        SliderManager slider = FindFirstObjectByType<SliderManager>();
        if (slider != null && slider.GetComponent<Stage1MirrorBoothKey>() == null)
            slider.gameObject.AddComponent<Stage1MirrorBoothKey>();
    }

    private IEnumerator Start()
    {
        // 새 판(씬을 새로 불러옴)마다 거울 미로 기록을 지웁니다. (static이라 다시 시작/로비 뒤에도 남아 있어서
        // '이미 깬 상태·열쇠 이미 주움'으로 열려 열쇠를 못 받던 문제) 같은 판 안에서는 기록 유지
        ResetMirrorMazeRecord("IsSolved");
        ResetMirrorMazeRecord("RewardTaken");

        // SliderManager가 방 화면을 만든 뒤에 찾습니다.
        yield return null;
        yield return null;

        Transform restroom = RestroomGimmick.FindRoomView();
        if (restroom == null)
        {
            Debug.LogWarning("[MirrorBoothKey] 화장실 화면을 찾지 못해 손거울을 놓지 못했습니다.", this);
            yield break;
        }

        Sprite mirror = LoadSprite(MirrorImagePath);
        if (mirror == null)
            yield break;

        GameObject go = new GameObject("손거울 (거울 미로)", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(restroom, false);
        RestroomGimmick.PlaceByPixels(go.GetComponent<RectTransform>(), MirrorRect);
        Image image = go.GetComponent<Image>();
        image.sprite = mirror;
        image.preserveAspect = true;

        Button button = go.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.92f, 0.96f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        colors.selectedColor = colors.normalColor;
        button.colors = colors;
        button.onClick.AddListener(OnMirrorClicked);

        boothKey = CreateBoothKey();
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void OnMirrorClicked()
    {
        if (Stage1Room.IsBlocked() || MirrorMazeGimmick.IsOpen || Time.frameCount == MirrorMazeGimmick.LastClosedFrame)
            return;

        if (keyGiven)
        {
            Stage1Room.PlayCutscene(Stage1Dialogues.MirrorEmpty());
            return;
        }

        if (DangerLevel.WarnBeforeGimmick())
            return;
        DangerLevel.UiOpened();

        Unsubscribe();
        MirrorMazeGimmick.Solved += OnSolved;
        MirrorMazeGimmick.Failed += OnFailed;
        MirrorMazeGimmick.Closed += OnClosed;
        MirrorMazeGimmick.RewardObtained += OnRewardObtained;
        opened = true;
        // 우리 부스 열쇠(아이콘 = 사용자 그림)를 그대로 주도록 넘겨 줌
        MirrorMazeGimmick maze = MirrorMazeGimmick.Open(rewardItemAsset: boothKey);
        // [이지] 미로 작게, 부딪힘 6번까지 (Start에서 미로를 만들기 전에 바꿈)
        if (GameDifficulty.IsEasy && maze != null)
        {
            maze.mazeWidth = 7;
            maze.mazeHeight = 5;
            maze.maxBumps = 6;
        }
        if (maze != null)
            StartCoroutine(DecorateMazeUi(maze));
    }

    // ─── 거울 미로 화면 손보기 (사용자 요청 2026-11-03) ──────────────
    // - 조작 안내(WASD)·X 버튼이 안 보이던 문제는 미로 UI를 미로 카메라에 붙여 해결 (MirrorMazeGimmick.BuildUI)
    //   여기서는 안내 문구의 부딪힘 횟수를 실제 값(이지 6번)으로 맞춤
    // - 미로 중에도 하루 시간은 흐름 → 남은 시간을 미로 화면 오른쪽 위에 보여 줌
    private const int MazeCanvasOrder = 650;   // 하트 HUD(600)·행동 요령(610) 위, 키패드(700)·대화창 아래

    private IEnumerator DecorateMazeUi(MirrorMazeGimmick maze)
    {
        GameObject ui = null;
        while (maze != null && (ui = FindMazeUi(maze)) == null)
            yield return null;
        if (maze == null || ui == null)
            yield break;

        Canvas canvas = ui.GetComponent<Canvas>();
        if (canvas != null)
            canvas.sortingOrder = MazeCanvasOrder;

        Transform guide = ui.transform.Find("Guide");
        if (guide != null)
        {
            Text text = guide.GetComponent<Text>();
            if (text != null)
            {
                text.text = "손거울을 끌어 반대편을 비춰보자   ·   WASD / 방향키: 이동   ·   벽에 " + maze.maxBumps + "번 부딪히면 실패   ·   ESC: 나가기";
                text.color = Color.white;
                text.fontSize = 30;
            }
        }

        // 남은 시간
        GameObject timeGo = new GameObject("남은 시간", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Shadow));
        timeGo.transform.SetParent(ui.transform, false);
        RectTransform timeRect = (RectTransform)timeGo.transform;
        timeRect.anchorMin = timeRect.anchorMax = new Vector2(1f, 1f);
        timeRect.pivot = new Vector2(1f, 1f);
        timeRect.anchoredPosition = new Vector2(-40f, -130f);   // 닫기(X) 버튼 아래
        timeRect.sizeDelta = new Vector2(360f, 60f);
        Text timeText = timeGo.GetComponent<Text>();
        timeText.font = guide != null && guide.GetComponent<Text>() != null ? guide.GetComponent<Text>().font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        timeText.fontSize = 36;
        timeText.alignment = TextAnchor.MiddleRight;
        timeText.raycastTarget = false;
        timeGo.GetComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.85f);

        while (maze != null && timeGo != null)
        {
            DayCycleController cycle = DayCycleController.Instance;
            if (cycle != null)
            {
                int seconds = Mathf.CeilToInt(cycle.RemainingSeconds);
                timeText.text = "남은 시간 " + (seconds / 60) + ":" + (seconds % 60).ToString("00");
                timeText.color = seconds <= 30 ? new Color(1f, 0.45f, 0.45f, 1f) : Color.white;
            }
            yield return null;
        }
    }

    private static GameObject FindMazeUi(MirrorMazeGimmick maze)
    {
        Transform t = maze.transform.Find("MirrorMazeUI");
        return t != null ? t.gameObject : null;
    }

    private bool solvedCounted;

    private void OnSolved(string clue)
    {
        // 위험도: 미로 성공 +5 (한 판에 한 번)
        if (solvedCounted)
            return;
        solvedCounted = true;
        DangerLevel.GimmickSolved();
    }

    // 미로 안에서 열쇠를 주웠을 때 (퀵슬롯에는 MirrorMazeReward가 이미 넣음) → 미로를 닫으면 대사
    private void OnRewardObtained(string itemId)
    {
        if (keyGiven)
            return;
        keyGiven = true;
        StartCoroutine(SayKeyLinesAfterClose());
    }

    private IEnumerator SayKeyLinesAfterClose()
    {
        while (MirrorMazeGimmick.IsOpen)
            yield return null;
        yield return null;
        Stage1Room.PlayCutscene(Stage1Dialogues.MirrorKeyFound());
    }

    private static void ResetMirrorMazeRecord(string propertyName)
    {
        PropertyInfo property = typeof(MirrorMazeGimmick).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static);
        MethodInfo setter = property != null ? property.GetSetMethod(true) : null;
        if (setter != null)
            setter.Invoke(null, new object[] { false });
    }

    private void OnFailed()
    {
        DangerLevel.GimmickFailed();
        StartCoroutine(SayAfterClose());
    }

    private IEnumerator SayAfterClose()
    {
        while (MirrorMazeGimmick.IsOpen)
            yield return null;
        yield return null;
        Stage1Room.PlayCutscene(Stage1Dialogues.MirrorFailed());
    }

    private void OnClosed()
    {
        opened = false;
        Unsubscribe();
    }

    private void Unsubscribe()
    {
        MirrorMazeGimmick.Solved -= OnSolved;
        MirrorMazeGimmick.Failed -= OnFailed;
        MirrorMazeGimmick.Closed -= OnClosed;
        MirrorMazeGimmick.RewardObtained -= OnRewardObtained;
    }

    // 산책 쪽 부스 기믹은 itemId로 같은 열쇠인지 비교하므로, 같은 ID로 만들어 씀
    private static ItemData CreateBoothKey()
    {
        ItemData item = ScriptableObject.CreateInstance<ItemData>();
        item.name = Stage1Dialogues.BoothKeyItemId;
        SetField(item, "itemId", Stage1Dialogues.BoothKeyItemId);
        SetField(item, "displayName", Stage1Dialogues.BoothKeyItemName);
        SetField(item, "icon", LoadSprite(KeyIconPath));
        SetField(item, "consumeOnSuccessfulUse", true);
        return item;
    }

    private static void SetField(object target, string field, object value)
    {
        FieldInfo info = typeof(ItemData).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
        info?.SetValue(target, value);
    }

    private static Sprite LoadSprite(string path)
    {
        Texture2D texture = Resources.Load<Texture2D>(path);
        if (texture == null)
        {
            Debug.LogWarning("[MirrorBoothKey] 그림을 찾을 수 없습니다: Resources/" + path);
            return null;
        }

        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
    }
}
