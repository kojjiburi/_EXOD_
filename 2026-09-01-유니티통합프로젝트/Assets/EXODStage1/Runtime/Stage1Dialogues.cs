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

    // 현관 도어락 비밀번호: 냉장고 과일 개수를 메모에 적힌 순서(감, 사과, 복숭아, 파인애플)대로 → 5 2 3 1
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
                DialoguePortrait.WithWalkCaught(Say(Girlfriend, "아무것도? …그럼 내 생각은 안 했다는 거야?")),   // 칼 든 검은 그림 (2026-11-03)
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
        lines.Add(Narrate("(무엇을 할지 모르겠으면 행동요령의 노란 글씨를 잘 보자)"));
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
    //       실패: 탈출을 포기하고 그녀 곁에 남음 → 순응 엔딩 : 따뜻한 감옥 (하트 감소 없음)
    //       성공: "내가 무슨 생각을! 얼른 탈출하자"
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

    // 물약 기믹 머릿속 생각 4가지 (사용자 문구 2026-11-03): 한 번씩은 꼭 나오고, 그 뒤로는 무작위
    public static readonly string[] PotionThoughts =
    {
        "탈출하지 말까..",
        "생각보다 살기 좋은데?",
        "여자친구도 이쁘잖아..",
        "몰라.. 다 귀찮아...",
    };

    // 기믹 실패: 이 대사 뒤에 암전 → 순응 엔딩 (2026-11-02 사용자 확정)
    public static List<DialogueLine> PotionFail() => new List<DialogueLine>
    {
        Say(Player, "그냥 이렇게 살자…"),
        Think("…앗."),
        Think("근데… 생각해 보면, 나쁠 것도 없잖아?"),
        Think("예쁘고, 날 이렇게까지 사랑해 주는 여자친구."),
        Think("따뜻한 집, 꼬박꼬박 챙겨 주는 밥."),
        Think("여기서 오순도순 사는 것도… 행복한 거 아닌가?"),
        Narrate("철컥. 문이 열리고, 그녀가 들어왔다."),
        Say(Girlfriend, "어머, 자기야. 표정이 왜 그렇게 편안해?"),
        Say(Player, "…있잖아. 나, 그냥 여기서 너랑 살래."),
        Say(Girlfriend, "…정말?"),
        Say(Girlfriend, "잘 생각했어~ ♡"),
        Say(Girlfriend, "이제 아무 데도 안 가는 거지? 여기서, 영원히, 나랑만."),
    };

    // 순응 엔딩 화면 (암전 뒤 여자친구 마지막 대사 → 제목·설명)
    public const string PotionEndingLine = "우린 이제 평생 함께야 ♡";
    public const string PotionEndingTitle = "순응 엔딩 : 따뜻한 감옥";
    public const string PotionEndingSubtitle = "탈출을 포기하고, 그녀의 곁에 남았다.";

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

    // 팔다리를 다 돌려줬을 때 (보답은 다음 날 아침 컷신에서 — 2026-11-02 사용자 결정)
    public static List<DialogueLine> BearReturn()
    {
        List<DialogueLine> lines = new List<DialogueLine>
        {
            Say(Player, "자, 여기! 네 팔다리."),
            Say(Bear, "와아… 정말 다 찾아주셨네요!"),
        };
        // [이지] 그 자리에서 바로 보답하므로 "꼭 은혜를 갚을게요!"는 뺌 (사용자 요청 2026-11-03)
        if (!GameDifficulty.IsEasy)
            lines.Add(Say(Bear, "꼭 은혜를 갚을게요!"));
        return lines;
    }

    // [이지] 팔다리를 돌려준 그 자리에서 바로 보답 (BearReturn 뒤에 이어짐) — 이 대사 뒤 냉장고 비밀번호 쪽지가 퀵슬롯에 들어옴
    public static List<DialogueLine> BearSameDayGift() => new List<DialogueLine>
    {
        Say(Bear, "아, 그리고 이거! 그녀가 몰래 숨겨 둔 쪽지예요."),
        Say(Bear, "어딘가의 비밀번호 같아요."),
        Say(Player, "오, 고마워."),
        Narrate("쪽지를 펼쳐 보니 숫자가 적혀 있다. 「" + FridgePassword + "」"),
        Think("…어디에 쓰는 번호지? 자물쇠 같은 데 쓰는 건가?"),
    };

    // 다음 날 아침 컷신 끝에 이어짐 (DayEndReport) — 이 대사 뒤 냉장고 비밀번호 쪽지가 퀵슬롯에 들어옴
    public static List<DialogueLine> BearMorningGift() => new List<DialogueLine>
    {
        Narrate("…그녀가 나가자, 침대 밑에서 무언가 꼼지락거렸다."),
        Say(Bear, "저기요! 밤새 집 안을 뒤졌는데… 제가 이걸 찾았어요!"),
        Say(Bear, "그녀가 몰래 숨겨 둔 쪽지예요. 어딘가의 비밀번호 같아요."),
        Say(Player, "오, 고마워."),
        Narrate("쪽지를 펼쳐 보니 숫자가 적혀 있다. 「" + FridgePassword + "」"),
        Think("…어디에 쓰는 번호지? 자물쇠 같은 데 쓰는 건가?"),
    };

    // 보답을 받기 전에 거실 곰인형을 다시 눌렀을 때
    public static List<DialogueLine> BearRewardWaiting() => new List<DialogueLine>
    {
        Say(Bear, "꼭 은혜를 갚을게요!"),
    };

    // ═══════════════════════════════════════════════════
    //  5. 냉장고
    // ═══════════════════════════════════════════════════

    public static List<DialogueLine> FridgeMemo() => new List<DialogueLine>
    {
        Narrate("냉장고에 메모가 붙어 있다. 「감, 사과, 복숭아, 파인애플」"),
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
        DialoguePortrait.WithWalkCaught(Say(Girlfriend, "…뭐라고?")),   // 칼 든 검은 그림
    };

    // 질문 2에서 '아니요'를 골랐을 때 (기절)
    public static List<DialogueLine> FrontDoorQuestion2Fail() => new List<DialogueLine>
    {
        DialoguePortrait.WithWalkCaught(Say(Girlfriend, "괜찮아. 이제부터… 천천히 알게 될 거야.")),   // 칼 든 검은 그림
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
    // (2026-11-03 사용자 요청) 앞부분은 '방 밖에서 들킴' 연출(색수차·컷신 이미지·UI 가림, 초상화 없음) 위에서 →
    // "…들켰다" 뒤 연출이 사라지고 화면이 정상으로 돌아온 다음 뒷부분 (Stage2HouseLink)
    public static List<DialogueLine> Stage2EnterFirstCaught() => new List<DialogueLine>
    {
        Say(Girlfriend, "자기야…?"),
        Narrate("끼익— 현관문이 열렸다."),
        Narrate("문 앞에… 누군가 서 있다."),
        Say(Girlfriend, "어머, 자기야. 마중 나와 준 거야?"),
        Think("들켰다....."),
        Say(Player, "...?"),   // (사용자 요청 2026-11-03) 이 줄까지 들킨 연출 위, 다음 줄부터 정상 화면
    };

    public static List<DialogueLine> Stage2EnterFirstAfter() => new List<DialogueLine>
    {
        Say(Girlfriend, "마침 잘됐다. 우리 같이 산책하자 ♡"),
    };

    // (예전 한 번에 이어진 버전 — 다른 곳에서 쓸 수 있게 남겨 둠)
    public static List<DialogueLine> Stage2EnterFirst()
    {
        List<DialogueLine> lines = Stage2EnterFirstCaught();
        lines.AddRange(Stage2EnterFirstAfter());
        return lines;
    }

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

    // ═══════════════════════════════════════════════════
    //  8. 여자친구 불시 방문 컷신 (위험도·등장 기능은 친구 담당, 그 기능에서 이 대사를 쓰면 됨) — 2026-11-02
    //     위험도가 높을 때 랜덤으로 → 2초 경고 → 침실에 있으면 무사, 아니면 화내고 하트 -1 + 다시 갇힘
    // ═══════════════════════════════════════════════════

    // 화면 가운데 큰 경고 (2초)
    public const string VisitWarningTitle = "…누군가의 발소리다.";
    public const string VisitWarningSub = "방으로 돌아가자!";

    // 침대로 일차를 넘기지 않고 하루 시간이 다 됐을 때, 침실 안에 있었으면 (DangerLevel, 사용자 요청 2026-11-03)
    // 구두 소리 → 이 대사 → 다음 일차
    public static List<DialogueLine> VisitBedtime() => new List<DialogueLine>
    {
        Say(Girlfriend, "뭐야? 아직도 안 자는 거야?"),
        Say(Girlfriend, "늦게 자면 건강에 안 좋으니까 얼른 자 ♡"),
        Say(Player, "강제로 눕혀졌다..."),
    };

    // 1일차 남은 시간 5초까지 침대를 누르지 않았을 때 (DayCycleController)
    public const string FirstDayBedWarning = "침대로 들어가 있지 않으면 여자친구가 올 것이다.";

    // 침실 안에서 위험도가 72 이상이 될 행동을 하려 할 때 (DangerLevel, 사용자 요청 2026-11-03)
    // 구두 소리 조금 → 방문 벌컥 → 이 대사 → 구두 소리. 여자친구 그림은 기본(웃는 얼굴 아님)
    public static List<DialogueLine> VisitRoomCheck() => new List<DialogueLine>
    {
        Say(Girlfriend, "음? 지금 뭐해?"),
        Say(Player, "!!"),
        Say(Player, "아하하... 아무것도 아니야"),
        Say(Girlfriend, "그래? 얌전히 잘 있어야 돼~"),
    };

    // 침실에 있었을 때
    // (사용자 요청 2026-11-02: 이 장면은 볼 빨간 웃는 얼굴 그림을 대화창 가운데에)
    public static List<DialogueLine> VisitSafe()
    {
        DialoguePortrait.UseSweetForNextDialogue(true);
        return VisitSafeLines();
    }

    private static List<DialogueLine> VisitSafeLines() => new List<DialogueLine>
    {
        Narrate("끼익— 방문이 살짝 열렸다."),
        Say(Girlfriend, "자기야~ 얌전히 잘 있었네? ♡"),
        Say(Girlfriend, "착하다. 조금만 더 기다려 줘."),
        Narrate("탁. 발소리가 멀어졌다."),
        Think("…휴, 들킬 뻔했다."),
    };

    // 침실 밖에서 들켰을 때 (이 대사 뒤 하트 -1, 침실로 끌려가 다시 갇힘)
    public static List<DialogueLine> VisitCaught() => new List<DialogueLine>
    {
        Say(Girlfriend, "…자기야?"),
        Say(Girlfriend, "왜 거기 있어? 방에서 기다리라고 했잖아."),
        Say(Player, "아, 그게… 목이 말라서…"),
        Say(Girlfriend, "거짓말."),
        Say(Girlfriend, "…나 지금 엄청 화났어."),
    };

    public static List<DialogueLine> VisitLockedAgain() => new List<DialogueLine>
    {
        Narrate("그녀에게 끌려 다시 방으로 돌아왔다."),
        Narrate("철컥. 밖에서 문 잠기는 소리가 들렸다."),
        Think("열쇠까지 빼앗겼다… 서랍에서 다시 꺼내야 해."),
    };

    // ═══════════════════════════════════════════════════
    //  9. 일차가 넘어갈 때 컷신 (DayEndReport) — 제자리에 안 돌려놓은 물건을 그녀가 짚어 줌
    // ═══════════════════════════════════════════════════

    public static List<DialogueLine> DayReportMissing(string itemNames) => new List<DialogueLine>
    {
        Narrate("아침이 밝았다. 그녀가 방 안을 천천히 둘러본다."),
        Say(Girlfriend, "…자기야."),
        Say(Girlfriend, "이거, 왜 제자리에 없어? 「" + itemNames + "」"),
        Say(Girlfriend, "내 물건 함부로 만지지 말랬잖아. …다음엔 진짜 화낼 거야."),
        Think("…정리하지 못했던 물건이 있었다."),
    };

    public static List<DialogueLine> DayReportClean() => new List<DialogueLine>
    {
        Narrate("아침이 밝았다."),
        Say(Girlfriend, "좋은 아침, 자기야~ 오늘도 얌전히 있었네? ♡"),
        Think("오늘은 무사히 넘긴 것 같다."),
    };

    // ═══════════════════════════════════════════════════
    //  10. 화장실 손거울 → 거울 미로 → 전화 부스 열쇠 (Stage1MirrorBoothKey) — 2026-11-02
    // ═══════════════════════════════════════════════════

    public const string BoothKeyItemId = "stage2_booth_key";
    public const string BoothKeyItemName = "부스 열쇠";

    // 미로 출구에 도착했을 때 (이 대사 뒤 부스 열쇠가 퀵슬롯에 들어옴)
    // 미로 안에서 떨어진 열쇠를 주운 뒤 미로를 닫으면 (줍기·"얻었다" 문구는 친구 미로 기믹이 보여 줌, 2026-11-02)
    // 아직 밖에 나가 본 적이 없으면 전화 부스 추측 대사는 빼기 (사용자 요청 2026-11-03)
    public static List<DialogueLine> MirrorKeyFound()
    {
        var lines = new List<DialogueLine> { Narrate("열쇠고리에 'TELEPHONE'이라고 적혀 있다.") };
        if (Stage2HouseLink.HasBeenOutside)
            lines.Add(Think("전화…? 밖에 있는 빨간 전화 부스 열쇠인가?"));
        return lines;
    }

    // 화장실 금고에 다른 비밀번호를 눌러 볼 때: 곰돌이 쪽지 0214(0 버튼이 없어 2·1·4) / 현관 5231 (RestroomGimmick)
    public static List<DialogueLine> SafeWrongNote() => new List<DialogueLine>
    {
        Think("아.. 여긴 아닌거같다."),
        Think("애초에 버튼 9개 다 누르지 않으면 안열리는 구조같다."),
    };

    // 벽에 너무 많이 부딪혀 닫혔을 때
    public static List<DialogueLine> MirrorFailed() => new List<DialogueLine>
    {
        Think("…눈이 핑 돈다. 거울 속 미로가 자꾸 뒤틀려 보여."),
        Think("조금 쉬었다가 다시 해 보자."),
    };

    // 이미 열쇠를 얻은 뒤 다시 눌렀을 때
    public static List<DialogueLine> MirrorEmpty() => new List<DialogueLine>
    {
        Think("거울 뒷면엔 테이프 자국만 남아 있다."),
    };

    // ═══════════════════════════════════════════════════
    //  11. 청혼 엔딩 (화장실 금고 안 청혼반지) — 2026-11-02 사용자 확정
    //      반지를 누르면 [반지를 집는다]/[그냥 둔다] → 집으면 컷신 → 암전·마지막 대사 → 엔딩 화면
    // ═══════════════════════════════════════════════════

    // 반지를 눌렀을 때 (이 대사 끝에 선택지)
    public static List<DialogueLine> ProposalRingFound() => new List<DialogueLine>
    {
        Narrate("금고 안에서 작은 반지 상자를 꺼냈다."),
        Think("…반지? 왜 이런 게 금고에…"),
    };

    public static readonly string[] ProposalRingChoices =
    {
        "반지를 집는다",
        "그냥 둔다",
    };

    public static List<DialogueLine> ProposalRingLeave() => new List<DialogueLine>
    {
        Think("…괜히 건드리지 말자. 왠지 불길해."),
    };

    // 반지를 집은 뒤 (이 대사 뒤 암전 → 마지막 대사 → 엔딩 화면)
    // (사용자 요청 2026-11-02: 청혼 장면은 볼 빨간 웃는 얼굴 그림)
    public static List<DialogueLine> ProposalCutscene()
    {
        DialoguePortrait.UseSweetForNextDialogue(false);
        return ProposalCutsceneLines();
    }

    private static List<DialogueLine> ProposalCutsceneLines() => new List<DialogueLine>
    {
        Narrate("반지 안쪽에 글자가 새겨져 있다. 「영원히, 나만의 자기」"),
        Narrate("등 뒤에서… 문이 열리는 소리가 났다."),
        Say(Girlfriend, "…찾았네?"),
        Say(Girlfriend, "원래는 더 예쁜 날 주려고 했는데. 뭐, 상관없어."),
        Say(Girlfriend, "자기가 먼저 찾아 줬다는 건… 그런 뜻이지?"),
        Say(Player, "아, 아니 그게…"),
        Narrate("그녀가 내 손에서 반지를 가져가, 내 약지에 천천히 끼웠다."),
        Say(Girlfriend, "이제 우리 진짜 가족이야. ♡"),
        Think("…반지가, 빠지지 않는다."),
    };

    public const string ProposalEndingLine = "평생, 영원히… 내 거야 ♡";
    public const string ProposalEndingTitle = "청혼 엔딩 : 해피 엔딩…?";
    public const string ProposalEndingSubtitle = "우리는 오래오래 행복하게 살았습니다. …아마도.";

    // ═══════════════════════════════════════════════════
    //  탈출 엔딩 (전화 부스 신고 → 다음 날 아침 경찰 도착) — 사용자 확인 2026-11-02
    //  이 대사 뒤 암전 → 여자친구 마지막 대사(대화창 위) → 엔딩 화면 (경찰차 불빛)
    // ═══════════════════════════════════════════════════

    public static List<DialogueLine> EscapeCutscene() => new List<DialogueLine>
    {
        Narrate(GameDifficulty.IsEasy ? "(딩동— 초인종이 울렸다.)" : "(딩동— 아침 일찍 초인종이 울렸다.)"),
        Say(Girlfriend, "…이 시간에 누구지?"),
        Say("경찰", "경찰입니다! 신고 받고 출동했습니다. 문 열어 주세요!"),
        Say(Girlfriend, "경찰…?"),
        Say(Girlfriend, "……"),
        Say(Girlfriend, "…자기야. 너야? 네가 부른 거야?"),
        Say(Player, "……"),
        Narrate("(대답 대신 고개를 숙였다.)"),
        Say(Girlfriend, "…그렇구나."),
        Say(Girlfriend, "괜찮아. 화 안 났어. 진짜야."),
        Narrate("(끼익— 문이 열리고, 경찰이 그녀의 손목에 수갑을 채웠다.)"),
        Narrate("(끌려 나가던 그녀가 문 앞에서 멈춰 돌아봤다.)"),
    };

    // 기믹 창을 열어 둔 채 10초 동안 아무것도 안 했을 때 (Stage1AntiIdle, 창을 닫고 실패 처리)
    public static List<DialogueLine> GimmickIdleClosed() => new List<DialogueLine>
    {
        Think("…너무 오래 망설였다. 이러다 들키겠어."),
    };

    // [이지] 하트가 1 깎였을 때 (EasyGiveUpPrompt) — [예]면 순응 엔딩, [아니요]면 계속
    public static List<DialogueLine> EasyGiveUpQuestion() => new List<DialogueLine>
    {
        Narrate("여기까지 하시겠습니까?"),
    };

    // [이지] 1일차에 현관문으로 나가려 하면 (Stage2HouseLink)
    public static List<DialogueLine> WalkFirstDayBlocked() => new List<DialogueLine>
    {
        Think("…아직 첫날이야. 지금 나가면 바로 들킬 것 같아."),
        Think("오늘은 집 안부터 살펴보자."),
    };

    // 하루 남은 시간이 1분 20초 이하일 때 현관문으로 나가려 하면 (Stage2HouseLink) — 하루 첫 번째 (사용자 대사 2026-11-03)
    // 여자친구 줄은 산책 들킴 그림으로 (DialoguePortrait.UseWalkCaughtForNextDialogue)
    public static List<DialogueLine> WalkTooLate() => new List<DialogueLine>
    {
        Narrate("산책을 나가려다가 멈췄다."),
        Think("그녀가 언젠가 말했던 게 떠올랐다."),
        Say(Girlfriend, "지금 나가면 나 화낼꺼야."),
        Say(Girlfriend, "잠자는 시간인데 밖에 나갈거야?"),
        Think("....."),
        Think("지금 나가면 자는 시간까지 돌아오지 못할 것 같다."),
    };

    // 같은 날 다시 현관문을 누르면 (산책 들킴 그림이 대사 위로 75%까지 서서히 나타남)
    public static List<DialogueLine> WalkTooLateAgain() => new List<DialogueLine>
    {
        Think("그녀의 모습이 아른거린다..."),
        Think("내일 나가자."),
    };

    // 산책에서 돌아왔는데 하루 남은 시간이 30초 미만일 때 (Stage2HouseLink, 뛰는 발소리와 함께)
    public static List<DialogueLine> WalkReturnHurry() => new List<DialogueLine>
    {
        Think("시간이 아슬아슬하다."),
        Think("얼른 정리하고 자러가자."),
    };

    // 10의 배수 일차 아침: 하트 +1 (DayEndReport)
    public static List<DialogueLine> DayHeal() => new List<DialogueLine>
    {
        Think("…오늘은 왠지 몸이 가볍다. 조금 더 버틸 수 있을 것 같아."),
        Narrate("[하트 하나가 회복됐다.]"),
    };

    public static List<DialogueLine> DayHealFull() => new List<DialogueLine>
    {
        Think("…오늘은 왠지 몸이 가볍다."),
    };

    public const string EscapeEndingLine = "금방 돌아올게. 기다려 줘, 자기야 ♡";
    public const string EscapeEndingTitle = "탈출 엔딩 : 문 밖의 아침";
    public const string EscapeEndingSubtitle = "드디어 그 집을 나왔다. …그런데 왜, 뒤를 돌아보게 되는 걸까.";

    // ═══════════════════════════════════════════════════
    //  12. 침실 단서 (BedroomClueStory) — 서랍 비밀번호 724 = 기념일 7일 · 사진 속 2 · 사랑해의 '사'
    // ═══════════════════════════════════════════════════

    public static readonly string[] BedroomCalendarOpening = { "벽에 걸린 달력이다.", "7일에 빨간 동그라미가 쳐져 있다." };
    public static readonly string[] BedroomCalendarFirst = { "동그라미 옆에 작게 적혀 있다. 「우리 기념일 ♡」" };
    public static readonly string[] BedroomCalendarRepeat = { "7일. 그녀가 정한 '우리 기념일'이다." };

    public static readonly string[] BedroomPhotoOpening = { "하얀 베개다.", "아래에 무언가 끼어 있는 것 같다." };
    public static readonly string[] BedroomPhotoFirst =
    {
        "베개 밑에서 사진 한 장을 발견했다.",
        "그녀가 볼을 붉히며 웃고 있는 사진이다.",
        "…뒤쪽 꼬마전구 줄이 이상한 모양으로 걸려 있다.",
    };
    public static readonly string[] BedroomPhotoRepeat = { "그녀의 사진. 뒤쪽 꼬마전구 모양이 묘하게 눈에 걸린다." };

    public static readonly string[] BedroomMemoOpening = { "화병 아래에 종이 한 장이 깔려 있다." };
    public static readonly string[] BedroomMemoFirst =
    {
        "그녀의 글씨다. 서랍 비밀번호를 순서대로 적어 둔 것 같다.",
        "기념일 날짜, 사진 속 숫자, 그리고… '사'랑해의 '사'?",
    };
    public static readonly string[] BedroomMemoRepeat = { "기념일 날짜, 사진 속 숫자, '사'랑해의 '사'. 순서대로라고 했지." };
}
