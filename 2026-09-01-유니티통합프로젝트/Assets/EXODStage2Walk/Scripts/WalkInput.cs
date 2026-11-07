using System;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace EXOD.Walk
{
    /// <summary>
    /// 새 Input System / 기존 Input Manager 어느 쪽 설정이어도 동작하도록 입력을 모아 둔 도우미.
    /// </summary>
    public static class WalkInput
    {
        /// <summary>
        /// EventSystem 없이 직접 그린 화면 요소(단독 프로젝트의 퀵슬롯 미리보기 등) 위에 마우스가 있는지 알려 주는 쪽.
        /// true를 돌려주면 그 클릭은 산책 화면 클릭으로 치지 않는다.
        /// </summary>
        public static Func<bool> ExtraPointerOverUi;

        /// <summary>켜 두면 산책 화면 클릭·Esc·좌우 이동 입력을 모두 무시 (부스 운영 모드 강제 탈출 엔딩 연출용, 2026-11-03)</summary>
        public static bool BlockAll;

        // [터치] 마지막으로 누른 곳이 손가락이었는지 (IsPointerOverUI가 그 자리로 UI를 확인)
        static bool lastPressWasTouch;
        static Vector2 lastPressPosition;
        static int lastPressFrame = -1;
        // [터치] 화면의 [그만두기] 버튼을 누른 프레임 (Esc 대신)
        static int quitRequestedFrame = -10;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { ExtraPointerOverUi = null; BlockAll = false; lastPressWasTouch = false; lastPressFrame = -1; quitRequestedFrame = -10; }

        /// <summary>[터치] 화면의 [그만두기] 버튼에서 부른다. 다음 QuitWalkDown()이 true가 된다.</summary>
        public static void RequestQuitWalk() { quitRequestedFrame = Time.frameCount; }

#if ENABLE_INPUT_SYSTEM
        /// <summary>[터치] 이번 프레임에 화면을 새로 누른 손가락이 있으면 그 위치</summary>
        static bool TouchPressedThisFrame(out Vector2 screenPosition)
        {
            var ts = Touchscreen.current;
            if (ts != null)
            {
                foreach (var t in ts.touches)
                {
                    if (!t.press.wasPressedThisFrame) continue;
                    screenPosition = t.position.ReadValue();
                    return true;
                }
            }
            screenPosition = default;
            return false;
        }
#endif

        /// <summary>A/D 또는 ←/→ : -1 ~ 1</summary>
        public static float Horizontal()
        {
            if (BlockAll) return 0f;
            float h = 0f;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return 0f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) h -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) h += 1f;
#else
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) h -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) h += 1f;
#endif
            return h;
        }

        /// <summary>
        /// 이번 프레임에 산책 화면(물체·문 등)을 클릭했는지.
        /// 대화창이 떠 있거나 이번 프레임에 대화창을 클릭해서 닫았으면 false (대사를 넘기는 클릭이 물체 클릭으로 새지 않게).
        /// </summary>
        public static bool ClickDown(out Vector2 screenPosition)
        {
            if (WalkDialogue.BlocksWorldClick)
            {
                screenPosition = default;
                return false;
            }
            return ClickDownAny(out screenPosition);
        }

        /// <summary>이번 프레임에 마우스 왼쪽 버튼을 눌렀는지 (대화창 상태와 상관없이)</summary>
        public static bool ClickDownAny(out Vector2 screenPosition)
        {
            if (BlockAll)
            {
                screenPosition = default;
                return false;
            }
#if ENABLE_INPUT_SYSTEM
            if (TouchPressedThisFrame(out screenPosition))
            {
                lastPressWasTouch = true; lastPressPosition = screenPosition; lastPressFrame = Time.frameCount;
                return true;
            }
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                screenPosition = mouse.position.ReadValue();
                lastPressWasTouch = false; lastPressFrame = Time.frameCount;
                return true;
            }
#else
            if (Input.GetMouseButtonDown(0))
            {
                screenPosition = Input.mousePosition;
                return true;
            }
#endif
            screenPosition = default;
            return false;
        }

        /// <summary>현재 마우스 화면 좌표</summary>
        public static Vector2 PointerScreenPosition()
        {
#if ENABLE_INPUT_SYSTEM
            var ts = Touchscreen.current;
            if (ts != null && ts.primaryTouch.press.isPressed) return ts.primaryTouch.position.ReadValue();
            if (lastPressWasTouch) return new Vector2(-10000f, -10000f);   // 손가락을 뗐으면 마우스 올림 표시는 안 함
            var mouse = Mouse.current;
            return mouse != null ? mouse.position.ReadValue() : Vector2.zero;
#else
            return Input.mousePosition;
#endif
        }

        /// <summary>산책 중간에 그만두기 (Esc)</summary>
        public static bool QuitWalkDown()
        {
            if (BlockAll) return false;
            if (Time.frameCount - quitRequestedFrame <= 1) { quitRequestedFrame = -10; return true; }   // [터치] [그만두기] 버튼
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            return kb != null && kb.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        /// <summary>대화 넘기기 (Space, 팀 대화창과 같은 키)</summary>
        public static bool AdvanceDialogueDown()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            return kb != null && kb.spaceKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Space);
#endif
        }

        /// <summary>마우스가 UI(퀵슬롯 등) 위에 있는지</summary>
        public static bool IsPointerOverUI()
        {
            if (ExtraPointerOverUi != null && ExtraPointerOverUi()) return true;
            var es = EventSystem.current;
            if (es == null) return false;
            // [터치] 손가락으로 누른 경우: 누른 자리에 UI가 있는지 직접 확인
            if (lastPressWasTouch && Time.frameCount - lastPressFrame <= 1)
            {
                var data = new PointerEventData(es) { position = lastPressPosition };
                var hits = new System.Collections.Generic.List<RaycastResult>();
                es.RaycastAll(data, hits);
                return hits.Count > 0;
            }
            return es.IsPointerOverGameObject();
        }

        /// <summary>화면 좌표 아래에 있는 Collider2D 중 T를 가진 것을 찾는다 (정렬 순서가 가장 앞인 것 우선).</summary>
        public static T FindAtScreenPoint<T>(Vector2 screenPosition, Camera cam) where T : Component
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return null;

            Vector3 world = cam.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, 0f));
            var hits = Physics2D.OverlapPointAll(new Vector2(world.x, world.y));

            // [통합] 숨겨진 아이템(종이 등)이 겹쳐 있으면 주변 물체(가로등·물웅덩이·벤치 등)보다 먼저 집는다.
            foreach (var hit in hits)
            {
                var pickup = hit.GetComponentInParent<WalkItemPickup>();
                if (pickup == null || !pickup.isActiveAndEnabled) continue;
                var c = hit.GetComponentInParent<T>();
                if (c != null) return c;
            }

            T best = null;
            int bestOrder = int.MinValue;
            foreach (var hit in hits)
            {
                var c = hit.GetComponentInParent<T>();
                if (c == null) continue;
                var sr = hit.GetComponent<SpriteRenderer>();
                int order = sr != null ? sr.sortingOrder : 0;
                if (best == null || order > bestOrder)
                {
                    best = c;
                    bestOrder = order;
                }
            }
            return best;
        }
    }
}
