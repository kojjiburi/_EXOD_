using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace EXOD.Walk
{
    /// <summary>
    /// 현관문. 클릭(상호작용)하면 산책 씬으로 이동한다.
    /// 산책에서 들킨 날(또는 이미 산책한 날)은 다음 날까지 열리지 않는다.
    ///
    /// 팀 프로젝트의 조사/대화 시스템에서 문을 열게 하려면 detectClickItself를 끄고
    /// 원하는 시점에 TryStartWalk()를 호출하면 된다.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class FrontDoorWalkStarter : MonoBehaviour
    {
        [SerializeField] string walkSceneName = "Stage2_Walk";
        [Tooltip("켜 두면 이 오브젝트를 클릭했을 때 직접 산책을 시작합니다.")]
        [SerializeField] bool detectClickItself = true;
        [SerializeField] Camera worldCamera;

        [Header("대사")]
        [SerializeField] string lockedMessage = "오늘은 더 이상 밖에 나갈 수 없다…";

        public UnityEvent onWalkStarting = new UnityEvent();
        public UnityEvent onDoorLocked = new UnityEvent();

        public bool IsLocked => DayClock.Instance != null && DayClock.Instance.FrontDoorLocked;

        bool starting;

        IEnumerator Start()
        {
            DayClock.Ensure();

            // 산책에서 막 돌아왔다면 화면을 밝히고 결과를 대화창으로 알려 준다
            if (WalkFade.IsDark) yield return WalkFade.FadeIn();
            if (WalkSession.LastResult != WalkResult.None)
            {
                string message = WalkGameManager.ResultLabel(WalkSession.LastResult);
                WalkSession.LastResult = WalkResult.None;
                if (!string.IsNullOrEmpty(message)) WalkDialogue.Show("", message);
            }
        }

        void Update()
        {
            if (!detectClickItself || starting || WalkDialogue.IsOpen) return;

            if (WalkInput.ClickDown(out var screenPos) && !WalkInput.IsPointerOverUI())
            {
                var hit = WalkInput.FindAtScreenPoint<FrontDoorWalkStarter>(screenPos, worldCamera);
                if (hit == this) TryStartWalk();
            }
        }

        /// <summary>산책 시작을 시도한다. 잠겨 있으면 false.</summary>
        public bool TryStartWalk()
        {
            if (starting) return false;
            var clock = DayClock.Ensure();
            if (clock.FrontDoorLocked)
            {
                WalkDialogue.Show("", lockedMessage);
                onDoorLocked.Invoke();
                return false;
            }

            if (!Application.CanStreamedLevelBeLoaded(walkSceneName))
            {
                Debug.LogWarning($"[Walk] 산책 씬 '{walkSceneName}'을(를) 불러올 수 없습니다. Build Profiles의 씬 목록에 추가했는지 확인하세요.");
                return false;
            }

            WalkSession.Additive = false;   // 테스트 집 씬은 산책 씬으로 통째로 넘어간다
            WalkSession.ReturnSceneName = gameObject.scene.name;
            WalkSession.LastResult = WalkResult.None;
            onWalkStarting.Invoke();
            StartCoroutine(LoadWalk());
            return true;
        }

        IEnumerator LoadWalk()
        {
            starting = true;
            yield return WalkFade.FadeOut();
            SceneManager.LoadScene(walkSceneName);
        }
    }
}
