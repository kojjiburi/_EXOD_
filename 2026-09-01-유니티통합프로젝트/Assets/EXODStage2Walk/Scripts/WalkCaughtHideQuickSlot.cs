using UnityEngine;

namespace EXOD.Walk
{
    /// <summary>
    /// 산책 중 들켰을 때 화면 아래 퀵슬롯 바를 숨긴다 (칼 든 그녀 그림이 가려지지 않게).
    /// WalkYandere와 같은 오브젝트에 붙인다. 산책 씬이 닫히면 퀵슬롯을 다시 보이게 한다.
    /// 퀵슬롯은 투명하게만 만들고 끄지 않으므로 들고 있던 아이템은 그대로다.
    /// </summary>
    [RequireComponent(typeof(WalkYandere))]
    public class WalkCaughtHideQuickSlot : MonoBehaviour
    {
        WalkYandere yandere;
        CanvasGroup hiddenGroup;
        float previousAlpha = 1f;
        bool previousBlocksRaycasts = true;
        bool addedGroup;

        void Awake()
        {
            yandere = GetComponent<WalkYandere>();
            yandere.onCaught.AddListener(Hide);
        }

        void OnDestroy()
        {
            if (yandere != null) yandere.onCaught.RemoveListener(Hide);
            Show();
        }

        void Hide()
        {
            if (hiddenGroup != null) return;

            var bar = FindAnyObjectByType<QuickSlotController>();
            if (bar == null) return;

            hiddenGroup = bar.GetComponent<CanvasGroup>();
            addedGroup = hiddenGroup == null;
            if (addedGroup) hiddenGroup = bar.gameObject.AddComponent<CanvasGroup>();

            previousAlpha = hiddenGroup.alpha;
            previousBlocksRaycasts = hiddenGroup.blocksRaycasts;
            hiddenGroup.alpha = 0f;
            hiddenGroup.blocksRaycasts = false;
        }

        void Show()
        {
            if (hiddenGroup == null) return;

            if (addedGroup)
            {
                Destroy(hiddenGroup);
            }
            else
            {
                hiddenGroup.alpha = previousAlpha;
                hiddenGroup.blocksRaycasts = previousBlocksRaycasts;
            }
            hiddenGroup = null;
        }
    }
}
