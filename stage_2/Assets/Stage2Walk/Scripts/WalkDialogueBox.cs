using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace EXOD.Walk
{
    /// <summary>
    /// 팀 DialogueManager가 없는 곳(스테이지2 단독 프로젝트)에서 쓰는 대화창.
    /// 팀 대화창과 같은 이미지·폰트·배치·글자색·타이핑 속도로 만들었다.
    /// Space 또는 클릭으로 넘기고, 글자가 나오는 중이면 한 번에 다 보여준다.
    /// </summary>
    public class WalkDialogueBox : MonoBehaviour
    {
        public static WalkDialogueBox Instance { get; private set; }

        const float TypingSeconds = 0.05f;   // 팀 DialogueManager 기본값

        public bool IsOpen { get; private set; }

        GameObject root;
        GameObject namePanel;
        Text body;
        Text nameText;
        readonly List<WalkLine> lines = new List<WalkLine>();
        int index;
        bool typing;
        Coroutine typingRoutine;
        int openedFrame;

        public static WalkDialogueBox Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("WalkDialogueBox");
            return go.AddComponent<WalkDialogueBox>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Build();
            Close();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Build()
        {
            Canvas canvas = WalkUi.CreateCanvas("WalkDialogueCanvas", 700, transform);

            // 화면 전체 클릭 영역 (대화 중 클릭이 산책 화면으로 새지 않게 막고, 클릭하면 넘김)
            var blocker = WalkUi.CreateImage("클릭 영역", canvas.transform, null, new Color(0f, 0f, 0f, 0f));
            WalkUi.Stretch(blocker.rectTransform);
            blocker.raycastTarget = true;
            var button = blocker.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(Advance);
            root = blocker.gameObject;

            // 대화창: 팀 씬과 같은 배치 (아래쪽, 좌우 210 여백, 높이 240, 바닥에서 150)
            var box = WalkUi.CreateImage("대화창", root.transform, WalkUi.DialogueBoxSprite, new Color(0.82f, 0.82f, 0.82f, 0.95f));
            RectTransform boxRect = box.rectTransform;
            boxRect.anchorMin = new Vector2(0f, 0f);
            boxRect.anchorMax = new Vector2(1f, 0f);
            boxRect.pivot = new Vector2(0.5f, 0.5f);
            boxRect.anchoredPosition = new Vector2(0f, 150f);
            boxRect.sizeDelta = new Vector2(-420f, 240f);

            body = WalkUi.CreateText("대화 텍스트", boxRect, 42, WalkUi.DialogueTextColor, TextAnchor.MiddleLeft);
            RectTransform bodyRect = body.rectTransform;
            WalkUi.Stretch(bodyRect, new Vector2(-328f, -48f));
            bodyRect.anchoredPosition = new Vector2(-128f, 0f);

            // 이름표: 팀 씬과 같은 위치 (왼쪽 아래 기준 210, 270)
            nameText = WalkUi.CreateNameBox("이름표", root.transform, out RectTransform nameRect);
            nameRect.anchorMin = nameRect.anchorMax = Vector2.zero;
            nameRect.anchoredPosition = new Vector2(210f, 270f);
            namePanel = nameRect.gameObject;
        }

        void Update()
        {
            if (!IsOpen || Time.frameCount == openedFrame) return;
            if (WalkInput.AdvanceDialogueDown()) Advance();
        }

        public void Show(IReadOnlyList<WalkLine> newLines)
        {
            lines.Clear();
            if (newLines != null)
            {
                foreach (WalkLine line in newLines)
                    if (line != null && !string.IsNullOrEmpty(line.text)) lines.Add(line);
            }
            if (lines.Count == 0) return;

            index = 0;
            IsOpen = true;
            openedFrame = Time.frameCount;
            root.SetActive(true);
            ShowLine();
        }

        void ShowLine()
        {
            WalkLine line = lines[index];
            string speaker = line.speaker != null ? line.speaker.Trim() : "";
            if (speaker == "얀데레" || speaker == "여주") speaker = "여자친구";   // 팀 DialogueManager와 같은 표기

            namePanel.SetActive(speaker.Length > 0);
            nameText.text = speaker;

            if (typingRoutine != null) StopCoroutine(typingRoutine);
            typingRoutine = StartCoroutine(Type(line.text));
        }

        IEnumerator Type(string text)
        {
            typing = true;
            body.text = "";
            foreach (char c in text)
            {
                body.text += c;
                yield return new WaitForSecondsRealtime(TypingSeconds);
            }
            typing = false;
        }

        public void Advance()
        {
            if (!IsOpen || Time.frameCount == openedFrame) return;

            if (typing)
            {
                if (typingRoutine != null) StopCoroutine(typingRoutine);
                body.text = lines[index].text;
                typing = false;
                return;
            }

            index++;
            if (index < lines.Count) ShowLine();
            else Close();
        }

        void Close()
        {
            IsOpen = false;
            typing = false;
            if (typingRoutine != null) StopCoroutine(typingRoutine);
            if (body != null) body.text = "";
            if (root != null) root.SetActive(false);
        }
    }
}
