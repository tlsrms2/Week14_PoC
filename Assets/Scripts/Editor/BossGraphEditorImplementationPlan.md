# 데이터 주도형 범용 보스 그래프 에디터 구현 계획

## 0. 전제
- 1차 대상은 `HogBossAI`만 본다.
- `DronePilot` 계열은 확정 전까지 범위에서 제외한다.
- 현재 프리팹에 들어간 `HogBossAI` 직렬화 값은 보존 요구가 없다.
- 목표는 단순 커스텀 인스펙터가 아니라, 런타임 보스 로직이 그래프/SO 데이터를 읽어 동작하는 구조다.

## 1. 현재 구조의 병목
- `HogBossAI`의 레거시 `pattern1~7`, `projectiles`, `phasePatterns`, `PatternKind switch`는 제거 완료됐다.
- `HogPatternXRunner`, `HogPatternContext`, `HogPatternSelector`, `HogBossAI.Settings`도 제거 완료됐다.
- 남은 병목은 HogBossAI가 `BossGraphAsset`을 직접 참조하는 첫 보스 구현이라는 점과, 실제 보스 프리팹/씬 데이터가 그래프 SO로 얼마나 이관됐는지 검증해야 한다는 점이다.
- 따라서 다음 작업은 새 레거시 필드를 추가하지 않고 범용 `BossAction`과 GraphAsset 참조 설정만 확장하는 방향으로 진행한다.

## 2. 목표 런타임 구조
### 2.1 BossGraphAsset
- 보스 하나의 전체 상태 그래프를 담는 최상위 SO.
- 포함 데이터:
  - 시작 노드 ID
  - 상태 노드 목록
  - 트랜지션 목록
  - 공통 보스 설정 참조
  - 그래프 에디터 좌표/접힘 상태 같은 에디터 전용 메타데이터
- 구현 상태:
  - `CombatEffectData`, `BossColorSettings`는 `BossGraphAsset.references`에서 관리한다.
  - 씬 오브젝트와 Boss Combat UI 참조는 에셋이 아닌 보스 컴포넌트의 `참조` 탭에서 관리한다.

### 2.2 BossStateNode
- `Idle`, `Phase1`, `Phase2`, `Groggy`, `Dead` 같은 큰 상태를 표현한다.
- 각 노드는 실행할 `AttackSequenceAsset` 목록과 선택 정책을 가진다.
- 선택 정책:
  - 순차
  - 랜덤
  - 직전 반복 방지
  - 가중치 랜덤

### 2.3 BossTransition
- 노드 간 이동 조건을 표현한다.
- 1차 조건:
  - HP 비율
  - 목숨/페이즈 인덱스
  - 플레이어 감지 여부
  - 현재 시퀀스 종료 여부
  - 그로기 진입/해제

### 2.4 AttackSequenceAsset
- 하나의 공격 패턴 또는 행동 묶음을 재사용 가능한 SO로 저장한다.
- `BossAction` 목록을 순서대로 가진다.
- 1차 구현은 `SerializeReference` 기반 액션 리스트를 사용한다.

### 2.5 BossAction
- 모든 행동 블록의 추상 기반 타입.
- 공통 실행 형태:
  - `IEnumerator Execute(BossActionContext context)`
- 1차 액션:
  - `WaitAction`
  - `MoveTowardPlayerAction`
  - `FireProjectileAction`
  - `RadialFireAction`
  - `SweepFireAction`
  - `PlayAnimationAction`
  - `SetTelegraphAction`
  - `CustomEventAction`
  - `SpawnPrefabAction`

### 2.6 BossActionContext
- 액션이 MonoBehaviour 구현 세부사항에 직접 닿지 않도록 감싸는 런타임 컨텍스트.
- 담당:
  - 보스 Transform/Rigidbody 접근
  - 플레이어 위치 조회
  - 투사체 발사
  - 이펙트/SFX 실행
  - 애니메이션 이벤트 대기
  - 실행 중단/처형 연출 일시정지 처리

## 3. Hog 기준 이관 전략
### 3.1 1단계: Legacy 브릿지 제거 완료
- `RunHogLegacyPatternAction`, `HogLegacyPatternKind`, `Hog Legacy Graph Set` 생성 메뉴는 제거한다.
- `BossActionContext`는 Hog 전용 레거시 콜백을 갖지 않는다.
- `HogBossAI`는 `BossGraphAsset`이 있을 때만 그래프를 실행하며, 그래프가 비어 있으면 레거시 패턴으로 fallback하지 않는다.
- 기존 `HogPatternXRunner` 계열은 레거시 소스 정리 대상이며, 신규 그래프는 범용 `BossAction` 블록만 사용한다.

### 3.2 2단계: 패턴별 액션 블록화
- 완료: Hog 이관용 범용 액션들의 필수 참조/문자열/리스트 검증을 `BossGraphValidationUtility`에 보강했다.
- 기존 패턴을 하나씩 액션 블록으로 분해한다.
- 우선순위:
  1. `Pattern1`: 원형/각도 증가 발사
  2. `Pattern4/6`: 슬램 + 전방위 웨이브
  3. `Pattern5`: 대기 + 연속 발사
  4. `Pattern7`: 텔레그래프 + 부채꼴 + 보조 발사
  5. `Pattern2/3`: 이동/특수탄 포함 패턴
- 각 패턴이 완전히 액션화되면 해당 Legacy 액션 의존을 제거한다.

### 3.3 3단계: HogBossAI 슬림화
- `HogBossAI`는 그래프 실행용 보스 컨트롤러가 된다.
- 제거 완료:
  - `pattern1~7`
  - `phasePatterns`
  - `PatternKind switch`
  - Hog 전용 커스텀 인스펙터의 패턴 탭
  - `HogBossAI.Settings`
  - `HogPatternXRunner`
  - `HogPatternContext`
  - `HogPatternSelector`
- 유지 대상:
  - 보스 생명/그로기/처형/광폭화 등 공통 상태 처리
  - 그래프 실행을 위한 `bossGraph` 참조와 `BossGraphRunner` 연결

## 4. 에디터 구현 순서
### 4.1 기반 SO 인스펙터
- `BossGraphAsset`, `AttackSequenceAsset` 생성 메뉴를 먼저 만든다.
- GraphView 없이 기본 인스펙터와 `ReorderableList`로 데이터가 저장/실행되는지 검증한다.

### 4.2 Sequence Editor
- `AttackSequenceAsset` 전용 에디터를 만든다.
- 액션 추가/삭제/순서 변경을 지원한다.
- 액션별 필드를 접이식 UI로 표시한다.

### 4.3 Boss Graph Editor Window
- `EditorWindow + UI Toolkit + GraphView` 기반 창을 만든다.
- 기능:
  - 그래프 에셋 열기
  - 상태 노드 생성/삭제
  - 노드 위치 저장
  - 트랜지션 연결/삭제
  - 노드별 시퀀스 목록 편집
  - 완료: 선택 노드에서 `AttackSequenceAsset` 생성 후 즉시 연결 버튼 추가
  - 완료: 선택 정책/가중치 편집 UI와 WeightedRandom/RandomNoRepeat 검증 추가

## 5. 애니메이션 이벤트 브릿지
- `BossAnimationEventBridge` 컴포넌트를 둔다.
- Animation Event는 문자열 ID를 브릿지에 전달한다.
- 완료: `BossAnimationEventBridge`, `WaitForAnimationEventAction`, 이벤트 ID 검증 연결.
- `BossActionContext`는 특정 이벤트 ID가 올 때까지 대기할 수 있어야 한다.
- 시간 기반 액션과 이벤트 기반 액션을 모두 허용한다.

## 6. 핫 리로드 기준
- 플레이 모드 중 SO 값을 바꾸면 다음 실행 액션부터 새 값을 읽는다.
- 이미 실행 중인 액션은 기본적으로 중간 변경을 강제 반영하지 않는다.
- 탄속/탄 수/쿨타임처럼 즉시 반영 가능한 값은 액션별로 명시적으로 지원한다.

## 7. 마이그레이션 완료 기준
- HogBoss가 `BossGraphAsset` 참조 하나로 전투 패턴을 실행한다.
- 페이즈별 패턴 선택이 그래프 데이터로 결정된다.
- 하나의 `AttackSequenceAsset`을 여러 PhaseNode에서 재사용할 수 있다.
- 신규 패턴 추가 시 `HogBossAI`에 `pattern8` 필드를 추가하지 않는다.
- Graph Editor에서 저장한 노드/엣지/시퀀스가 플레이 모드 실행에 반영된다.

## 8. 권장 작업 순서
1. `BossGraphAsset`, `BossStateNode`, `BossTransition`, `AttackSequenceAsset`, `BossAction` 타입 추가.
2. `BossActionContext`와 `BossGraphRunner` 추가.
3. `HogBossAI`에 그래프 전용 실행 모드 추가.
4. 레거시 Hog 액션/생성 메뉴 제거.
5. 기본 인스펙터/ReorderableList로 그래프 데이터 실행 검증.
6. `AttackSequenceAsset` 액션 편집기 제작.
7. GraphView 기반 `BossGraphEditorWindow` 제작.
8. Hog 패턴을 일반 액션 블록으로 순차 이관.
9. Animation Event Bridge 추가.

## 9. 첫 구현 목표
- 그래프 창보다 먼저 다음 수직 슬라이스를 완성한다.
- `BossGraphAsset` 하나를 만들고, Phase 1 노드에 `AttackSequenceAsset`을 연결한다.
- 그 시퀀스에는 범용 projectile/move/wait 액션만 넣는다.
- Play 시 HogBoss가 레거시 fallback 없이 그래프 데이터 기반 액션만 실행하면 성공이다.
