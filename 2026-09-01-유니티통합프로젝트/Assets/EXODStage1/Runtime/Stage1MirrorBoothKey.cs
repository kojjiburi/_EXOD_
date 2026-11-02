using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 화장실 선반의 손거울 → 거울 미로(MirrorMazeGimmick, 친구 main 기믹) → 전화 부스 열쇠 (2026-11-02 사용자 결정)
/// - 화장실 선반 오른쪽 칸(금고 왼쪽)에 분홍 손거울 그림을 세워 두고, 누르면 거울 미로가 열립니다.
/// - 출구에 도착하면 "거울 뒷면에 작은 열쇠가… 'TELEPHONE'" → 부스 열쇠(stage2_booth_key)가 퀵슬롯에 들어옵니다.
///   (일차 끝 점검 예외 아이템: ItemPositionManager.AlwaysAllowedInInventoryItemIds)
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
        opened = true;
        MirrorMazeGimmick.Open();
    }

    private void OnSolved(string clue)
    {
        if (keyGiven)
            return;

        // 기믹 창이 닫힌 뒤 대사와 함께 열쇠를 줌
        StartCoroutine(GiveKeyAfterClose());
    }

    private IEnumerator GiveKeyAfterClose()
    {
        keyGiven = true;
        DangerLevel.GimmickSolved();
        EXODSound.Play("key_unlock");

        while (MirrorMazeGimmick.IsOpen)
            yield return null;
        yield return null;

        Stage1Room.PlayCutscene(Stage1Dialogues.MirrorKeyFound(), GiveKey);
    }

    private void GiveKey()
    {
        QuickSlotInventory inventory = QuickSlotInventory.Instance;
        if (inventory == null || boothKey == null)
            return;

        bool alreadyHas = inventory.Entries.Any(e => e != null && e.ItemData != null && e.ItemData.ItemId == Stage1Dialogues.BoothKeyItemId);
        if (alreadyHas)
            return;

        if (inventory.TryAdd(boothKey))
        {
            EXODSound.Play("item_pickup");
        }
        else
        {
            // 자리가 없으면 다음에 손거울을 다시 눌러 받을 수 있게
            keyGiven = false;
            DialogueManager.Instance?.StartDialogue(Stage1Dialogues.Thought("퀵슬롯이 가득 차서 열쇠를 챙길 수 없어. 자리를 비우고 다시 보자."));
        }
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
