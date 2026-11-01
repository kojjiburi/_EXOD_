using System.Collections;
using UnityEngine;

namespace EXOD.Walk
{
    /// <summary>
    /// 집 ↔ 산책 사이의 화면 전환(검은 화면으로 어두워졌다가 밝아짐). 씬이 바뀌어도 유지된다.
    /// </summary>
    public class WalkFade : MonoBehaviour
    {
        public const float DefaultDuration = 0.4f;

        static WalkFade instance;
        CanvasGroup group;

        public static bool IsDark => instance != null && instance.group != null && instance.group.alpha > 0.5f;

        static WalkFade Get()
        {
            if (instance != null) return instance;
            var go = new GameObject("WalkFade");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<WalkFade>();
            instance.Build();
            return instance;
        }

        void Build()
        {
            Canvas canvas = WalkUi.CreateCanvas("FadeCanvas", 1000, transform);
            var black = WalkUi.CreateImage("Black", canvas.transform, null, Color.black);
            WalkUi.Stretch(black.rectTransform);
            black.raycastTarget = true;   // 어두운 동안 클릭 막기
            group = canvas.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        public static IEnumerator FadeOut(float duration = DefaultDuration)
        {
            yield return Get().Run(1f, duration);
        }

        public static IEnumerator FadeIn(float duration = DefaultDuration)
        {
            if (instance == null) yield break;
            yield return instance.Run(0f, duration);
        }

        IEnumerator Run(float target, float duration)
        {
            group.blocksRaycasts = true;
            float start = group.alpha;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(t / duration));
                yield return null;
            }
            group.alpha = target;
            group.blocksRaycasts = target > 0.5f;
        }
    }
}
