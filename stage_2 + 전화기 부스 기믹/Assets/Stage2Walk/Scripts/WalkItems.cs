using System;
using System.Collections.Generic;
using UnityEngine;

namespace EXOD.Walk
{
    /// <summary>
    /// 산책 기믹에서 퀵슬롯 아이템을 다루는 도우미 (고른 아이템 확인, 가지고 있는지, 사용해서 없애기, 넣어 주기).
    ///
    /// 실제 퀵슬롯과는 아래 연결 칸으로 이어진다.
    ///  - 스테이지2 단독 프로젝트: TeamQuickSlotPreview가 실행할 때 자동으로 연결한다.
    ///  - 통합 프로젝트: Stage2HouseLink에서 팀 퀵슬롯으로 연결한다 (README의 "전화 부스 기믹" 참고).
    /// 연결이 하나도 없으면 산책에서 얻은 아이템만 기억해 두고, "고른 아이템"은 "가지고 있는 아이템"으로 대신한다.
    /// </summary>
    public static class WalkItems
    {
        /// <summary>퀵슬롯에서 지금 골라 둔 아이템 (없으면 null)</summary>
        public static Func<ItemData> SelectedProvider;
        /// <summary>이 아이템을 퀵슬롯에 가지고 있는지</summary>
        public static Func<ItemData, bool> HasProvider;
        /// <summary>퀵슬롯에서 아이템을 뺀다 (뺐으면 true)</summary>
        public static Func<ItemData, bool> RemoveProvider;

        static readonly List<ItemData> fallbackOwned = new List<ItemData>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            SelectedProvider = null;
            HasProvider = null;
            RemoveProvider = null;
            fallbackOwned.Clear();
        }

        /// <summary>같은 아이템인지 (itemId가 있으면 itemId로, 없으면 에셋으로 비교)</summary>
        public static bool Same(ItemData a, ItemData b)
        {
            if (a == null || b == null) return false;
            if (a == b) return true;
            return !string.IsNullOrEmpty(a.ItemId) && a.ItemId == b.ItemId;
        }

        public static ItemData Selected => SelectedProvider != null ? SelectedProvider() : null;

        public static bool Has(ItemData item)
        {
            if (item == null) return false;
            if (HasProvider != null) return HasProvider(item);
            foreach (var owned in fallbackOwned)
                if (Same(owned, item)) return true;
            return false;
        }

        /// <summary>
        /// 이 아이템을 "골라서 쓰는 중"인지.
        /// 고르기 기능이 연결돼 있으면 고른 아이템이 같은지, 없으면 가지고 있는지로 판단한다.
        /// </summary>
        public static bool IsUsing(ItemData item)
        {
            if (item == null) return false;
            if (SelectedProvider != null) return Same(SelectedProvider(), item);
            return Has(item);
        }

        /// <summary>아이템을 퀵슬롯에 넣는다. 자리가 없으면 false.</summary>
        public static bool Give(ItemData item)
        {
            if (item == null) return false;
            bool given = WalkItemPickup.Giver == null || WalkItemPickup.Giver(item);
            if (!given) return false;
            if (HasProvider == null && !Has(item)) fallbackOwned.Add(item);
            if (!WalkItemPickup.PickedNames.Contains(item.DisplayName)) WalkItemPickup.PickedNames.Add(item.DisplayName);
            return true;
        }

        /// <summary>아이템을 사용했다. ItemData의 "사용하면 없어짐"이 켜져 있으면 퀵슬롯에서 뺀다.</summary>
        public static void UseUp(ItemData item)
        {
            if (item == null || !item.ConsumeOnSuccessfulUse) return;
            if (RemoveProvider != null) RemoveProvider(item);
            fallbackOwned.RemoveAll(o => Same(o, item));
        }

        /// <summary>WalkItemPickup으로 얻은 아이템도 기억해 둔다 (연결이 없을 때만 쓰임)</summary>
        internal static void NotePicked(ItemData item)
        {
            if (item == null || HasProvider != null || Has(item)) return;
            fallbackOwned.Add(item);
        }
    }
}
