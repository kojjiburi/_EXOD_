using UnityEngine;

namespace EXOD.Walk
{
    /// <summary>
    /// 오브젝트 위에 이름을 띄우는 디버그용 라벨. 에셋/UI가 정리되면 지워도 된다.
    /// onlyWhenHovered를 켜면 마우스를 올렸을 때만 보인다 (클릭 가능한 물체 표시용).
    /// </summary>
    public class WorldLabel : MonoBehaviour
    {
        [SerializeField] string text = "";
        [SerializeField] Vector3 offset = new Vector3(0f, 1f, 0f);
        [SerializeField] int fontSize = 18;
        [SerializeField] bool onlyWhenHovered = false;

        SpriteRenderer sr;

        public void SetText(string t) => text = t;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
        }

        void OnGUI()
        {
            if (string.IsNullOrEmpty(text) || !isActiveAndEnabled) return;
            var cam = Camera.main;
            if (cam == null) return;

            // 마우스를 올렸을 때 보이는 이름은 WalkHud가 1스테이지와 같은 이름표 상자로 보여준다
            if (onlyWhenHovered) return;

            // 실제 스프라이트를 넣으면 임시 이름표는 감춘다
            if (sr != null && sr.sprite != null && !sr.sprite.name.StartsWith("white")) return;

            Vector3 sp = cam.WorldToScreenPoint(transform.position + offset);
            if (sp.z < 0f) return;

            var style = DayClock.HudStyle(fontSize);
            style.alignment = TextAnchor.MiddleCenter;
            float s = Screen.height / 720f;
            var rect = new Rect(sp.x - 150 * s, Screen.height - sp.y - 15 * s, 300 * s, 30 * s);
            GUI.Label(rect, text, style);
        }
    }
}
