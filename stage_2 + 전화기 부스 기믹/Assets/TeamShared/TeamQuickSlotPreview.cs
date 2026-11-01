using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace EXOD.Walk
{
    /// <summary>
    /// 스테이지2 단독 프로젝트(EXOD_newstage2) 전용.
    /// 1스테이지 아래쪽 아이템 칸(팀 프리팹 QuickSlotSystem: 퀵슬롯 바 이미지 + 10칸)을
    /// 같은 이미지·위치·크기로 똑같이 보여준다. 씬에 넣을 필요 없이 실행하면 자동으로 생긴다.
    ///
    /// 산책에서 얻은 아이템(WalkItemPickup)이 왼쪽 칸부터 차례로 들어간다.
    /// 통합 프로젝트에는 진짜 팀 퀵슬롯이 있으므로, 이 파일(Assets/TeamShared)은 합칠 때 옮겨지지 않는다.
    ///
    /// 아이템 고르기 (전화 부스 기믹용): 칸을 클릭하거나 숫자키 1~0을 누르면 그 칸의 아이템을 고른다.
    /// 한 번 더 누르면 고르기가 풀린다. 고른 아이템은 WalkItems.Selected로 읽는다.
    /// 아이템 칸 위를 클릭하는 것은 산책 화면 클릭으로 치지 않는다 (그녀가 뒤돌아봐도 들키지 않음).
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

        /// <summary>지금 골라 둔 아이템 (없으면 null)</summary>
        public static ItemData Selected { get; private set; }

        readonly Image[] icons = new Image[SlotCount];
        readonly Image[] frames = new Image[SlotCount];
        readonly RectTransform[] slotRects = new RectTransform[SlotCount];
        RectTransform barRectRef;
        readonly Text[] labels = new Text[SlotCount];
        int shownVersion = -1;
        static int version;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            Items.Clear();
            Selected = null;
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

        /// <summary>이 아이템(같은 itemId 포함)을 가지고 있는지</summary>
        public static bool Has(ItemData item)
        {
            return IndexOf(item) >= 0;
        }

        static int IndexOf(ItemData item)
        {
            for (int i = 0; i < Items.Count; i++)
                if (WalkItems.Same(Items[i], item)) return i;
            return -1;
        }

        /// <summary>아이템을 고른다 (null이면 고르기 해제)</summary>
        public static void Select(ItemData item)
        {
            Selected = item;
            version++;
        }

        /// <summary>아이템을 칸에서 뺀다 (사용했을 때 등).</summary>
        public static bool Remove(ItemData item)
        {
            int index = IndexOf(item);
            if (index < 0) return false;
            if (WalkItems.Same(Selected, Items[index])) Selected = null;
            Items.RemoveAt(index);
            version++;
            return true;
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

            // 기믹(전화 부스 등)이 "고른 아이템"을 읽을 수 있게 연결한다
            if (WalkItems.SelectedProvider == null) WalkItems.SelectedProvider = GetSelected;
            if (WalkItems.HasProvider == null) WalkItems.HasProvider = Has;
            if (WalkItems.RemoveProvider == null) WalkItems.RemoveProvider = Remove;
            if (WalkInput.ExtraPointerOverUi == null) WalkInput.ExtraPointerOverUi = PointerOverBar;

            Build();
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            if (WalkItemPickup.Giver == (System.Func<ItemData, bool>)TryAdd)
                WalkItemPickup.Giver = null;
            if (WalkItems.SelectedProvider == (System.Func<ItemData>)GetSelected) WalkItems.SelectedProvider = null;
            if (WalkItems.HasProvider == (System.Func<ItemData, bool>)Has) WalkItems.HasProvider = null;
            if (WalkItems.RemoveProvider == (System.Func<ItemData, bool>)Remove) WalkItems.RemoveProvider = null;
            if (WalkInput.ExtraPointerOverUi == (System.Func<bool>)PointerOverBar) WalkInput.ExtraPointerOverUi = null;
        }

        static ItemData GetSelected() => Selected;

        bool PointerOverBar()
        {
            return barRectRef != null && barRectRef.gameObject.activeInHierarchy &&
                   RectTransformUtility.RectangleContainsScreenPoint(barRectRef, WalkInput.PointerScreenPosition(), null);
        }

        void Update()
        {
            if (WalkDialogue.IsOpen || WalkFade.IsDark) return;

            int pressed = NumberKeyDown();
            if (pressed < 0 && WalkInput.ClickDown(out Vector2 pos))
            {
                for (int i = 0; i < SlotCount; i++)
                {
                    if (slotRects[i] != null && RectTransformUtility.RectangleContainsScreenPoint(slotRects[i], pos, null))
                    {
                        pressed = i;
                        break;
                    }
                }
            }
            if (pressed < 0) return;

            ItemData item = pressed < Items.Count ? Items[pressed] : null;
            Select(item == null || WalkItems.Same(item, Selected) ? null : item);
        }

        static int NumberKeyDown()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return -1;
            if (kb.digit1Key.wasPressedThisFrame) return 0;
            if (kb.digit2Key.wasPressedThisFrame) return 1;
            if (kb.digit3Key.wasPressedThisFrame) return 2;
            if (kb.digit4Key.wasPressedThisFrame) return 3;
            if (kb.digit5Key.wasPressedThisFrame) return 4;
            if (kb.digit6Key.wasPressedThisFrame) return 5;
            if (kb.digit7Key.wasPressedThisFrame) return 6;
            if (kb.digit8Key.wasPressedThisFrame) return 7;
            if (kb.digit9Key.wasPressedThisFrame) return 8;
            if (kb.digit0Key.wasPressedThisFrame) return 9;
#else
            for (int i = 0; i < 9; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i)) return i;
            if (Input.GetKeyDown(KeyCode.Alpha0)) return 9;
#endif
            return -1;
        }

        void Build()
        {
            Canvas canvas = WalkUi.CreateCanvas("QuickSlotCanvas", 590, transform);   // 대화창(700)보다 아래

            Sprite barSprite = LoadSprite("QuickSlot", "QuickSlot_1");
            Image bar = WalkUi.CreateImage("QuickSlot_1", canvas.transform, barSprite, new Color(0.82f, 0.82f, 0.82f, 1f));
            RectTransform barRect = bar.rectTransform;
            barRectRef = barRect;
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
                slotRects[i] = slot;

                // 고른 칸 표시 (노란 테두리)
                Image frame = WalkUi.CreateImage("Selected", slot, null, new Color(1f, 0.85f, 0.2f, 0.35f));
                WalkUi.Stretch(frame.rectTransform, new Vector2(-4f, -4f));
                var outline = frame.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(1f, 0.85f, 0.2f, 1f);
                outline.effectDistance = new Vector2(4f, -4f);
                frame.enabled = false;
                frames[i] = frame;

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
                frames[i].enabled = item != null && WalkItems.Same(item, Selected);
            }
        }
    }
}
