using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace EXOD.Walk
{
    /// <summary>
    /// 가로등 관리 번호판. 가로등(WalkInteractable이 붙은 판정 영역)에 붙인다.
    /// 그녀가 앞을 보고 있을 때 클릭하면 번호판 내용이 대사로 나온다(뒤돌아봤을 때 클릭하면 들킴).
    /// 서로 다른 가로등을 정해진 개수만큼 보면 '주소지'를 얻는다 (PhoneBooth 설정).
    /// 확인한 가로등은 다음 날에도 기억한다.
    /// 가로등 기둥에는 관리 번호판 그림(자식 '관리 번호판')이 붙어 있고, 클릭하면 그 그림이 화면에 크게 보인다.
    /// </summary>
    [RequireComponent(typeof(WalkInteractable))]
    public class PhoneBoothLamp : MonoBehaviour
    {
        [Tooltip("가로등 번호 (1~4). 가로등마다 달라야 한다")]
        [SerializeField] int lampIndex = 1;
        [Tooltip("비워 두면 산책 씬의 전화 부스를 자동으로 찾는다")]
        [SerializeField] PhoneBooth booth;
        [Tooltip("클릭했을 때 나오는 번호판 내용")]
        [SerializeField] WalkLine[] plateLines =
        {
            new WalkLine("", "(가로등 기둥에 관리 번호판이 붙어 있다.)")
        };
        [Tooltip("이미 본 번호판을 다시 클릭했을 때 덧붙는 대사 (비워도 됨)")]
        [SerializeField] WalkLine[] alreadyCheckedLines = new WalkLine[0];

        [Header("번호판 그림")]
        [Tooltip("가로등 기둥에 붙은 번호판 (자식 오브젝트의 SpriteRenderer). 그림을 바꾸려면 이 SpriteRenderer의 Sprite를 바꾼다")]
        [SerializeField] SpriteRenderer plateRenderer;
        [Tooltip("크게 보기용 그림 (비워 두면 기둥에 붙은 그림을 그대로 크게 보여준다)")]
        [SerializeField] Sprite closeupSprite;
        [Tooltip("클릭했을 때 번호판 그림을 화면에 크게 보여준다 (대사가 끝나면 사라짐)")]
        [SerializeField] bool showCloseup = true;
        [Tooltip("크게 보일 때 화면 높이 (1920×1080 기준)")]
        [SerializeField] float closeupHeight = 420f;

        Canvas closeupCanvas;
        Image closeupImage;

        public int LampIndex => lampIndex;
        public bool Checked => PhoneBoothProgress.LampsChecked.Contains(lampIndex);

        void Awake()
        {
            GetComponent<WalkInteractable>().onInteract.AddListener(OnClicked);
        }

        void OnClicked()
        {
            if (booth == null) booth = PhoneBooth.Current;

            bool wasChecked = Checked;
            var lines = new List<WalkLine>();
            if (plateLines != null) lines.AddRange(plateLines);
            if (wasChecked && alreadyCheckedLines != null) lines.AddRange(alreadyCheckedLines);

            if (booth != null) lines.AddRange(booth.OnLampChecked(lampIndex));
            else
            {
                PhoneBoothProgress.LampsChecked.Add(lampIndex);
                Debug.LogWarning("[PhoneBooth] 산책 씬에서 전화 부스(PhoneBooth)를 찾지 못해 주소지를 줄 수 없습니다.", this);
            }

            if (lines.Count > 0) WalkDialogue.Show(lines);
            if (showCloseup && CloseupSprite != null) StartCoroutine(ShowCloseup());
        }

        Sprite CloseupSprite => closeupSprite != null ? closeupSprite : plateRenderer != null ? plateRenderer.sprite : null;

        IEnumerator ShowCloseup()
        {
            if (closeupCanvas == null)
            {
                closeupCanvas = WalkUi.CreateCanvas("번호판 크게 보기", 690, transform);   // 대화창(700) 바로 아래
                closeupImage = WalkUi.CreateImage("번호판", closeupCanvas.transform, null, Color.white);
                closeupImage.preserveAspect = true;
            }
            Sprite sprite = CloseupSprite;
            closeupImage.sprite = sprite;
            closeupImage.color = Color.white;
            float aspect = sprite.rect.width / Mathf.Max(1f, sprite.rect.height);
            WalkUi.Place(closeupImage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 80f), new Vector2(closeupHeight * aspect, closeupHeight));
            closeupCanvas.gameObject.SetActive(true);

            yield return null;
            while (WalkDialogue.IsOpen) yield return null;
            if (closeupCanvas != null) closeupCanvas.gameObject.SetActive(false);
        }
    }
}
