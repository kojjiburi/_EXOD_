using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace EXOD.Walk
{
    /// <summary>
    /// 스테이지2 단독 프로젝트(EXOD_newstage2) 전용.
    /// 1스테이지 아래쪽 아이템 칸(팀 프리팹 QuickSlotSystem: 퀵슬롯 바 이미지 + 10칸)을
    /// 같은 이미지·위치·크기로 똑같이 보여준다. 씬에 넣을 필요 없이 실행하면 자동으로 생긴다.
    ///
    /// 산책에서 얻은 아이템(WalkItemPickup)이 왼쪽 칸부터 차례로 들어간다.
    /// 통합 프로젝트에는 진짜 팀 퀵슬롯이 있으므로, 이 파일(Assets/TeamShared)은 합칠 때 옮겨지지 않는다.
    /// </summary>
    public class TeamQuickSlotPreview : MonoBehaviour
    {
        public static TeamQuickSlotPreview Instance { get; private set; }

        public const int SlotCount = 10;

        // 팀 프리팹(QuickSlotSystem)의 값 그대로 (1920×1080 기준)
        static readonly Vector2 BarAnchorMin = new Vector2(0.02f, 0f);
        static readonly Vector2 BarAnchorMax = new Vector2(0.98f, 0f);
        const float BarBottom = 24f;
        const float BarHeight = 205f;
        static readonly Vector2 SlotAreaMin = new Vector2(0.012f, 0.07f);
        static readonly Vector2 SlotAreaMax = new Vector2(0.988f, 0.93f);
        const float SlotPadding = 6f;
        const float SlotSpacing = 2f;
        const float IconInset = 0.12f;   // 칸 안쪽 여백 (팀 ItemIcon 앵커 0.12~0.88)

        /// <summary>칸에 들어 있는 아이템 (왼쪽부터)</summary>
        public static readonly List<ItemData> Items = new List<ItemData>();

        readonly Image[] icons = new Image[SlotCount];
        readonly Text[] labels = new Text[SlotCount];
        int shownVersion = -1;
        static int version;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            Items.Clear();
            version = 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void CreateOnStart()
        {
            if (Instance != null) return;
            var go = new GameObject("TeamQuickSlotPreview (1스테이지 아이템 칸)");
            DontDestroyOnLoad(go);
            go.AddComponent<TeamQuickSlotPreview>();
        }

        /// <summary>빈 칸에 아이템을 넣는다. 칸이 모두 차 있으면 false.</summary>
        public static bool TryAdd(ItemData item)
        {
            if (item == null) return false;
            if (Items.Count >= SlotCount) return false;
            Items.Add(item);
            version++;
            return true;
        }

        /// <summary>아이템을 칸에서 뺀다 (사용했을 때 등).</summary>
        public static bool Remove(ItemData item)
        {
            bool removed = Items.Remove(item);
            if (removed) version++;
            return removed;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // 단독 프로젝트에서는 산책에서 얻은 아이템을 이 칸에 넣는다.
            // (통합 프로젝트에서는 Stage2HouseLink가 팀 퀵슬롯으로 연결한다)
            if (WalkItemPickup.Giver == null)
                WalkItemPickup.Giver = TryAdd;

            Build();
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            if (WalkItemPickup.Giver == (System.Func<ItemData, bool>)TryAdd)
                WalkItemPickup.Giver = null;
        }

        void Build()
        {
            Canvas canvas = WalkUi.CreateCanvas("QuickSlotCanvas", 590, transform);   // 대화창(700)보다 아래

            Sprite barSprite = LoadSprite("QuickSlot", "QuickSlot_1");
            Image bar = WalkUi.CreateImage("QuickSlot_1", canvas.transform, barSprite, new Color(0.82f, 0.82f, 0.82f, 1f));
            RectTransform barRect = bar.rectTransform;
            barRect.anchorMin = BarAnchorMin;
            barRect.anchorMax = BarAnchorMax;
            barRect.pivot = new Vector2(0.5f, 0f);
            barRect.anchoredPosition = new Vector2(0f, BarBottom);
            barRect.sizeDelta = new Vector2(0f, BarHeight);

            RectTransform area = WalkUi.CreateRect("SlotContainer", barRect);
            area.anchorMin = SlotAreaMin;
            area.anchorMax = SlotAreaMax;
            area.pivot = new Vector2(0.5f, 0.5f);
            area.anchoredPosition = Vector2.zero;
            area.sizeDelta = Vector2.zero;

            var layout = area.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset((int)SlotPadding, (int)SlotPadding, (int)SlotPadding, (int)SlotPadding);
            layout.spacing = SlotSpacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            for (int i = 0; i < SlotCount; i++)
            {
                RectTransform slot = WalkUi.CreateRect($"Slot_{i + 1:00}", area);

                Image icon = WalkUi.CreateImage("ItemIcon", slot, null, Color.clear);
                RectTransform iconRect = icon.rectTransform;
                iconRect.anchorMin = new Vector2(IconInset, IconInset);
                iconRect.anchorMax = new Vector2(1f - IconInset, 1f - IconInset);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.anchoredPosition = Vector2.zero;
                iconRect.sizeDelta = Vector2.zero;
                icon.preserveAspect = true;
                icon.enabled = false;
                icons[i] = icon;

                // 아이콘이 아직 없는 아이템은 이름을 대신 보여준다
                Text label = WalkUi.CreateText("ItemName", slot, 26, WalkUi.DialogueTextColor, TextAnchor.MiddleCenter);
                WalkUi.Stretch(label.rectTransform, new Vector2(-16f, -16f));
                label.enabled = false;
                labels[i] = label;
            }
        }

        static Sprite LoadSprite(string file, string spriteName)
        {
            Sprite[] sprites = Resources.LoadAll<Sprite>("TeamUI/" + file);
            if (sprites == null || sprites.Length == 0)
            {
                Debug.LogWarning($"[TeamQuickSlotPreview] Resources/TeamUI/{file} 이미지를 찾지 못했습니다.");
                return null;
            }
            foreach (Sprite s in sprites)
                if (s.name == spriteName) return s;
            return sprites[0];
        }

        void LateUpdate()
        {
            if (shownVersion == version) return;
            shownVersion = version;

            for (int i = 0; i < SlotCount; i++)
            {
                ItemData item = i < Items.Count ? Items[i] : null;
                bool hasIcon = item != null && item.Icon != null;

                icons[i].sprite = hasIcon ? item.Icon : null;
                icons[i].color = Color.white;
                icons[i].enabled = hasIcon;

                labels[i].text = item != null && !hasIcon ? item.DisplayName : string.Empty;
                labels[i].enabled = item != null && !hasIcon;
            }
        }
    }
}
