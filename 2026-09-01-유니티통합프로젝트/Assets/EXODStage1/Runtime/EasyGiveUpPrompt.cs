using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// [이지] 하트가 1 깎일 때마다 "여기까지 하시겠습니까?" [예]/[아니요] (사용자 요청 2026-11-03)
/// - [예] → 간호 엔딩 (하트 0일 때와 같은 연출, 2026-11-04 순응 엔딩에서 바꿈)
/// - [아니요] → 그대로 계속
/// - 하트가 0이 되면 묻지 않음 (그대로 게임 오버)
/// - 들킴·점검 실패·산책 들킴·기절 등 어떤 이유로 깎여도, 그 연출(대사·컷신·일차 넘김·산책)이 다 끝난 뒤에 물음
/// 하드 모드에서는 아무것도 하지 않습니다. 실행할 때 SliderManager가 있는 씬에 자동으로 붙습니다.
/// </summary>
public sealed class EasyGiveUpPrompt : MonoBehaviour
{
    private HeartManager hearts;
    private int lastHp = -1;
    private bool pending;
    private bool asking;

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
        if (slider != null && slider.GetComponent<EasyGiveUpPrompt>() == null)
            slider.gameObject.AddComponent<EasyGiveUpPrompt>();
    }

    private void Update()
    {
        if (hearts == null)
        {
            hearts = FindFirstObjectByType<HeartManager>(FindObjectsInactive.Include);
            if (hearts == null)
                return;
            lastHp = hearts.HP;
        }

        int hp = hearts.HP;
        // 현관에서 기절해 깎인 하트는 묻지 않음 (현관에서는 이 문답을 빼기로, 사용자 요청 2026-11-04)
        bool fromFrontDoorFaint = PlayerFaintController.Instance != null && PlayerFaintController.Instance.IsFainting;
        if (hp < lastHp && hp > 0 && GameDifficulty.IsEasy && !fromFrontDoorFaint)
            pending = true;
        if (hp <= 0)
            pending = false;   // 하트 0이면 게임 오버로
        lastHp = hp;

        // 다른 연출이 다 끝나고 0.5초 동안 조용할 때 물음 (산책에서 돌아온 직후 '산책 결과' 대사 등에 선택지가 덮이지 않게, 2026-11-06)
        if (pending && !asking && CanAskNow())
        {
            quietTimer += Time.unscaledDeltaTime;
            if (quietTimer >= QuietSecondsBeforeAsk)
            {
                quietTimer = 0f;
                Ask();
            }
        }
        else
            quietTimer = 0f;
    }

    private const float QuietSecondsBeforeAsk = 0.5f;
    private float quietTimer;

    // 하트가 깎인 연출이 모두 끝난 뒤에만
    private static bool CanAskNow()
    {
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueOpen)
            return false;
        if (Stage1StoryController.Instance != null && Stage1StoryController.Instance.IsPlaying)
            return false;
        if (PlayerFaintController.Instance != null && PlayerFaintController.Instance.IsBusy)
            return false;
        if (DayController.IsTransitioning || Stage2HouseLink.IsInStage2 || Stage2HouseLink.IsTransitioning
            || DangerLevel.IsAppearing || ScreenAberration.IsPlaying)
            return false;
        return true;
    }

    private void Ask()
    {
        pending = false;
        asking = true;
        GimmickInterrupt.CloseAll();
        Stage1Room.PlayChoiceCutscene(Stage1Dialogues.EasyGiveUpQuestion(), Stage1Dialogues.YesNoChoices, choice =>
        {
            asking = false;
            if (choice != 0)
            {
                BoothMode.GiveUpDeclined = true;   // 부스 모드 '이미 볼 수 있는 엔딩'(순응) 조건
                // [아니요] → 계속. 선택지 컷신은 끝나도 방이 잠긴 채라 짧은 대사로 이어서 풀어 줌 (방 이동 먹통 수정, 2026-11-04)
                Stage1Room.PlayCutscene(Stage1Dialogues.EasyGiveUpDeclined());
                return;
            }

            // [예] → 하트가 깎여서 뜬 질문이므로 간호 엔딩 (하트 0과 같은 연출, 사용자 요청 2026-11-04 / 예전엔 순응 엔딩)
            if (PlayerFaintController.Instance != null)
                PlayerFaintController.Instance.ShowNursingEnding();
        });
    }
}
