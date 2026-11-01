using UnityEngine;
using UnityEngine.Events;

namespace EXOD.Walk
{
    /// <summary>
    /// 산책 화면에서 클릭할 수 있는 배경 물체 (화분, 우편함, 아이템 숨긴 곳 등).
    /// 그녀가 앞을 보고 있을 때 클릭하면 onInteract가 실행되고,
    /// 뒤돌아보고 있을 때 클릭하면 들켜서 산책이 끝난다.
    /// Collider2D가 있어야 클릭이 감지된다.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class WalkInteractable : MonoBehaviour
    {
        [SerializeField] string displayName = "";
        [Tooltip("한 번 상호작용하면 오브젝트를 끈다 (아이템 줍기 등)")]
        [SerializeField] bool disableAfterUse = false;

        [Tooltip("들키지 않고 상호작용했을 때 실행 (아이템 획득 등을 여기에 연결)")]
        public UnityEvent onInteract = new UnityEvent();
        [Tooltip("들킨 상태에서 상호작용했을 때 실행")]
        public UnityEvent onCaughtInteracting = new UnityEvent();

        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public int UseCount { get; private set; }

        internal void Perform()
        {
            UseCount++;
            Debug.Log($"[Walk] '{DisplayName}' 상호작용 (안 들킴)");
            onInteract.Invoke();
            if (disableAfterUse) gameObject.SetActive(false);
        }

        internal void PerformCaught()
        {
            Debug.Log($"[Walk] '{DisplayName}' 상호작용하다 들킴");
            onCaughtInteracting.Invoke();
        }
    }
}
