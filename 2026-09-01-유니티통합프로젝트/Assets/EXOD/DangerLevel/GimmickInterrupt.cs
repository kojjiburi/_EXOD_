using UnityEngine;

/// <summary>
/// 여자친구 등장·일차 끝 안내처럼 게임이 먼저 대사를 띄울 때, 열려 있던 기믹 창·조사 화면을 닫습니다. (사용자 요청 2026-11-03)
/// - 기믹 창: 현관/냉장고 키패드, 서랍 비밀번호, 화장실 금고, 거울 미로
/// - 조사 화면(물건 확대·설명), 메모 확대, 손전등 비춘 화면
/// 이렇게 닫힌 기믹은 실패로 치지 않습니다 (Stage1AntiIdle이 JustClosed를 보고 건너뜀).
/// </summary>
public static class GimmickInterrupt
{
    private const float GraceSeconds = 0.5f;
    private static float closedAt = -999f;

    /// <summary>방금 이 코드가 창을 닫았는지 (잠수 방지의 '그냥 닫으면 실패'를 건너뛰는 데 씀)</summary>
    public static bool JustClosed => Time.unscaledTime - closedAt <= GraceSeconds;

    public static void CloseAll()
    {
        closedAt = Time.unscaledTime;

        if (MirrorMazeGimmick.Current != null)
            MirrorMazeGimmick.Current.Close();

        if (Stage1Keypad.IsOpen)
            Stage1Keypad.CloseActive();

        if (RestroomGimmick.Instance != null && RestroomGimmick.Instance.IsKeypadOpen)
            RestroomGimmick.Instance.CloseKeypad();

        PasswordDrawer drawer = Object.FindFirstObjectByType<PasswordDrawer>(FindObjectsInactive.Include);
        if (drawer != null && drawer.IsPanelOpen)
            drawer.Close();

        if (InspectionManager.Instance != null && InspectionManager.Instance.IsOpen)
            InspectionManager.Instance.Close();

        if (MemoZoom.Instance != null && MemoZoom.Instance.IsOpen)
            MemoZoom.Instance.Close();

        if (FlashlightGimmick.Instance != null)
        {
            if (FlashlightGimmick.Instance.IsViewerOpen)
                FlashlightGimmick.Instance.CloseViewer();
            FlashlightGimmick.Instance.CancelTargetSelection();
        }
    }
}
