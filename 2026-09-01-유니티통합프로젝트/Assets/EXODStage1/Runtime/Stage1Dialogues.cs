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
    //  3. 거실 시작 → 테이블 위의 이상한 물약 (바로 마시고 연타 기믹)
    //     - 목이 말라 물약을 마시면 '탈출 하지 말까' 생각 지우기 연타 기믹 (StrangePotionGimmick)
    //       실패: "그냥 이렇게 살자" → 하트 -1 → 다시 도전 / 성공: "내가 무슨 생각을! 얼른 탈출하자"
    // ═══════════════════════════════════════════════════

    public static List<DialogueLine> LivingRoomArrival() => new List<DialogueLine>
    {
        Think("후… 일단 방 하나는 빠져나왔다."),
        Think("너무 머리를 굴렸나. 목이 바싹 마르네."),
        Think("…테이블 위에 물약 병이 하나 놓여 있다."),
    };

    // 거실 도착 대사 뒤에 바로 마시고, 이 대사 뒤에 머릿속 연타 기믹 시작
    public static List<DialogueLine> PotionDrink() => new List<DialogueLine>
    {
        Narrate("꿀꺽, 꿀꺽."),
        Say(Player, "…윽?! 뭐야, 이 맛…!"),
        Think("머릿속에… 이상한 생각이 차오른다."),
        Think("'탈출 하지 말까…'"),
        Think("안 돼… 이 생각을 떨쳐내야 해!"),
    };

    // 연타 기믹 화면에 떠다니는 생각
    public const string PotionThought = "탈출 하지 말까";

    // 기믹 실패: 이 대사 뒤에 하트 1개 감소 → 다시 도전
    public static List<DialogueLine> PotionFail() => new List<DialogueLine>
    {
        Say(Player, "그냥 이렇게 살자"),
    };

    public static List<DialogueLine> PotionRetry() => new List<DialogueLine>
    {
        Think("…아니, 정신 차려! 다시 떨쳐내자."),
    };

    // 기믹 성공
    public static List<DialogueLine> PotionSuccess() => new List<DialogueLine>
    {
        Say(Player, "내가 무슨 생각을! 얼른 탈출하자"),
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
            case "bear_right_arm": return "전자레인지에 왜 이게 있어?";
            case "bear_left_leg": return "텅 빈 보드에… 못으로 박혀 있어.";
            case "bear_right_leg": return "…변기 속에서 나오다니. 불쌍한 녀석.";
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
    };

    // 냉장고 안 그림을 띄운 채로 나오는 대사
    public static List<DialogueLine> FridgeInside() => new List<DialogueLine>
    {
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
    //  현관문을 나설 때 → 2스테이지(산책) 입장 컷신 (Stage2HouseLink)
    // ═══════════════════════════════════════════════════

    // 처음 나갈 때: 문 앞에 그녀가 서 있는 반전
    public static List<DialogueLine> Stage2EnterFirst() => new List<DialogueLine>
    {
        Narrate("끼익— 현관문이 열렸다."),
        Narrate("문 앞에… 누군가 서 있다."),
        Say(Girlfriend, "어머, 자기야. 마중 나와 준 거야?"),
        Think("…들켰다. 심장이 멎는 줄 알았어."),
        Say(Girlfriend, "마침 잘됐다. 우리 같이 산책하자 ♡"),
    };

    // 두 번째부터 (다음 날 다시 나갈 때)
    public static List<DialogueLine> Stage2EnterAgain() => new List<DialogueLine>
    {
        Narrate("현관문을 열자, 그녀가 기다리고 있었다."),
        Say(Girlfriend, "오늘도 산책 갈 시간이야, 자기야."),
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
