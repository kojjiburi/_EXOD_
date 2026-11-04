using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 지금 엔딩이 진행 중인지 (엔딩 컷신 → 엔딩 화면까지). 엔딩 중이면 IsEnding = true (사용자 요청 2026-11-04)
/// - 엔딩 컷신이 시작될 때 Begin()을 부름 (청혼·순응(물약/침대)·탈출 컷신)
/// - 엔딩 화면·간호 엔딩·운명의 갈림길·시간 초과(PlayerFaintController 게임 오버 상태)와 강제 엔딩(BoothMode.ForcingEnding)도 엔딩으로 침
/// - 씬을 다시 불러오면(다시 시작·타이틀) false로
/// 쓰는 곳: 부스 운영 모드 제한시간 멈춤, 위험도 급상승 첫 대사 안 띄움, 위쪽 [종료] 버튼 숨김
/// </summary>
public static class EndingState
{
    private static bool started;

    public static bool IsEnding =>
        started
        || BoothMode.ForcingEnding
        || (PlayerFaintController.Instance != null && PlayerFaintController.Instance.IsEndingShown)
        || EXOD.Walk.PhoneBoothEnding.IsShowing;

    public static void Begin() => started = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        started = false;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single)
            return;   // 산책 씬(추가 로드)은 제외
        started = false;
        DialoguePortrait.ExtraWalkCaughtVignette = false;   // 아른거림 비네트가 남지 않게
    }
}
