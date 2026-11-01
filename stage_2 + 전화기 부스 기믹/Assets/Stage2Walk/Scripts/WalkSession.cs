using System;

namespace EXOD.Walk
{
    public enum WalkResult
    {
        None,
        Completed,  // 두 번 왕복 완료
        TimeUp,     // 1분이 다 지남
        QuitEarly,  // Esc로 중간에 그만둠
        Caught,     // 상호작용하다 들킴
        DayEnded    // 산책 중 그날의 시간이 끝남
    }

    /// <summary>집(1스테이지 씬)과 산책 씬 사이에 넘기는 정보</summary>
    public static class WalkSession
    {
        /// <summary>
        /// true면 산책 씬이 집 씬 위에 추가로(Additive) 열린 상태다.
        /// 이때는 산책이 끝나도 집 씬을 다시 불러오지 않고 Finished만 알린다
        /// (1스테이지 진행 상황이 초기화되지 않게 하기 위해서).
        /// </summary>
        public static bool Additive;

        /// <summary>Additive가 아닐 때 산책이 끝나면 돌아갈 씬</summary>
        public static string ReturnSceneName;

        /// <summary>마지막 산책 결과</summary>
        public static WalkResult LastResult = WalkResult.None;

        /// <summary>산책이 끝나 집으로 돌아갈 때 (Additive 모드)</summary>
        public static event Action<WalkResult> Finished;

        internal static void RaiseFinished(WalkResult result)
        {
            Finished?.Invoke(result);
        }
    }
}
