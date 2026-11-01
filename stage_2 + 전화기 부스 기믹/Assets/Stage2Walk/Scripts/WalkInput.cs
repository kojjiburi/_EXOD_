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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => ExtraPointerOverUi = null;

        /// <summary>A/D 또는 ←/→ : -1 ~ 1</summary>
        public static float Horizontal()
        {
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
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                screenPosition = mouse.position.ReadValue();
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
            var mouse = Mouse.current;
            return mouse != null ? mouse.position.ReadValue() : Vector2.zero;
#else
            return Input.mousePosition;
#endif
        }

        /// <summary>산책 중간에 그만두기 (Esc)</summary>
        public static bool QuitWalkDown()
        {
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
            return es != null && es.IsPointerOverGameObject();
        }

        /// <summary>화면 좌표 아래에 있는 Collider2D 중 T를 가진 것을 찾는다 (정렬 순서가 가장 앞인 것 우선).</summary>
        public static T FindAtScreenPoint<T>(Vector2 screenPosition, Camera cam) where T : Component
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return null;

            Vector3 world = cam.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, 0f));
            var hits = Physics2D.OverlapPointAll(new Vector2(world.x, world.y));

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
