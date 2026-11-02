# EXOD 프로젝트 — AI 작업자(Claude·Codex 등) 필독 메모

작업 중인 유니티 프로젝트: `2026-09-01-유니티통합프로젝트/` (Unity 6000.0.48f1, 씬 `Assets/Scenes/SampleScene.unity`)

## ⚠️ 아이템을 새로 추가할 때 반드시 할 일

이 게임은 **일차가 끝날 때 모든 아이템이 제자리에 있는지 점검**합니다.
하나라도 틀리면 하트(HP)가 1 줄고, 모든 아이템이 처음 자리로 초기화됩니다.

- 점검 코드: `Assets/EXOD/TeamMerge/Code/Items/ItemPositionManager.cs`
  - `CheckItemsOnDayEnd()` — 일차 종료 점검 (틀리면 `HeartManager.HPM()`으로 하트 감소)
  - `ResetItemsForNewDay()` — 틀렸을 때 모든 아이템을 처음 자리로 되돌림
  - `GetMisplacedInventoryItemIds()` — **퀵슬롯에 들고 있는 아이템도 "제자리 아님"으로 셈**
- 일차 진행: `Assets/EXOD/DayCycle/DayCycleController.cs` (3분 경과 또는 침실 침대 클릭 → `GManager.NextDay()`)

**규칙**
- 모든 아이템은 일차가 끝나면 점검 대상입니다. (맵의 처음 자리에 있어야 함, 퀵슬롯에 들고 있으면 틀림)
- **예외**: 아래 아이템은 일차 끝 점검에서 **점검하지 않습니다**.
  - 기믹을 진행해서 얻는 아이템 (예: 곰돌이 보답 `fridge_lock_note`)
  - 사용하면 사라지는 아이템 (예: 곰돌이 팔다리 `bear_left_arm` 등)
  - 청혼반지 `proposal_ring` (화장실 금고를 열면 안에 있음, 주우면 바로 청혼 엔딩)
  - 종이 `paper_a`~`paper_e` (퀵슬롯에 들고 있어도 됨, 단 틀렸을 때 초기화되면 사라지고 다시 주울 수 있게 됨)
- 단, **황금 열쇠 `golden_key`는 서랍 기믹으로 얻지만 점검·초기화 대상**입니다. (서랍에 다시 넣어야 함, 틀리면 서랍 안으로 돌아감)
- 위 예외를 뺀 **퀵슬롯에 얻을 수 있는 모든 아이템**은 점검·초기화 대상입니다. 처음 자리는 `WorldItemPickup`이 시작할 때 자동으로 저장됩니다.
  - 맵에 `WorldItemPickup`으로 놓지 않고 코드로 퀵슬롯에 바로 넣는 아이템은 처음 자리가 없어서 초기화가 안 됩니다(콘솔 경고). 이런 아이템은 화장실 선반(`RestroomGimmick`)·서랍(`PlayerSystemsSceneBridge`)처럼 처음 자리를 따로 등록해 주세요.

예외 목록 위치 (`ItemPositionManager.cs`):
- `AlwaysAllowedInInventoryItemIds` — 퀵슬롯에 들고 있어도 되는 아이템 (코드 상수)
- `ignoredItemIds` / `inventoryAllowedItemIds` — 인스펙터에서 추가하는 예외 목록
- `paperItemIdPrefix` (`paper_`) — 종이 처리

### 👉 그래서 AI 작업자는
**새 아이템(ItemData)을 추가하거나 아이템을 주는 기믹을 만들 때, 작업 전에 반드시 사용자에게 물어보세요:**

> "이 아이템은 일차 끝 점검(제자리 정리)에서 **예외**인가요?
> (기믹을 진행해서 얻는 아이템이거나, 사용하면 사라지는 아이템인가요?)"

- 예외라면 → `AlwaysAllowedInInventoryItemIds`에 아이템 ID를 추가
- 점검 대상이라면 → 맵에 `WorldItemPickup`으로 놓아서 `ItemPositionManager`에 처음 자리가 등록되게 하기

## 2스테이지(산책) 연결 구조

- 1스테이지 복도 현관문을 나서면 → 입장 컷신(지금은 테스트 문구) → 2스테이지 산책 씬 `Stage2_Walk`가 **1스테이지 씬 위에 Additive로** 열립니다.
  - 연결 코드: `Assets/EXOD/Stage2Link/Stage2HouseLink.cs` (입장/복귀, 대화창·퀵슬롯 연결)
  - 산책 코드: `Assets/EXODStage2Walk/` (2스테이지 원본 프로젝트에서 가져옴, `BedNextDay`는 삭제)
- **일차(DAY)는 1스테이지 `DayCycleController`(3분 타이머)만 올립니다.** 2스테이지에 있는 동안 1스테이지 3분 타이머는 멈추고, 산책이 끝나 돌아오면 **성공/실패 상관없이 1분을 뺍니다** (`Stage2HouseLink.WalkTimeCostSeconds`). 들켰을 때 시간은 따로 더 빼지 않고, 대신 **하트(HP)가 1 줄어듭니다** (`Stage2HouseLink.ReturnRoutine`, HP 0이면 게임 오버).
- 산책 자동 이동 속도는 `DayClock` 1분 안에 1번 왕복(`WalkGameManager.roundTripsRequired`)이 끝나도록 자동 계산됩니다 (`WalkPlayerMover.matchDayClockDuration`). 양 끝에서 2초 멈추고, 그녀가 뒤돌아보는 동안에도 멈추며, 멈춘 시간은 남은 시간에 맞춰 속도로 따라잡습니다.
- 2스테이지 시간은 `DayClock`(1분)이 따로 잽니다. 입장하자마자 시작, 멈추는 기준은 `DayCycleController`와 같음(대화·컷신·기절 중).
  - 남은 시간은 오른쪽 위 1스테이지 3분 타이머 바로 아래에 표시(2스테이지에서만 보임).
  - 1분이 다 되면 "돌아가자" 대사 후 1스테이지 복도로 돌아옵니다.

### 👉 2스테이지 관련 대사를 만들 때 (친구 작업)
**1스테이지 탈출(현관문을 나서는) 대사나 2스테이지 입장 컷신을 만들려고 하면, 2스테이지에서 1스테이지로 돌아오는 컷신도 같이 만들어 달라고 요청하세요.**
- 시간이 다 돼서 돌아올 때: 시간이 다 됐다며 돌아오라고 하는 대사
- 스스로 돌아올 때(Esc로 그만둠 등): "뭐야? 벌써 가게?" 같은 대사 후 돌아오는 대사
- 대사 위치: `Assets/EXODStage2Walk/Scenes/Stage2_Walk.unity`의 `WalkGameManager` (timeUpLines, quitLines 등)
- 입장 컷신: `Stage1Dialogues.cs`의 `Stage2EnterFirst`(처음) / `Stage2EnterAgain`(두 번째부터) — `Stage2HouseLink`가 재생
- 대사 내용은 친구 담당이므로 AI는 내용을 지어내지 말고, 만들어 달라고 전달만 합니다.

## 그 밖의 작업 규칙
- 작업이 끝날 때마다 GitHub `codex/exod-integration-2026-08-17` 브랜치에 올립니다. 올리기 전에 친구가 올린 것을 먼저 받아 합칩니다.
- 이미 있는 코드와 **내용이나 실행 결과가 겹쳐 보이면** 올리기 전에 사용자에게 보고하고 결정을 받습니다.
- 대답은 한국어로 합니다.

## 엔딩 연결 자리
- **청혼 엔딩**: 화장실 금고(RRSafe_block이 치워진 뒤) 안의 청혼반지를 주우면 `ProposalEnding.Trigger()`가 호출됩니다 (`Assets/EXOD/Endings/ProposalEnding.cs`).
  - 엔딩 연출은 nicezzomaru 담당: `ProposalEnding.Requested` 이벤트에 연출을 연결하세요. 연결 전에는 임시 대사만 나옵니다.
## 위험도(여자친구 등장) 시스템
- 코드: `Assets/EXOD/DangerLevel/DangerLevel.cs` (규칙 전체가 파일 맨 위 주석에 있음). 아이콘은 팀 HUD의 `Yan` 아이콘 자리를 바꿔 씀 (`Yan.cs`의 자체 로직은 안 씀).
- 기믹을 새로 만들면: 성공 시 `DangerLevel.GimmickSolved()`(+5), 실패 시 `DangerLevel.GimmickFailed()`(+10), 기믹 창을 열기 전 `if (DangerLevel.WarnBeforeGimmick()) return;` 을 넣어 주세요.
- 등장·걸림·튜토리얼 대사는 임시라 DangerLevel.cs 위쪽 Lines 상수를 바꾸면 됩니다.
- 일차가 끝나면 페이드 뒤에 제자리에 없던 물건 이름을 대사로 알려 줌 (`ItemPositionManager.LastDayEndMisplacedNames`).
