# 친구용 아이템 제작 안내

친구가 수정할 범위는 아이템 데이터와 월드 프리팹뿐입니다. 퀵슬롯 스크립트와 Stage1의 QuickSlot_1 오브젝트는 수정하지 않습니다.

## 1. ItemData 만들기

Project 창에서 `Create > EXOD123 > Items > Item Data`를 선택합니다.

- `Item Id`: 프로젝트 전체에서 중복되지 않는 영문 ID. 예: `golden_key`
- `Display Name`: 게임에 표시할 이름
- `Icon`: 퀵슬롯용 Sprite
- `Consume On Successful Use`: 성공적으로 사용했을 때 사라져야 하면 체크

`acquiredSequence`와 슬롯 번호는 퀵슬롯 시스템이 자동 관리하므로 아이템 코드에 추가하지 않습니다.

## 2. 월드 아이템 프리팹 만들기

프리팹에 다음 컴포넌트를 붙입니다.

- SpriteRenderer
- Collider2D
- WorldItemPickup

WorldItemPickup의 `Item Data`에 위에서 만든 에셋을 연결합니다.

## 3. 아이템을 받는 대상 만들기

문이나 화분처럼 아이템을 받을 오브젝트에는 Collider2D와 ItemInteractionTarget을 붙입니다.

- `Accept Any Item`: 어떤 아이템이든 받을 때만 체크
- `Accepted Items`: 허용할 ItemData 목록
- `On Item Accepted`: 성공 시 실행할 기능
- `On Item Rejected`: 잘못된 아이템일 때 실행할 기능

특수한 동작이 필요한 경우 친구는 문 열기, 식물 성장 같은 효과 컴포넌트만 작성하고 `On Item Accepted`에 연결합니다.
