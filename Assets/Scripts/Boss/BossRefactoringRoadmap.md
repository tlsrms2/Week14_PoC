# 보스 확장성 리팩토링 로드맵

## 목적

이 문서는 보스 러쉬 게임 구조를 전제로, 새로운 보스를 계속 추가할 수 있도록 `Assets/Scripts/Boss` 하위 코드를 어떤 방향으로 리팩토링해야 하는지 정리한다.

핵심 목표는 다음과 같다.

- 새 보스가 `BossAI`의 공통 생명주기, HP, 처형, 페이즈, 광폭화, 사망 처리를 재사용할 수 있게 한다.
- 보스별 핵심 스크립트가 지나치게 커지는 것을 막는다.
- 보스마다 패턴 실행 루프, 대기, 회복, 투사체 생성 코드를 복붙하지 않게 한다.
- 보스별 고유 패턴의 개성은 유지하고, 억지 공통화로 코드가 더 복잡해지는 일을 피한다.
- 보스 러쉬같은 아웃루프 부분은 다른 개발자가 진행하므로 원활한 협업이 가능하도록 로직을 작성해야 한다.

## 현재 구조 요약

### 공통 코어

- `Core/BossAI.cs`
  - 모든 보스의 기반 클래스다.
  - HP, 목숨/페이즈, 처형 가능 상태, 광폭화, 사망 처리, UI, 공통 투사체 생성 진입점을 가진다.
  - `OnBossTick`, `CancelBossAction`, `OnBossPhaseChanged`, `OnBossDied` 같은 훅으로 개별 보스 구현을 받는다.

- `Core/BossStateMachine.cs`
  - 보스 상태를 `WaitingForPlayer`, `Combat`, `HpEmpty`, `PhaseTransition`, `ExecutionPaused`, `ExecutionLocked`, `Dead`로 관리한다.

- `Core/BossStates.cs`
  - 상태별 Tick 동작을 가진다.
  - `BossHpEmptyState`가 HP 0 이후 처형 가능 창과 자동 회복을 관리한다.

- `Core/BossPhaseController.cs`
  - 목숨/페이즈, 페이즈 전환 대기, 광폭화 타이머, 플레이어 최대 탄환 감소를 관리한다.

### Hog 보스

- `Hog/HogBossAI.cs`
  - `BossAI`를 상속한다.
  - 현재는 `RunPatternLoop()`가 패턴 선택, 패턴 1회 실행, 광폭화 적용, 회복 대기, 다음 패턴 선택을 반복한다.
  - `RunPattern()`은 `PatternKind`에 따라 `RunPattern1~7()`만 실행하는 형태로 정리되어 있다.
  - 패턴 실행 코드는 아직 대부분 `HogBossAI` 안에 남아 있다.

- `Hog` 하위 helper들
  - `HogPatternSelector`
  - `HogPatternRecoveryController`
  - `HogPatternMovement`
  - `HogProjectileEmitter`
  - `HogPatternShotEmitter`
  - `HogProjectileOriginResolver`
  - `HogPatternPreviewPresenter`
  - `HogPattern7GuideView`
  - `HogBodyRootSlamController`

이 helper들은 Hog 유지보수에는 도움이 되지만, 대부분 타입이 `HogBossAI.PatternKind`, `HogBossAI.ProjectileSettings`, `HogBossAI.PatternXSettings`에 묶여 있어 새 보스가 직접 재사용하기는 어렵다.

### Drone 보스

- `Drone/DronePilot.cs`
  - `BossAI`를 상속하는 두 번째 보스 구현이다.
  - 보스 본체 패턴과 드론 패턴을 별도 코루틴으로 돌린다.
  - 아직 `RunBossPattern()`과 `RunDronePattern()` 끝에서 다음 `StartCoroutine()`을 다시 시작하는 구조가 남아 있다.
  - 이 구조는 `HogBossAI.RunPatternLoop()` 방식보다 흐름 추적과 취소 안정성이 떨어진다.

## 핵심 판단

새 보스의 핵심 스크립트가 모든 패턴의 정보를 아는 것은 맞다.

다만 핵심 스크립트가 모든 패턴의 실행 코드까지 전부 가지는 구조는 보스가 늘수록 유지보수가 어려워진다.

권장 책임 분리는 다음과 같다.

```text
BossAI
  공통 생명주기, HP, 페이즈, 처형, 광폭화, 사망, 공통 투사체 생성

HogBossAI / DronePilot / NewBossAI
  보스 전용 설정
  패턴 목록과 선택 규칙
  보스 전용 공통 참조
  패턴 runner 호출

HogPatternXRunner / DronePatternXRunner
  해당 패턴의 실제 실행 순서
  발사 타이밍
  이동, 차징, 연출 호출
```

중요한 점은 패턴 자체를 너무 일찍 공통화하지 않는 것이다.

패턴 내용은 보스의 개성에 가까우므로, 새 보스 확장성을 위해 먼저 공통화해야 할 대상은 패턴 구현이 아니라 아래 항목이다.

1. 패턴 실행 루프
2. 일시정지 대기
3. 패턴 회복 대기
4. 공통 투사체 설정
5. 공통 투사체 발사
6. 보스 러쉬 진행 제어

## 현재 구조로 가능한 것과 부족한 것

### 가능한 것

- `BossAI`를 상속해서 새 보스를 직접 구현할 수 있다.
- HP, 페이즈, 처형 가능 상태, 광폭화, 사망 이벤트는 공통으로 재사용 가능하다.
- Hog, Drone처럼 전혀 다른 보스 기믹을 구현할 수 있다.

### 부족한 것

- 보스 하나가 죽으면 바로 승리 UI가 뜨는 구조가 남아 있다.
  - `BossAI.Defeated`를 UI가 직접 승리 처리에 사용한다.
  - 보스 러쉬에서는 중간 보스 사망과 최종 보스 사망을 구분해야 한다.

- 보스별 패턴 루프 구조가 통일되어 있지 않다.
  - Hog는 `RunPatternLoop()` 구조다.
  - Drone은 패턴 코루틴 안에서 다음 코루틴을 다시 시작한다.

- 투사체 설정이 보스별로 중복되어 있다.
  - `HogBossAI.ProjectileSettings`
  - `DronePilot.ProjectileSettings`

- Hog helper들이 Hog 타입에 강하게 묶여 있다.
  - 새 보스에서 재사용하려면 공통 타입으로 분리하거나, 각 보스 전용 helper로 유지해야 한다.

## 리팩토링 원칙

### 해야 할 것

- 먼저 Hog와 Drone의 패턴 실행 흐름을 같은 모양으로 맞춘다.
- 두 보스 이상에서 실제로 중복되는 로직만 `Core`로 이동한다.
- 새 공통 클래스는 작게 시작한다.
- 코루틴 취소 시 `CancelBossAction()`에서 모든 활성 코루틴과 보스 전용 임시 상태가 정리되어야 한다.
- 각 단계마다 Unity Editor에서 한 보스씩 debug 고정 패턴으로 확인한다.

### 피해야 할 것

- Hog 패턴을 바로 전부 공통 패턴 클래스로 만들지 않는다.
- `IBossPattern` 같은 큰 인터페이스를 먼저 만들고 거기에 코드를 끼워 맞추지 않는다.
- `ScriptableObject` 패턴 데이터화를 초반에 진행하지 않는다.
- Inspector 직렬화 필드 이름을 한 번에 크게 바꾸지 않는다.
- 보스별 고유 연출과 공통 로직을 억지로 한 클래스로 합치지 않는다.

## 추천 최종 구조

```text
Assets/Scripts/Boss
  Core
    BossAI.cs
    BossStateMachine.cs
    BossStates.cs
    BossPhaseController.cs
    BossDeathSequencePlayer.cs
    BossProjectileTracker.cs
    BossPatternMovement.cs
    BossPatternRecovery.cs
    BossProjectileSettings.cs
    BossProjectileEmitter.cs

  Hog
    HogBossAI.cs
    HogPatternSelector.cs
    HogPatternContext.cs
    Patterns
      HogPattern1Runner.cs
      HogPattern2Runner.cs
      HogPattern3Runner.cs
      HogPattern4Runner.cs
      HogPattern5Runner.cs
      HogPattern7Runner.cs

  Drone
    DronePilot.cs
    DronePatternSelector.cs
    DronePatternContext.cs
    Patterns
      DroneBossBurstRunner.cs
      DroneSummonRunner.cs
      DronePattern1Runner.cs
      DronePattern2Runner.cs
      DronePattern3Runner.cs
      DronePattern4Runner.cs
      DronePattern5Runner.cs

  Rush
    BossRushController.cs
```

위 구조는 최종 목표이며, 한 번에 전부 만들면 안 된다.

## 단계별 계획

## 1단계: DronePilot 패턴 루프 정리

### 목표

`DronePilot`의 패턴 코루틴 구조를 `HogBossAI`와 같은 방식으로 맞춘다.

현재 Drone은 다음 형태다.

```text
RunBossPattern()
  패턴 실행
  광폭화 적용
  회복 대기
  StartCoroutine(RunBossPattern(next))

RunDronePattern()
  패턴 실행
  광폭화 적용
  회복 대기
  StartCoroutine(RunDronePattern(next))
```

권장 형태는 다음이다.

```text
OnBossTick()
  bossPatternRoutine == null이면 RunBossPatternLoop() 시작
  dronePatternRoutine == null이면 RunDronePatternLoop() 시작

RunBossPatternLoop()
  while true
    RunBossPattern(pattern)
    자동 소환 처리
    ApplyPendingEnrageIfAny()
    WaitBossPatternRecovery()
    pattern = SelectBossPattern()

RunDronePatternLoop()
  while true
    RunDronePattern(pattern)
    ApplyPendingEnrageIfAny()
    WaitDronePatternRecovery()
    pattern = SelectDronePattern()
```

### 주의점

- 보스 본체 루프와 드론 루프가 둘 다 `ApplyPendingEnrageIfAny()`를 호출하면 광폭화 연출 타이밍이 중복될 수 있다.
- 가능하면 광폭화 적용은 보스 본체 루프 한 곳에서만 처리하는 방향을 검토한다.
- 기존 동작 보존을 우선한다면 첫 단계에서는 호출 위치만 유지하고, 중복 문제는 별도 단계에서 정리한다.

### 확인 방법

- Drone 보스 전투 진입 후 보스 본체 패턴이 반복되는지 확인한다.
- 드론 패턴이 반복되는지 확인한다.
- 처형 가능 상태에서 보스/드론 패턴 코루틴이 멈추는지 확인한다.
- 보스 사망 시 드론이 정리되는지 확인한다.

## 2단계: 공통 대기 로직 분리

### 목표

Hog와 Drone에 흩어진 대기 코루틴을 `Core/BossPatternMovement.cs` 또는 `Core/BossPatternWaiter.cs`로 이동한다.

공통 후보는 다음이다.

```text
WaitSeconds(seconds, onTick, isExecutionPaused, stop)
WaitStoppedSeconds(seconds, isExecutionPaused, stop)
WaitWhileExecutionPaused(isExecutionPaused, stop)
```

### 현재 중복

- Hog
  - `HogPatternMovement.WaitSeconds`
  - `HogPatternMovement.WaitWhileExecutionPaused`

- Drone
  - `WaitPatternSeconds`
  - `WaitStoppedSeconds`
  - `WaitDronePatternSeconds`
  - `WaitWhileExecutionPaused`

### 권장 구현

처음부터 거대한 패턴 실행 클래스를 만들지 말고, 대기 helper만 `Core`로 옮긴다.

```text
Core/BossPatternMovement.cs
  WaitSeconds()
  WaitStoppedSeconds()
  WaitWhileExecutionPaused()
  MoveTowardTarget()
```

`MoveTowardTarget()`은 Hog와 Drone 양쪽에서 실제 재사용될 때만 추가한다.

### 확인 방법

- Hog 패턴 사이 대기와 Pattern2/5/7 내부 대기가 정상인지 확인한다.
- Drone 패턴 회복 대기와 내부 fire interval이 정상인지 확인한다.
- 실행 연출 중 `IsExecutionPaused`일 때 보스가 정지하는지 확인한다.

## 3단계: 공통 회복 시간 로직 분리

### 목표

패턴 종료 후 다음 패턴 전까지 쉬는 시간을 공통화한다.

현재 후보는 다음이다.

- `HogPatternRecoveryController.GetRecoverySeconds`
- `DronePilot.GetPatternRecoverySeconds`

### 권장 파일

```text
Core/BossPatternRecovery.cs
```

권장 역할은 작게 유지한다.

```text
GetRecoverySeconds(min, max)
RunRecovery(duration, onTick, isExecutionPaused, stop)
```

Hog의 다음 패턴 preview, FirePoint telegraph는 Hog 전용 기능이므로 처음부터 `Core`에 넣지 않는다.

### 확인 방법

- Hog 패턴 종료 후 preview와 FirePoint telegraph가 유지되는지 확인한다.
- Drone 패턴 종료 후 회복 시간이 기존 범위로 동작하는지 확인한다.

## 4단계: 공통 패턴 루프 추출 검토

### 목표

Hog와 Drone의 루프 구조가 충분히 비슷해진 뒤, 새 보스가 루프를 복붙하지 않게 공통화한다.

### 전제 조건

이 단계는 반드시 1~3단계 이후에 진행한다.

먼저 두 보스가 아래 형태를 가져야 한다.

```text
SelectPattern()
RunPattern(pattern)
ApplyPendingEnrageIfAny()
GetRecoverySeconds()
RunRecovery()
```

### 권장 파일

```text
Core/BossPatternLoop.cs
```

### 가능한 형태

초기 버전은 generic을 쓰지 않고 delegate 기반으로 작게 시작한다.

```text
Run(
  selectPattern,
  runPattern,
  applyPendingEnrage,
  runRecovery,
  onPatternFinished)
```

다만 `PatternKind` 타입이 보스마다 다르므로, 바로 공통화가 어색하면 이 단계는 보류해도 된다.

### 판단 기준

다음 조건을 만족하면 공통화한다.

- Hog와 Drone 모두 동일한 루프 형태를 가진다.
- 새 보스 구현 시 루프 코드가 거의 그대로 복붙될 가능성이 높다.
- delegate 전달이 5개 이하로 유지된다.

다음 조건이면 보류한다.

- 보스마다 루프가 너무 다르다.
- delegate가 지나치게 많아진다.
- 공통 루프를 읽는 것보다 각 보스 루프를 읽는 편이 더 명확하다.

## 5단계: 공통 투사체 설정 분리

### 목표

보스마다 중복되는 투사체 설정을 공통 타입으로 분리한다.

현재 중복 타입은 다음이다.

- `HogBossAI.ProjectileSettings`
- `DronePilot.ProjectileSettings`

### 권장 파일

```text
Core/BossProjectileSettings.cs
Core/BossProjectileEmitter.cs
```

### 공통 필드 후보

```text
EnemyProjectile prefab
int bulletDamage
float chargeSeconds
float chargeDriftSpeed
bool aimAtPlayerWhileCharging
bool aimAtPlayerOnLaunch
float speed
float lifetime
float radius
Color chargingColor
Color launchedColor
float trailSeconds
float trailWidthMultiplier
bool homingEnabled
float homingSeconds
float homingTurnDegreesPerSecond
```

### 주의점

- Inspector 직렬화가 깨질 수 있으므로 가장 위험한 단계다.
- 기존 prefab에 저장된 필드 값을 유지해야 한다.
- `FormerlySerializedAs`를 적극적으로 사용한다.
- 한 번에 Hog와 Drone을 모두 바꾸지 말고 한 보스씩 바꾼다.

### 추천 순서

1. `BossProjectileSettings`를 새로 추가한다.
2. `DronePilot.ProjectileSettings`와 동일한 필드부터 맞춘다.
3. Drone에서 먼저 사용해 본다.
4. 정상 확인 후 Hog를 전환한다.
5. Hog 전용 필드는 별도 wrapper 또는 패턴 설정에 남긴다.

### 확인 방법

- 기존 prefab의 투사체 설정 값이 Inspector에서 유지되는지 확인한다.
- 일반탄, 특수탄, 유도탄, 차징탄이 기존과 같은 색/속도/수명으로 발사되는지 확인한다.

## 6단계: Hog 패턴 runner 분리

### 목표

`HogBossAI`가 너무 커지는 것을 막기 위해 패턴 실행 코드를 클래스별로 이동한다.

이 단계는 새 보스 확장성보다 Hog 유지보수성을 위한 단계다.

### 권장 순서

1. `RunPattern4Like()` 분리
   - `pattern4`, `pattern6`이 같은 구조를 공유한다.
   - `settings`, `patternKind`를 이미 인자로 받으므로 가장 안전하다.

2. `RunPattern5()` 분리
   - 차징, FirePoint 회전, 미니건 발사가 비교적 독립적이다.

3. `RunPattern7()` 분리
   - Pattern5와 비슷하지만 guide line 의존성이 있으므로 5 이후가 좋다.

4. `RunPattern2()` 분리
   - Volley 구조는 단순하지만 이동 지속 로직과 발사 helper가 엮여 있다.

5. `RunPattern1()`, `RunPattern3()` 분리
   - Pattern1은 추격 이동과 랜덤 각도, preview가 섞여 있다.
   - Pattern3은 `EnemyProjectile` charge 설정이 길어 분리 비용이 크다.

### 권장 파일 구조

```text
Hog/Patterns/HogPattern4Runner.cs
Hog/Patterns/HogPattern5Runner.cs
Hog/Patterns/HogPattern7Runner.cs
```

처음부터 1~7을 모두 만들지 않는다.

### Context 도입 기준

runner에 전달해야 하는 delegate가 너무 많아지면 `HogPatternContext`를 만든다.

```text
HogPatternContext
  Owner
  Player
  Body
  BodyRoot
  Stop
  WaitSeconds
  WaitWhileExecutionPaused
  FireConfiguredProjectile
  RotateFirePoint
  SetFirePointActive
  AdvancePreviewGroup
```

단, context가 `HogBossAI`의 모든 메서드를 그대로 노출하는 God Object가 되면 실패한 분리다.

### 확인 방법

- Debug fixed pattern으로 분리한 패턴만 반복 실행한다.
- 패턴 취소, HP empty, execution pause, 사망 시 runner 코루틴이 멈추는지 확인한다.
- FirePoint가 패턴 종료 후 비활성화되는지 확인한다.

## 7단계: Drone 패턴 runner 분리

### 목표

Drone도 Hog와 같은 방식으로 패턴 실행 코드를 분리한다.

### 주의점

Drone은 보스 본체와 드론 여러 마리의 상태가 함께 움직인다.

따라서 Hog보다 context 설계가 더 중요하다.

### 추천 순서

1. `RunSummonPattern()`
2. `RunBossBurst()`
3. `RunDronePattern1()`
4. `RunDronePattern3()`
5. `RunDronePattern2()`, `RunDronePattern4()`, `RunDronePattern5()`

이유는 소환과 보스 본체 발사 패턴이 가장 독립적이고, 드론 formation/동기화 패턴은 의존성이 더 크기 때문이다.

## 8단계: BossRushController 추가

### 목표

보스 하나가 죽으면 바로 승리 처리하지 않고 다음 보스로 넘어가게 한다.

### 권장 파일

```text
Rush/BossRushController.cs
```

### 책임

```text
보스 목록 보관
현재 보스 활성화
현재 보스 사망 이벤트 수신
다음 보스 스폰 또는 활성화
마지막 보스 사망 시 승리 처리
BGM 전환 정책 결정
보스 UI target 갱신
```

### UI 처리 주의점

현재 UI는 `BossAI.Defeated`를 직접 받아 승리 UI를 띄우는 구조가 있다.

보스 러쉬에서는 아래처럼 바꾸는 것이 좋다.

```text
BossAI.Defeated
  BossRushController가 수신
  다음 보스가 있으면 다음 보스 시작
  마지막 보스면 Victory 이벤트 발생

HelpGameOverView
  BossAI.Defeated 직접 구독 제거
  BossRushController의 Victory 이벤트 또는 GameFlow 이벤트 구독
```

### 확인 방법

- 1번 보스를 처치해도 Victory UI가 뜨지 않아야 한다.
- 다음 보스가 활성화되어야 한다.
- 마지막 보스를 처치했을 때만 Victory UI가 떠야 한다.
- 보스 UI가 현재 보스를 가리켜야 한다.

## 작업 우선순위

추천 순서는 다음과 같다.

```text
1. DronePilot 패턴 루프 정리
2. 공통 대기 로직 분리
3. 공통 회복 시간 로직 분리
4. 공통 패턴 루프 추출 검토
5. 공통 투사체 설정/발사 분리
6. Hog 패턴 runner 선택 분리
7. Drone 패턴 runner 선택 분리
8. BossRushController 추가
```

보스 러쉬 기능이 더 급하다면 다음 순서로 바꿀 수 있다.

```text
1. BossRushController 추가
2. HelpGameOverView 승리 처리 분리
3. DronePilot 패턴 루프 정리
4. 공통 대기/회복 분리
5. 투사체 설정 공통화
```

## 다른 에이전트에게 주는 작업 규칙

- `Assets/Scripts` 밖의 파일은 열지 않는다.
- `Library`, `Temp`, `Obj`, `Logs`, `Build`, `ProjectSettings`, `Packages`, `*.meta`는 열지 않는다.
- 한 번에 큰 구조 변경을 하지 않는다.
- Inspector 직렬화 필드 변경은 별도 커밋 또는 별도 단계로 진행한다.
- `BossAI` 공통 동작을 바꿀 때는 Hog와 Drone을 모두 확인한다.
- `HogBossAI` 전용 타입에 묶인 helper를 `Core`로 옮길 때는 먼저 타입 의존성을 제거한다.
- 컴파일 오류를 줄이기 위해, 기존 public/protected API를 삭제하기보다 새 API로 옮긴 뒤 사용처를 바꾼다.

## 단계별 최소 검증 체크리스트

### 공통

- 보스가 플레이어 감지 후 전투를 시작한다.
- HP가 0이 되면 처형 가능 상태가 된다.
- 처형하지 않으면 일정 시간 후 HP가 일부 회복된다.
- 처형하면 다음 페이즈로 넘어간다.
- 마지막 목숨 처형 후 사망 처리가 된다.
- 사망 시 활성 보스 투사체가 정리된다.
- 실행 연출 중 보스가 정지한다.

### Hog

- Pattern1: 추격하면서 방사형 탄환을 발사한다.
- Pattern2: volley 설정대로 머신건 탄환을 발사한다.
- Pattern3: 차징 후 분열탄을 발사한다.
- Pattern4/6: bodyRoot slam 후 원형탄 wave를 발사한다.
- Pattern5: FirePoint 차징 후 sweep 발사를 한다.
- Pattern7: guide line 표시 후 3갈래 일반탄과 특수탄을 발사한다.
- 패턴 preview가 다음 패턴/현재 발사 그룹에 맞게 갱신된다.

### Drone

- 보스 본체 패턴이 반복된다.
- 드론 패턴이 반복된다.
- 자동 소환이 정상 동작한다.
- 보스 사망 시 생성 드론이 정리된다.
- 드론 소유권이 해제된다.

### Boss Rush

- 첫 보스 사망 시 Victory UI가 뜨지 않는다.
- 다음 보스가 활성화된다.
- 마지막 보스 사망 시 Victory UI가 뜬다.
- BGM이 의도한 방식으로 전환된다.
- 보스 HP/Lives/Enrage UI가 현재 보스를 가리킨다.

## 결론

지금 구조는 새 보스를 추가할 수 있는 최소 기반은 갖추고 있다.

하지만 보스 러쉬 게임으로 확장하려면, 먼저 보스별 패턴 루프를 통일하고, 대기/회복/투사체 설정을 공통화한 뒤, 보스 러쉬 진행자를 추가해야 한다.

Hog 패턴별 코루틴을 각 클래스로 분리하는 일은 좋은 리팩토링이 될 수 있지만, 새 보스 확장성의 첫 번째 해답은 아니다. 먼저 공통 루프와 공통 발사 기반을 정리하고, 그 다음 보스별 패턴 runner를 필요한 만큼만 분리하는 방향이 가장 안전하다.
