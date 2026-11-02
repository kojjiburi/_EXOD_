using UnityEngine;
using UnityEngine.UI;

namespace EXOD.Walk
{
    /// <summary>
    /// 산책 화면 표시. 1스테이지 UI와 같은 이름표 상자·Galmuri9 글꼴을 쓴다.
    ///  - 오른쪽 위: 조작 안내 (남은 시간은 DayClock 상자 하나만 보여준다)
    ///  - 마우스를 올린 물체: 이름표 상자로 이름 표시
    /// 왼쪽 위(DAY·하트·위험도)와 아래(퀵슬롯)는 팀 UI 자리라 비워 둔다.
    /// </summary>
    public class WalkHud : MonoBehaviour
    {
        WalkGameManager manager;
        Camera worldCamera;

        RectTransform canvasRect;
        GameObject statusGroup;
        RectTransform hoverBox;
        Text hoverText;

        public static WalkHud Create(WalkGameManager manager, Camera worldCamera)
        {
            var go = new GameObject("WalkHud");
            go.transform.SetParent(manager.transform, false);
            var hud = go.AddComponent<WalkHud>();
            hud.manager = manager;
            hud.worldCamera = worldCamera;
            hud.Build();
            return hud;
        }

        void Build()
        {
            Canvas canvas = WalkUi.CreateCanvas("WalkHudCanvas", 650, transform);
            canvasRect = (RectTransform)canvas.transform;

            statusGroup = WalkUi.CreateRect("산책 상태", canvas.transform).gameObject;
            WalkUi.Stretch((RectTransform)statusGroup.transform);

            Vector2 topRight = new Vector2(1f, 1f);
            var hint = WalkUi.CreateOutlinedText("조작", statusGroup.transform, 22, TextAnchor.MiddleCenter);
            WalkUi.Place(hint.rectTransform, topRight, new Vector2(-170f, -150f), new Vector2(420f, 34f));
            hint.text = "Esc 그만두기";
            hint.color = new Color(1f, 1f, 1f, 0.75f);

            hoverText = WalkUi.CreateNameBox("물체 이름", canvas.transform, out hoverBox, 32);
            hoverBox.anchorMin = hoverBox.anchorMax = new Vector2(0.5f, 0.5f);
            hoverBox.pivot = new Vector2(0f, 0f);
            hoverBox.gameObject.SetActive(false);
            statusGroup.SetActive(false);
        }

        void LateUpdate()
        {
            if (manager == null) return;

            bool running = manager.IsRunning;
            if (statusGroup.activeSelf != running) statusGroup.SetActive(running);

            UpdateHover(running);
        }

        void UpdateHover(bool running)
        {
            WalkInteractable target = null;
            Vector2 pointer = WalkInput.PointerScreenPosition();
            if (running && !WalkDialogue.IsOpen && WalkGameManager.Overlay == null && !WalkInput.IsPointerOverUI())
                target = WalkInput.FindAtScreenPoint<WalkInteractable>(pointer, worldCamera);

            bool show = target != null;
            if (hoverBox.gameObject.activeSelf != show) hoverBox.gameObject.SetActive(show);
            if (!show) return;

            hoverText.text = target.DisplayName;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, pointer, null, out Vector2 local))
            {
                // 커서 오른쪽 위에 띄우되 화면 밖으로 나가지 않게
                Vector2 half = canvasRect.rect.size * 0.5f;
                Vector2 size = hoverBox.sizeDelta;
                local += new Vector2(24f, 24f);
                local.x = Mathf.Min(local.x, half.x - size.x - 8f);
                local.y = Mathf.Min(local.y, half.y - size.y - 8f);
                hoverBox.anchoredPosition = local;
            }
        }
    }
}
