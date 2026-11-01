using UnityEngine;

namespace EXOD.Walk
{
    /// <summary>
    /// 테스트용 침대: 클릭하면 바로 다음 날로 넘어간다 (기획서: 침대 상호작용 시 DAY +1).
    /// 팀 프로젝트에 이미 침대 기능이 있으면 이 컴포넌트는 쓰지 않아도 된다.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class BedNextDay : MonoBehaviour
    {
        [SerializeField] Camera worldCamera;

        void Update()
        {
            if (WalkDialogue.IsOpen) return;
            if (WalkInput.ClickDown(out var screenPos) && !WalkInput.IsPointerOverUI())
            {
                var hit = WalkInput.FindAtScreenPoint<BedNextDay>(screenPos, worldCamera);
                if (hit == this) DayClock.Ensure().AdvanceDay();
            }
        }
    }
}
