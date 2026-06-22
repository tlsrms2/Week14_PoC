# Boss 최종 구조 전환 계획

## 목표 구조

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
```

## 전환 원칙

- 직렬화된 Inspector 값 보존을 최우선으로 한다.
- 파일 수를 목표 구조에 맞추되, 책임은 억지로 한 클래스에 몰아넣지 않는다.
- `HogBossAI`, `DronePilot`은 Unity 생명주기, 상태 보관, runner 호출만 담당하게 한다.
- 패턴별 실행 순서는 `Patterns/*Runner.cs`로 이동한다.
- 패턴 선택은 `*PatternSelector.cs`로 이동한다.
- 패턴 실행에 필요한 보스 helper는 `*PatternContext.cs`로 모은다.
- 기존 보조 파일 삭제는 기능을 흡수한 뒤 마지막에 진행한다.

## 현재 구조에서 목표 구조로 맞지 않는 항목

### Core

목표 구조에 없는 Core 파일은 제거 또는 흡수 대상이다.

```text
BossPatternLoop.cs
```

처리 방향:

- `BossPatternLoop`는 `HogBossAI`와 `DronePilot` 내부 루프가 충분히 작으면 각 보스에 흡수한다.
- 두 보스의 루프 중복이 다시 커지면 `BossAI` protected helper로 흡수한다.
- 최종적으로 별도 파일은 제거한다.

### Hog

목표 구조에 없는 Hog 파일은 `HogPatternContext` 또는 runner로 흡수 후 제거한다.

```text
HogProjectileOriginResolver.cs
HogProjectileEmitter.cs
HogPatternShotEmitter.cs
HogPatternRecoveryController.cs
HogPatternPreviewPresenter.cs
HogPatternPreviewGroupBuilder.cs
HogPatternMovement.cs
HogPatternEffects.cs
HogPattern7GuideView.cs
HogFirePointUtility.cs
HogBodyRootSlamController.cs
```

처리 방향:

- `HogPatternMovement`, `HogPatternRecoveryController`는 Core의 `BossPatternMovement`, `BossPatternRecovery` 직접 사용으로 대체한다.
- `HogProjectileEmitter`, `HogPatternShotEmitter`는 `HogPatternContext`의 발사 메서드 또는 각 runner 내부 private helper로 흡수한다.
- `HogProjectileOriginResolver`, `HogFirePointUtility`는 `HogPatternContext`로 흡수한다.
- `HogPatternPreviewPresenter`, `HogPatternPreviewGroupBuilder`는 `HogPatternContext`의 preview API로 흡수한다.
- `HogPatternEffects`, `HogPattern7GuideView`, `HogBodyRootSlamController`는 사용하는 runner 또는 `HogPatternContext`로 흡수한다.

### Drone

목표 구조에 필요한 파일 중 아직 정리 대상이 있다.

```text
DronePatternSelector.cs
DronePatternContext.cs
```

처리 방향:

- 현재 runner별 Context를 `DronePatternContext` 하나로 통합한다.
- Drone의 보스 패턴 선택과 드론 패턴 선택을 `DronePatternSelector`로 이동한다.

## 단계별 계획

### 1단계: Core 목표 파일 정리 - 완료

목표:

- Core 폴더를 목표 파일 목록과 맞춘다.

작업:

- 완료: `BossPatternLoop.cs` 사용 지점을 확인한다.
- 완료: 루프 helper를 `HogBossAI`, `DronePilot`로 흡수한다.
- 완료: `BossPatternLoop.cs`를 제거한다.

확인:

- 완료: Core `.cs` 파일 목록이 목표 구조와 일치한다.
- 완료: 코드 기준 `BossPatternLoop`와 `patternLoop` 참조가 남아 있지 않다.
- Unity 확인 필요: Hog/Drone 패턴 루프가 반복 실행된다.
- Unity 확인 필요: 패턴 종료 후 recovery가 정상 동작한다.
- Unity 확인 필요: execution pause 중 루프가 진행되지 않는다.

### 2단계: HogPatternContext 생성 - 완료

목표:

- Hog runner가 공통 Context 하나만 사용하게 한다.

작업:

- 완료: `HogPatternContext.cs`를 추가한다.
- 완료: 다음 책임을 Context로 모은다.
  - 대기와 정지
  - 투사체 발사
  - FirePoint 활성화/회전
  - 투사체 원점 계산
  - preview group 제어
  - guide line 제어
  - body root slam/recover
  - smoke/muzzle effect 실행

확인:

- 완료: 기존 `HogPattern4Runner`, `HogPattern5Runner`, `HogPattern7Runner`가 개별 Context 없이 컴파일된다.
- 완료: `HogPatternContext`가 대기/정지, 발사, FirePoint, 원점 계산, preview, guide, body root, effect 콜백을 가진다.

### 3단계: HogPattern4/5/7Runner Context 통합 - 완료

목표:

- 이미 분리된 runner를 최종 구조에 맞춘다.

작업:

- 완료: `HogPattern4Runner.Context`, `HogPattern5Runner.Context`, `HogPattern7Runner.Context`를 제거한다.
- 완료: 세 runner가 `HogPatternContext`를 받도록 변경한다.
- 완료: `HogBossAI`의 runner 호출부를 단순화한다.

확인:

- 완료: `HogPattern4Runner.Context`, `HogPattern5Runner.Context`, `HogPattern7Runner.Context` 참조가 남아 있지 않다.
- 완료: `HogBossAI`가 `CreatePatternContext()`로 runner 호출부를 단순화했다.
- Unity 확인 필요: Debug fixed pattern으로 Pattern4, Pattern5, Pattern7을 각각 실행한다.
- Unity 확인 필요: Pattern5/7 종료 후 FirePoint가 비활성화된다.
- Unity 확인 필요: Pattern7 guide line이 종료/취소 시 숨겨진다.

### 4단계: HogPattern1Runner 추가 - 완료

목표:

- `HogBossAI.RunPattern1()` 본문을 제거한다.

작업:

- 완료: `Patterns/HogPattern1Runner.cs`를 추가한다.
- 완료: Pattern1의 추격 이동, 원형 배치, 반복 발사 흐름을 runner로 옮긴다.
- 완료: 필요한 helper는 `HogPatternContext`에만 추가한다.

확인:

- 완료: `HogBossAI.RunPattern1()` 본문이 runner 위임으로 축소됐다.
- Unity 확인 필요: Pattern1 탄환 생성 위치와 각도 변화가 기존과 같다.
- Unity 확인 필요: preview group 진행이 기존과 같다.

### 5단계: HogPattern2Runner 추가 - 완료

목표:

- `HogBossAI.RunPattern2()` 본문을 제거한다.

작업:

- 완료: `Patterns/HogPattern2Runner.cs`를 추가한다.
- 완료: Volley 순서, 이동 대기, rest 시간 처리를 runner로 옮긴다.
- 완료: Pattern2 전용 설정 타입은 `HogBossAI` nested type으로 유지한다.

확인:

- 완료: `HogBossAI.RunPattern2()` 본문이 runner 위임으로 축소됐다.
- Unity 확인 필요: volley별 탄환 수, 발사 간격, rest 시간이 유지된다.
- Unity 확인 필요: 대기 중 이동 처리와 pause 처리 동작이 유지된다.

### 6단계: HogPattern3Runner 추가 - 완료

목표:

- `HogBossAI.RunPattern3()` 본문을 제거한다.

작업:

- 완료: `Patterns/HogPattern3Runner.cs`를 추가한다.
- 완료: charge, aim spread, FirePoint, muzzle effect 흐름을 runner로 옮긴다.
- 완료: charge 중 조준/발사 순간 조준 정책이 유지되게 한다.

확인:

- 완료: `HogBossAI.RunPattern3()` 본문이 runner 위임으로 축소됐다.
- Unity 확인 필요: Pattern3 차징 색, 탄환 방향, muzzle effect가 기존과 같다.
- Unity 확인 필요: 발사 후 FirePoint가 비활성화된다.

### 7단계: HogPatternSelector 최종화 - 완료

목표:

- Hog 패턴 선택 책임을 `HogPatternSelector`로 완전히 이동한다.

작업:

- 완료: phase pattern, debug fixed pattern, randomize, repeat 방지 로직을 selector로 옮긴다.
- 완료: `HogBossAI`는 selector에 현재 phase와 설정만 전달한다.

확인:

- 완료: selector가 선택 시 마지막 패턴 기록까지 내부 처리한다.
- Unity 확인 필요: phase별 패턴 목록이 정상 적용된다.
- Unity 확인 필요: debug fixed pattern이 항상 우선한다.
- Unity 확인 필요: repeat 방지 옵션이 유지된다.

### 8단계: Hog 보조 파일 흡수 및 제거 - 완료

목표:

- Hog 폴더를 목표 구조로 축소한다.

작업:

- 완료: 목표 구조에 없는 Hog 보조 파일의 참조를 모두 제거한다.
- 완료: 기능은 `HogPatternContext`, `HogBossAI` 내부 helper, runner 호출 경로로 흡수한다.
- 완료: 참조가 사라진 파일을 삭제한다.

확인:

- 완료: `Assets/Scripts/Boss/Hog`에는 다음 `.cs` 파일만 남는다.
  - `HogBossAI.cs`
  - `HogPatternSelector.cs`
  - `HogPatternContext.cs`
  - `Patterns/HogPattern1Runner.cs`
  - `Patterns/HogPattern2Runner.cs`
  - `Patterns/HogPattern3Runner.cs`
  - `Patterns/HogPattern4Runner.cs`
  - `Patterns/HogPattern5Runner.cs`
  - `Patterns/HogPattern7Runner.cs`
- 완료: 재점검 기준 삭제 대상 Hog 보조 클래스 참조가 남아 있지 않다.
- Unity 확인 필요: Hog Pattern1~7, Pattern6 변형, preview, guide line, body slam, FirePoint 정리가 기존처럼 동작한다.

### 9단계: DronePatternContext 생성 - 완료

목표:

- Drone runner의 개별 Context를 공통 Context로 통합한다.

작업:

- 완료: `DronePatternContext.cs`를 추가한다.
- 완료: 소환, 보스 발사, 드론 명령, 동기화 발사, 대기 helper를 Context로 모은다.
- 완료: runner별 Context 클래스를 제거한다.

확인:

- 완료: `DroneSummonRunner`, `DroneBossBurstRunner`, `DronePattern1~5Runner`가 모두 `DronePatternContext`를 사용한다.
- 완료: runner별 `Context` 클래스와 `.Context` 참조가 남아 있지 않다.
- Unity 확인 필요: Drone 소환, BossBurst, Pattern1~5가 기존처럼 동작한다.

### 10단계: DronePatternSelector 추가 - 완료

목표:

- Drone 패턴 선택 책임을 `DronePatternSelector`로 이동한다.

작업:

- 완료: 보스 패턴 선택과 드론 패턴 선택을 selector로 옮긴다.
- 완료: randomize, sequence index, fallback pattern 처리를 selector로 옮긴다.

확인:

- 완료: 보스 패턴과 드론 패턴 선택이 `DronePatternSelector`를 통해 분리됐다.
- 완료: sequence index와 randomize fallback 처리가 selector 내부에만 남아 있다.
- 완료: `DronePilot`에는 pattern sequence 설정 전달과 selector 호출만 남아 있다.
- Unity 확인 필요: 보스 패턴과 드론 패턴이 각각 정상 반복된다.
- Unity 확인 필요: sequence와 randomize 동작이 기존과 같다.

### 11단계: DronePilot 최종 정리 - 완료

목표:

- `DronePilot`을 상태 보관과 runner 호출 중심으로 축소한다.

작업:

- 완료: runner 호출 wrapper만 남긴다.
- 완료: selector와 context 초기화 코드를 정리한다.
- 완료: 소환/동기화/드론 명령 helper 중 runner 전용 코드는 `DronePatternContext`로 옮긴다.

확인:

- 완료: `Assets/Scripts/Boss/Drone`의 패턴 구조 파일은 다음 목표와 일치한다.
  - `DronePilot.cs`
  - `DronePatternSelector.cs`
  - `DronePatternContext.cs`
  - `Patterns/DroneBossBurstRunner.cs`
  - `Patterns/DroneSummonRunner.cs`
  - `Patterns/DronePattern1Runner.cs`
  - `Patterns/DronePattern2Runner.cs`
  - `Patterns/DronePattern3Runner.cs`
  - `Patterns/DronePattern4Runner.cs`
  - `Patterns/DronePattern5Runner.cs`
- 완료: `DronePilot`에는 소환/동기화/드론 일괄 명령 helper 본문이 남아 있지 않다.
- 보류: `Drone.cs`는 프리팹에 붙는 독립 `MonoBehaviour`라 삭제/병합하면 Unity 스크립트 참조가 깨질 수 있다.
- Unity 확인 필요: 취소/사망 시 동기화 발사 정리, 드론 정지, 소환 드론 제거가 기존처럼 동작한다.

### 12단계: 최종 검증

목표:

- 구조와 동작을 동시에 검증한다.

작업:

- 목표 구조 외 `.cs` 파일이 남아 있는지 확인한다.
- Console 컴파일 오류를 확인한다.
- Hog Debug fixed pattern으로 Pattern1, Pattern2, Pattern3, Pattern4, Pattern5, Pattern6, Pattern7을 확인한다.
- Drone Summon, BossBurst, DronePattern1, DronePattern2, DronePattern3, DronePattern4, DronePattern5를 확인한다.
- 기존 prefab Inspector 값이 유지되는지 확인한다.

완료 기준:

- 목표 구조의 파일만 남는다.
- Hog/Drone 모든 패턴이 기존과 같은 순서와 연출로 동작한다.
- execution pause, HP empty, 사망 중 코루틴이 멈춘다.
- FirePoint, guide line, preview가 패턴 종료 후 정리된다.
