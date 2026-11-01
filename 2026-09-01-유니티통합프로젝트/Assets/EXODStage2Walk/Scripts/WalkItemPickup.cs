using System;
using System.Collections.Generic;
using UnityEngine;

namespace EXOD.Walk
{
    /// <summary>
    /// 산책 중 숨겨진 아이템. WalkInteractable과 같은 오브젝트에 붙인다.
    /// 그녀가 앞을 보고 있을 때 클릭하면 아이템을 얻고, 뒤돌아보고 있을 때 클릭하면 들킨다(WalkInteractable 규칙 그대로).
    ///
    /// item 칸에는 팀 공용 ItemData 에셋을 넣는다.
    ///  - 스테이지2 단독 프로젝트: 퀵슬롯이 없으므로 얻은 아이템을 화면 왼쪽 위 디버그 표시로만 보여준다.
    ///  - 통합 프로젝트: Stage2HouseLink가 Giver를 연결해 두므로 팀 퀵슬롯에 바로 들어간다.
    /// 한 번 얻은 아이템은 다음 날 산책에서도 다시 나오지 않는다.
    /// </summary>
    [RequireComponent(typeof(WalkInteractable))]
    public class WalkItemPickup : MonoBehaviour
    {
        [Tooltip("얻을 아이템 (팀 공용 ItemData 에셋)")]
        [SerializeField] ItemData item;
        [Tooltip("얻은 뒤 이 오브젝트를 숨긴다")]
        [SerializeField] bool hideAfterPickup = true;

        /// <summary>
        /// 아이템을 실제로 넣어 주는 쪽 (통합 프로젝트의 퀵슬롯). 넣었으면 true, 자리가 없으면 false.
        /// 비어 있으면 얻은 것으로만 기록한다.
        /// </summary>
        public static Func<ItemData, bool> Giver;

        /// <summary>얻은 아이템 이름 (디버그 표시용)</summary>
        public static readonly List<string> PickedNames = new List<string>();

        static readonly HashSet<string> pickedKeys = new HashSet<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Giver = null;
            PickedNames.Clear();
            pickedKeys.Clear();
        }

        public ItemData Item => item;
        string Key => item != null && !string.IsNullOrEmpty(item.ItemId) ? item.ItemId : gameObject.name;

        void Awake()
        {
            if (pickedKeys.Contains(Key))
            {
                gameObject.SetActive(false);
                return;
            }
            GetComponent<WalkInteractable>().onInteract.AddListener(Pickup);
        }

        void Pickup()
        {
            if (pickedKeys.Contains(Key)) return;

            if (item == null)
            {
                Debug.LogWarning($"[Walk] '{name}'의 WalkItemPickup에 아이템이 비어 있습니다.", this);
                return;
            }

            bool given = Giver == null || Giver(item);
            if (!given)
            {
                Debug.Log($"[Walk] 퀵슬롯에 자리가 없어 '{item.DisplayName}'을(를) 줍지 못했습니다.");
                return;
            }

            pickedKeys.Add(Key);
            PickedNames.Add(item.DisplayName);
            Debug.Log($"[Walk] 아이템 획득: {item.DisplayName} ({item.ItemId})");
            if (hideAfterPickup) gameObject.SetActive(false);
        }
    }
}
