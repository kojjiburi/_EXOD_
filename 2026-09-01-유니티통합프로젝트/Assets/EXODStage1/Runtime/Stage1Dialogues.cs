using System.Collections.Generic;

/// <summary>
/// 1스테이지 대사 모음 (2026-10-29 확정본)
/// 대사를 고칠 때는 이 파일만 수정하면 됩니다.
/// 이름 없이 나오는 줄(Think, Narrate)은 이름표가 표시되지 않습니다.
/// </summary>
public static class Stage1Dialogues
{
    public const string Player = "주인공";
    public const string Girlfriend = "여자친구";
    public const string Unknown = "???";
    public const string BearUnknown = "곰인형…?";
    public const string Bear = "곰인형";
    public const string DoorLock = "도어락";

    public const string GameOverLine = "우리는 평생 함께야... ♡";
    public const string FaintText = "플레이어가 기절했다.";

    // 곰인형 보상 아이템 (냉장고 키패드 비밀번호가 적힌 쪽지)
    public const string FridgeNoteItemId = "fridge_lock_note";
    public const string FridgeNoteItemName = "냉장고 비밀번호 쪽지";
    public const string FridgePassword = "0214";

    // 현관 도어락 비밀번호: 냉장고 과일 개수를 메모의 글자 수 순서(감, 사과, 복숭아, 파인애플)로 → 5 2 3 1
    public const string FrontDoorPassword = "5231";

    // ─── 줄 만들기 도우미 ─────────────────────────────────

    private static DialogueLine Say(string speaker, string text) =>
        new DialogueLine { speakerName = speaker, text = text, dialogueType = DialogueType.Character };

    // 주인공 속마음 (이름표 없음)
    private static DialogueLine Think(string text) =>
        new DialogueLine { speakerName = string.Empty, text = text, dialogueType = DialogueType.Narration };

    // 상황 설명 (이름표 없음)
    private static DialogueLine Narrate(string text) =>
        new DialogueLine { speakerName = string.Empty, text = text, dialogueType = DialogueType.Narration };

    /// <summary>주인공 속마음 몇 줄을 바로 대화로 띄울 때 사용합니다.</summary>
    public static List<DialogueLine> Thought(params string[] texts)
    {
        List<DialogueLine> lines = new List<DialogueLine>();
        foreach (string text in texts)
            lines.Add(Think(text));
        return lines;
    }

    // ═══════════════════════════════════════════════════
    //  1. 인트로
    // ═══════════════════════════════════════════════════

    public static List<DialogueLine> IntroOpening() => new List<DialogueLine>
    {
        Narrate("똑. 똑. 똑."),
        Say(Unknown, "자기야~ 일어났어?"),
        Say(Player, "…어, 일어났어."),
        Think("오늘도 어김없이 왔네. 하루도 빠짐없이, 같은 시간에."),
        Think("이대로 있다간 숨이 막혀 죽을 것 같아. …여길 나가야 해."),
        Say(Girlfriend, "…무슨 생각해?"),
    };

    public static readonly string[] IntroChoices =
    {
        "아, 아무것도 아냐!",
        "…네 생각 하고 있었어.",
    };

    public static List<DialogueLine> IntroReply(int choice)
    {
        List<DialogueLine> lines = choice == 0
            ? new List<DialogueLine>
            {
                Say(Girlfriend, "아무것도? …그럼 내 생각은 안 했다는 거야?"),
                Say(Player, "아, 아니! 당연히 네 생각 했지! 사, 사랑해… 하하."),
                Say(Girlfriend, "후훗. 알면 됐어."),
            }
            : new List<DialogueLine>
            {
                Say(Girlfriend, "역시~ 그럴 줄 알았어 ♡"),
            };

        lines.Add(Say(Girlfriend, "이따 밥 가져올게. 그때까지 얌전히 기다려야 해 ♡"));
        lines.Add(Narrate("철컥. 밖에서 문 잠기는 소리가 들렸다."));
        lines.Add(Think("…좋아. 그녀가 다시 오기 전에, 이 방부터 빠져나가자."));
        return lines;
    }

    // ═══════════════════════════════════════════════════
    //  2. 침실 탈출 → 곰인형
    // ═══════════════════════════════════════════════════

    public static List<DialogueLine> BearFirstMeet() => new List<DialogueLine>
    {
        Narrate("철컥. 열쇠로 문을 열었다."),
        Say(BearUnknown, "저, 저기…"),
        Say(Player, "으악?! 뭐, 뭐야! 인형이 왜 말을 해?!"),
        Say(BearUnknown, "'뭐야'라니… 너무해."),
        Say(BearUnknown, "그녀가 내 팔다리를 뜯어서, 집 안 여기저기에 숨겨 놨어."),
        Say(BearUnknown, "혹시… 내 팔다리를 찾아줄 수 있을까?"),
        Say(Player, "…알았어. 어차피 집 안을 다 뒤져야 하니까."),
        Say(BearUnknown, "정말? 찾아주면 꼭 보답할게!"),
    };

    // ═══════════════════════════════════════════════════
    //  3. 거실 시작 → 테이블 위의 물 (선택지)
    // ═══════════════════════════════════════════════════

    public static List<DialogueLine> LivingRoomArrival() => new List<DialogueLine>
    {
        Think("후… 일단 방 하나는 빠져나왔다."),
        Think("너무 머리를 굴렸나. 목이 바싹 마르네."),
        Think("…마침 테이블 위에 물컵이 있잖아?"),
    };

    public static readonly string[] WaterChoices =
    {
        "물을 마신다",
        "마시지 않는다",
    };

    // 물을 마시면: 이 대사 뒤에 기절 (하트 1개 감소)
    public static List<DialogueLine> WaterDrink() => new List<DialogueLine>
    {
        Narrate("꿀꺽, 꿀꺽."),
        Say(Player, "…윽?! 뭐야, 이 맛…!"),
        Think("몸이… 축 늘어진다. 눈앞이 빙글빙글…"),
    };

    public static List<DialogueLine> WaterAfterFaint() => new List<DialogueLine>
    {
        Think("…으, 머리야. 얼마나 쓰러져 있었던 거지?"),
        Think("그녀가 둔 건 함부로 입에 대지 말자."),
    };

    public static List<DialogueLine> WaterRefuse() => new List<DialogueLine>
    {
        Think("…아니야. 그녀가 둔 걸 함부로 마실 순 없지."),
        Think("목마른 건 참고, 나갈 방법부터 찾자."),
    };

    // ═══════════════════════════════════════════════════
    //  4. 곰돌이 팔다리
    // ═══════════════════════════════════════════════════

    // 팔다리 아이템 ID별로 주웠을 때의 한마디
    public static string BearLimbFound(string limbId)
    {
        switch (limbId)
        {
            case "bear_left_arm": return "소파 쿠션 사이에… 곰돌이 왼팔이다.";
            case "bear_right_arm": return "냄비 안에 이게 왜 들어 있어…?";
            case "bear_left_leg": return "액자 뒤에 뭔가 걸려 있다.";
            case "bear_right_leg": return "…변기 뒤에서 나오다니. 불쌍한 녀석.";
            default: return "곰돌이 팔다리를 찾았다.";
        }
    }

    public static List<DialogueLine> BearAllCollected() => new List<DialogueLine>
    {
        Say(Player, "됐다, 다 모았어! 얼른 곰인형한테 가져다주자."),
    };

    public static List<DialogueLine> BearReturn() => new List<DialogueLine>
    {
        Say(Player, "자, 여기! 네 팔다리."),
        Say(Bear, "와아… 정말 다 찾아줬구나! 이 은혜를 어떻게 갚지?"),
        Say(Bear, "(주머니를 뒤적뒤적) …이거 받아! 그녀가 흘리고 간 쪽지야."),
        Say(Player, "오, 아싸~!"),
        Narrate("쪽지를 펼쳐 보니 숫자가 적혀 있다. 「" + FridgePassword + "」"),
        Think("…어디에 쓰는 번호지? 자물쇠 같은 데 쓰는 건가?"),
    };

    // ═══════════════════════════════════════════════════
    //  5. 냉장고
    // ═══════════════════════════════════════════════════

    public static List<DialogueLine> FridgeMemo() => new List<DialogueLine>
    {
        Narrate("냉장고에 메모가 붙어 있다. 「복숭아, 감, 사과, 파인애플」"),
        Think("…장 볼 목록인가?"),
    };

    public static List<DialogueLine> FridgeLocked() => new List<DialogueLine>
    {
        Narrate("냉장고 문에 숫자 키패드 잠금장치가 달려 있다."),
        Think("…냉장고를 대체 왜 잠가 놓은 거지?"),
    };

    public static List<DialogueLine> FridgeOpened() => new List<DialogueLine>
    {
        Narrate("철컥. 냉장고 문이 열렸다."),
        Narrate("안쪽 깊숙이… 검은 비닐봉지가 들어 있다."),
        Think("서, 설마… 이거… 시… 시체…?"),
        Think("…그래도 확인은 해야겠지. (부스럭)"),
        Narrate("봉지 안에는 복숭아 3개, 감 5개, 사과 2개, 파인애플 1개가 들어 있다."),
        Say(Player, "…휴, 과일이잖아. 간 떨어질 뻔했네."),
        Think("근데 개수를 딱딱 맞춰서 넣어 놨네… 뭔가 의미가 있는 걸까?"),
    };

    // 이미 연 냉장고를 다시 눌렀을 때
    public static List<DialogueLine> FridgeAlreadyOpen() => new List<DialogueLine>
    {
        Think("봉지 안에는 복숭아 3개, 감 5개, 사과 2개, 파인애플 1개."),
        Think("개수에 무슨 의미가 있을 것 같은데…"),
    };

    // ═══════════════════════════════════════════════════
    //  6. 현관
    // ═══════════════════════════════════════════════════

    public static List<DialogueLine> FrontDoorFirstLook() => new List<DialogueLine>
    {
        Think("도어락이… 안쪽에 달려 있네? 보통은 밖에 있지 않나."),
        Think("…아. 나를 가두려고 일부러 이렇게 단 거구나."),
    };

    public static List<DialogueLine> FrontDoorWrongPassword() => new List<DialogueLine>
    {
        Narrate("삐용- 삐용- 삐용-!"),
        Say(Girlfriend, "자기야… 거기서 뭐 해?"),
    };

    public static List<DialogueLine> FrontDoorUnlocked() => new List<DialogueLine>
    {
        Narrate("삐리릭. …도어락 화면에 글자가 떠오른다."),
    };

    public static List<DialogueLine> FrontDoorQuestion1() => new List<DialogueLine>
    {
        Say(DoorLock, "[보안 질문 1] 이성 친구는 존재한다고 생각합니까?"),
    };

    public static List<DialogueLine> FrontDoorQuestion2() => new List<DialogueLine>
    {
        Say(DoorLock, "[보안 질문 2] 영원한 사랑은 존재한다고 생각합니까?"),
    };

    public static readonly string[] YesNoChoices = { "예", "아니요" };

    // 질문 1에서 '예'를 골랐을 때 (기절)
    public static List<DialogueLine> FrontDoorQuestion1Fail() => new List<DialogueLine>
    {
        Say(Girlfriend, "…뭐라고?"),
    };

    // 질문 2에서 '아니요'를 골랐을 때 (기절)
    public static List<DialogueLine> FrontDoorQuestion2Fail() => new List<DialogueLine>
    {
        Say(Girlfriend, "괜찮아. 이제부터… 천천히 알게 될 거야."),
    };

    public static List<DialogueLine> FrontDoorPassed() => new List<DialogueLine>
    {
        Say(DoorLock, "인증되었습니다. 사랑해, 자기야 ♡"),
        Say(Player, "휴… 매일같이 받던 질문 공세가 이럴 때 도움이 되네."),
    };

    // ═══════════════════════════════════════════════════
    //  7. 1스테이지 끝
    // ═══════════════════════════════════════════════════

    public static List<DialogueLine> StageEnd() => new List<DialogueLine>
    {
        Narrate("끼익— 현관문이 열렸다."),
        Narrate("…밖은 이미 캄캄한 밤이었다."),
        Say(Player, "뭐야, 벌써 밤이라고?! 한 것도 없는데…!"),
        Say(Player, "그녀가 오기 전에, 빨리 전부 원래대로 돌려놔야 해!"),
    };
}
