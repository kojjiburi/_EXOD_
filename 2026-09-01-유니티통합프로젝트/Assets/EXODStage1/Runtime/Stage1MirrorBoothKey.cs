using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 화장실 선반의 손거울 → 거울 미로(MirrorMazeGimmick, 친구 main 기믹) → 전화 부스 열쇠 (2026-11-02 사용자 결정)
/// - 화장실 선반 오른쪽 칸(금고 왼쪽)에 분홍 손거울 그림을 세워 두고, 누르면 거울 미로가 열립니다.
/// - 출구에 도착하면 미로 화면 안 선반 위로 전화부스 열쇠가 떨어지고, 클릭해서 주우면 퀵슬롯에 들어옵니다.
///   (main "미로기믹 수정본" 방식, MirrorMazeGimmick/MirrorMazeReward — 2026-11-02 사용자 결정으로 이 방식만 씀)
///   열쇠 ItemData는 여기서 만든 것(stage2_booth_key, BoothKeyIcon)을 넘겨 줍니다.
///   (일차 끝 점검 예외 아이템: ItemPositionManager.AlwaysAllowedInInventoryItemIds)
/// - 벽에 3번 부딪히면 닫히고 다시 도전할 수 있습니다.
/// - 친구 위험도 규칙: 열기 전 WarnBeforeGimmick·UiOpened, 성공 GimmickSolved(+5), 실패 GimmickFailed(+10)
/// 대사: Stage1Dialogues.MirrorFailed / MirrorEmpty (MirrorKeyFound는 지금 안 씀)
/// 실행할 때 SliderManager가 있는 씬에 자동으로 붙습니다.
/// </summary>
[DefaultExecutionOrder(230)]
public sealed class Stage1MirrorBoothKey : MonoBehaviour
{
    // 화장실 그림(1920x1080, 위쪽이 y=0) 기준 손거울 자리: 선반 오른쪽 칸, 금고(1318~) 왼쪽
    private static readonly RectInt MirrorRect = new RectInt(1110, 38, 120, 150);
    private const string MirrorImagePath = "EXODStage1/HandMirror";
    private const string KeyIconPath = "EXODStage1/BoothKeyIcon";

    private bool solvedCounted;   // 위험도 '기믹 성공'은 한 번만
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

        // 새 판이면 지난 판의 '미로 깸 / 열쇠 주움' 기록을 지움 (정적 값이라 다시 시작해도 남아 있음)
        SetStaticProperty(nameof(MirrorMazeGimmick.IsSolved), false);
        SetStaticProperty(nameof(MirrorMazeGimmick.RewardTaken), false);
    }

    private static void SetStaticProperty(string name, bool value)
    {
        PropertyInfo property = typeof(MirrorMazeGimmick).GetProperty(name, BindingFlags.Public | BindingFlags.Static);
        MethodInfo setter = property != null ? property.GetSetMethod(true) : null;
        setter?.Invoke(null, new object[] { value });
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void OnMirrorClicked()
    {
        if (Stage1Room.IsBlocked() || MirrorMazeGimmick.IsOpen || Time.frameCount == MirrorMazeGimmick.LastClosedFrame)
            return;

        if (MirrorMazeGimmick.RewardTaken)
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
        opened = true;
        // 미로를 깼는데 퀵슬롯이 꽉 차서 못 주웠으면, 다시 열 때 선반 위 열쇠를 바로 주울 수 있음
        MirrorMazeGimmick.Open(rewardItemAsset: boothKey);
    }

    // 열쇠는 미로 화면 안에서 직접 주움 (MirrorMazeGimmick.TakeKey). 여기서는 위험도 '기믹 성공'만
    private void OnSolved(string clue)
    {
        if (solvedCounted)
            return;

        solvedCounted = true;
        DangerLevel.GimmickSolved();
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
