using UnityEngine;

namespace EXOD.Walk
{
    /// <summary>
    /// 산책 화면 위에 덮이는 창 (전화 부스 안 클로즈업 등).
    /// WalkGameManager.Overlay에 넣어 두면 열려 있는 동안
    ///  - 화면(카메라) 자동 이동이 멈추고
    ///  - 클릭과 Esc를 이 창이 먼저 처리한다.
    /// 산책 시간과 그녀가 뒤돌아보는 규칙은 그대로 흐른다.
    /// </summary>
    public interface IWalkOverlay
    {
        /// <summary>클릭을 처리했으면 true (true면 뒤에 있는 산책 화면 물체는 클릭되지 않는다)</summary>
        bool HandleClick(Vector2 screenPosition);

        /// <summary>Esc를 처리했으면 true (false면 원래대로 산책을 그만둔다)</summary>
        bool HandleEscape();
    }
}
