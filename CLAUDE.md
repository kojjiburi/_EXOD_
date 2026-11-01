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
  - 종이 `paper_a`~`paper_e` (퀵슬롯에 들고 있어도 됨, 단 틀렸을 때 초기화되면 사라지고 다시 주울 수 있게 됨)
- 단, **황금 열쇠 `golden_key`는 서랍 기믹으로 얻지만 점검 대상**입니다. (서랍에 다시 넣어야 함)

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
- **일차(DAY)는 1스테이지 `DayCycleController`(3분 타이머)만 올립니다.** 2스테이지에 있는 동안에도 1스테이지 3분 타이머는 초기화되지 않고 계속 흐릅니다.
- 2스테이지 시간은 `DayClock`(1분)이 따로 잽니다. 입장하자마자 시작, 멈추는 기준은 `DayCycleController`와 같음(대화·컷신·기절 중).
  - 남은 시간은 오른쪽 위 1스테이지 3분 타이머 바로 아래에 표시(2스테이지에서만 보임).
  - 1분이 다 되면 "돌아가자" 대사 후 1스테이지 복도로 돌아옵니다.

### 👉 2스테이지 관련 대사를 만들 때 (친구 작업)
**1스테이지 탈출(현관문을 나서는) 대사나 2스테이지 입장 컷신을 만들려고 하면, 2스테이지에서 1스테이지로 돌아오는 컷신도 같이 만들어 달라고 요청하세요.**
- 시간이 다 돼서 돌아올 때: 시간이 다 됐다며 돌아오라고 하는 대사
- 스스로 돌아올 때(Esc로 그만둠 등): "뭐야? 벌써 가게?" 같은 대사 후 돌아오는 대사
- 대사 위치: `Assets/EXODStage2Walk/Scenes/Stage2_Walk.unity`의 `WalkGameManager` (timeUpLines, quitLines 등)
- 입장 컷신 자리: `Stage2HouseLink.cs`의 `EnterCutsceneTestLines` (지금은 테스트 문구)
- 대사 내용은 친구 담당이므로 AI는 내용을 지어내지 말고, 만들어 달라고 전달만 합니다.

## 그 밖의 작업 규칙
- 작업이 끝날 때마다 GitHub `codex/exod-integration-2026-08-17` 브랜치에 올립니다. 올리기 전에 친구가 올린 것을 먼저 받아 합칩니다.
- 이미 있는 코드와 **내용이나 실행 결과가 겹쳐 보이면** 올리기 전에 사용자에게 보고하고 결정을 받습니다.
- 대답은 한국어로 합니다.
