using UnityEngine;

/// <summary>
/// 엔딩 목록 (시작 화면 [엔딩 목록]에서 보여 줌, 사용자 요청 2026-11-02)
/// - 엔딩 화면이 뜨는 순간(PlayerFaintController.ShowEndScreen) 본 엔딩으로 기록합니다.
/// - 기록은 PlayerPrefs에 저장되어 게임을 껐다 켜도 남습니다.
/// - 못 본 엔딩은 "???"로 가립니다. (힌트 없음 — 사용자 결정)
/// 엔딩을 새로 추가하면 EndingStyle에 하나 넣고 아래 Entries에도 한 줄 추가하세요.
/// </summary>
public static class EndingCollection
{
    public struct Entry
    {
        public EndingStyle Style;
        public string Title;
        public string Subtitle;
        public string LastLine;
        public Color Accent;
    }

    private const string KeyPrefix = "EXOD.EndingSeen.";

    public static readonly Entry[] Entries =
    {
        new Entry
        {
            Style = EndingStyle.GameOver,
            Title = "GAME OVER",
            Subtitle = string.Empty,
            LastLine = Stage1Dialogues.GameOverLine,
            Accent = new Color(0.78f, 0.08f, 0.12f, 1f),
        },
        new Entry
        {
            Style = EndingStyle.Submission,
            Title = Stage1Dialogues.PotionEndingTitle,
            Subtitle = Stage1Dialogues.PotionEndingSubtitle,
            LastLine = Stage1Dialogues.PotionEndingLine,
            Accent = new Color(0.86f, 0.32f, 0.56f, 1f),
        },
        new Entry
        {
            Style = EndingStyle.Proposal,
            Title = Stage1Dialogues.ProposalEndingTitle,
            Subtitle = Stage1Dialogues.ProposalEndingSubtitle,
            LastLine = Stage1Dialogues.ProposalEndingLine,
            Accent = new Color(1f, 0.62f, 0.80f, 1f),
        },
        new Entry
        {
            Style = EndingStyle.Escape,
            Title = Stage1Dialogues.EscapeEndingTitle,
            Subtitle = Stage1Dialogues.EscapeEndingSubtitle,
            LastLine = Stage1Dialogues.EscapeEndingLine,
            Accent = new Color(0.45f, 0.62f, 1f, 1f),
        },
    };

    public static bool IsSeen(EndingStyle style) => PlayerPrefs.GetInt(KeyPrefix + style, 0) == 1;

    public static void MarkSeen(EndingStyle style)
    {
        if (IsSeen(style))
            return;
        PlayerPrefs.SetInt(KeyPrefix + style, 1);
        PlayerPrefs.Save();
    }

    public static int SeenCount
    {
        get
        {
            int count = 0;
            foreach (Entry entry in Entries)
                if (IsSeen(entry.Style))
                    count++;
            return count;
        }
    }

    /// <summary>테스트용: 본 엔딩 기록을 모두 지웁니다.</summary>
    public static void ResetAll()
    {
        foreach (Entry entry in Entries)
            PlayerPrefs.DeleteKey(KeyPrefix + entry.Style);
        PlayerPrefs.Save();
    }
}
