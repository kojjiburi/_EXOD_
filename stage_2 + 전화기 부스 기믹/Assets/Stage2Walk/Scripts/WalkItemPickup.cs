using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

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
        [Tooltip("얻었을 때 나올 대사 (비워 두면 대사 없이 얻는다)")]
        [SerializeField] WalkLine[] pickupLines = new WalkLine[0];
        [Tooltip("아이템을 얻었을 때 실행")]
        public UnityEvent onPickedUp = new UnityEvent();

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

        /// <summary>이 아이템을 이미 얻었는지</summary>
        public static bool HasPicked(ItemData item)
        {
            return item != null && pickedKeys.Contains(string.IsNullOrEmpty(item.ItemId) ? item.name : item.ItemId);
        }

        /// <summary>
        /// 얻은 기록을 지워서 다음 산책에서 다시 나오게 한다 (전화 부스: 통화 중에 들키면 테이프가 한 번 더 생김).
        /// 지금 열려 있는 산책 화면에는 바로 나타나지 않고, 다음에 산책 씬이 열릴 때 나온다.
        /// </summary>
        public static void Forget(ItemData item)
        {
            if (item == null) return;
            pickedKeys.Remove(string.IsNullOrEmpty(item.ItemId) ? item.name : item.ItemId);
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
            WalkItems.NotePicked(item);
            Debug.Log($"[Walk] 아이템 획득: {item.DisplayName} ({item.ItemId})");
            onPickedUp.Invoke();
            if (pickupLines != null && pickupLines.Length > 0) WalkDialogue.Show(pickupLines);
            if (hideAfterPickup) gameObject.SetActive(false);
        }
    }
}
