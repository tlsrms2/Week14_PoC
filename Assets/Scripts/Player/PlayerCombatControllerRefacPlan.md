# PlayerCombatController 역할 분리 계획

## 원인

`PlayerCombatController`가 현재 한 클래스에서 아래 책임을 동시에 처리한다.

- 입력 분기와 전투 가능 상태 판단
- 조준, 마우스 좌표, 락온 대상 선정
- 좌클릭 사격, 탄환 소모, 대미지 계산, 총구 이펙트
- 우클릭 패링, 투사체 탐색, 레티클 표시, 실패 패널티
- 처형 시작/진행/종료, 카메라 포커스, 화면 딤, HP UI 숨김
- 피격 처리, 몸통 접촉 대미지, 넉백, 히트스톱, 바디 플래시
- 커서 표시, 리그 참조 자동 연결, 카메라 참조 캐싱

이 구조는 전투 규칙을 고치려 할 때 연출/UI/카메라 코드까지 같이 건드리게 만들어 변경 범위가 커진다.

## 목표

- `PlayerCombatController`는 수명주기와 하위 역할 조율만 담당한다.
- 프리팹 직렬화가 깨지지 않도록 기존 `SerializeField`는 1차 리팩터링에서 최대한 유지한다.
- 외부에서 쓰는 공개 API는 먼저 유지한다.
  - `Active`
  - `IsExecutionCinematicActive`
  - `Health`
  - `Bullets`
  - `LockOnTarget`
  - `HoveredExecutionTarget`
  - `IsExecuting`
  - `CanMove`
  - `ShouldStopMovementWhenBlocked`
  - `SetConfig`
  - `ReceiveAttack`
  - `PlayParryImpact`

## 분리 대상

### 1. PlayerCombatState

전투 공통 상태를 모은다.

- `Health`, `BulletGauge`, `PlayerCombatConfig`
- `IsExecuting`, `IsWaitingForVictoryPanel`
- `CanAct`, `CanMove`, `ShouldStopMovementWhenBlocked`
- 몸통 접촉 스턴 상태

처음에는 별도 `MonoBehaviour`보다 일반 C# 상태 객체로 시작한다.

### 2. PlayerAimController

조준과 캐릭터 방향 갱신을 담당한다.

- `RotateToAim`
- `GetAimPoint`
- `GetAimDirection`
- `GetMouseWorldPosition`
- `LockLeftGunAim`
- `AimExecutionPose`
- `AimGunAndGetDirection`

락온 대상은 직접 찾지 않고 `PlayerLockOnController`에서 받은 값을 사용한다.

### 3. PlayerLockOnController

적 락온 대상 선정과 유지 조건을 담당한다.

- `UpdateLockOnTarget`
- `FindNearestLockOnTarget`
- `ClearInvalidLockOnTarget`
- `SetLockOnTarget`
- 카메라 안 대상 판정
- 보스/드론 유효성 판정

카메라 포커스 반영은 직접 카메라를 제어하지 않고 콜백 또는 결과값으로 `PlayerCombatController`에 넘긴다.

### 4. PlayerShooter

좌클릭 사격만 담당한다.

- `TryShootEnemy`
- `CalculateAttackBulletDamage`
- 총알 소모/복구
- `PlayerProjectile.Spawn`
- 총구 이펙트와 사운드 요청

조준 방향은 `PlayerAimController`에서 받아 사용한다.

### 5. PlayerParryController

패링 판정과 패링 UI를 담당한다.

- `TryParryProjectile`
- `FindClosestInterceptTarget`
- `IsProjectileInMouseParryRange`
- `ApplyMouseParryMissPenalty`
- 레티클 위치/스케일/위협 상태 갱신
- 투사체 락온 인디케이터 표시
- `PlayParryImpact`

레티클 표시와 실제 패링 판정을 같은 클래스에 묶어 마우스 패링 규칙 변경 범위를 줄인다.

### 6. PlayerExecutionController

처형 규칙과 처형 시퀀스를 담당한다.

- `TryBeginExecution`
- `UpdateHoveredExecutionTarget`
- `FindHoveredExecutionTarget`
- `ExecuteTarget`
- `FinishExecution`
- `RunExecutionFlourish`
- `FireExecutionFlourishShot`
- 승리 패널 대기 상태

처형 중 카메라, HP UI, 화면 딤 같은 연출은 `PlayerExecutionPresentation`으로 다시 나눌 수 있게 경계를 둔다.

### 7. PlayerExecutionPresentation

처형 연출 보조를 담당한다.

- `HidePlayerHpForExecution`
- `RestorePlayerHpAfterExecution`
- `BeginFinalDeathCameraFocus`
- `WaitBeforeFinalDeathFocus`
- `UpdateExecutionFocusPoint`
- `PlayExecutionShotDim`
- `StopExecutionShotDim`
- `ExecutionImageEffect` 제어

처형 규칙과 연출 타이밍을 분리해 보스 사망 연출 변경이 공격 로직에 번지지 않게 한다.

### 8. PlayerDamageReceiver

피격, 몸통 접촉, 히트 피드백을 담당한다.

- `ReceiveAttack`
- `TryReceiveEnemyBodyContact`
- `IsEnemyBodyContact`
- `ApplyEnemyBodyContactKnockback`
- `FlashBodyHitColor`
- `UpdateBodyColor`
- `PlayHitStop`
- 피격 VFX와 카메라 충격 요청

충돌 이벤트는 `PlayerCombatController`가 받아 이 클래스로 위임한다.

### 9. PlayerCombatRig

씬/프리팹 참조 해석을 담당한다.

- `ResolveRigReferences`
- `ResolveMouseParryReticleReference`
- `CacheBodyRenderers`
- `FindChildRecursive`
- `GetLeftFireOrigin`
- `StopBody`

참조 자동 탐색 코드를 한곳으로 모아 프리팹 구조 변경 대응을 쉽게 한다.

## 적용 순서

1. `PlayerCombatController`에 공통 의존성을 넘기는 내부 컨텍스트를 만든다.
2. 외부 공개 API와 `SerializeField`는 유지한 채, 메서드를 역할별 클래스로 한 덩어리씩 이동한다.
3. 먼저 독립성이 높은 `PlayerDamageReceiver`와 `PlayerCombatRig`를 분리한다.
4. 다음으로 `PlayerAimController`, `PlayerLockOnController`를 분리한다.
5. 사격과 패링을 각각 `PlayerShooter`, `PlayerParryController`로 분리한다.
6. 마지막에 처형 관련 코드를 `PlayerExecutionController`, `PlayerExecutionPresentation`으로 분리한다.
7. 각 단계마다 Unity 컴파일 에러와 기존 프리팹 인스펙터 참조 유지 여부를 확인한다.

## 최소 수정 원칙

- 한 PR 또는 한 커밋에서는 한 책임만 이동한다.
- 동작 변경 없이 메서드 이동과 위임부터 진행한다.
- `PlayerCombatController`는 최종적으로 `Awake`, `OnEnable`, `OnDisable`, `Start`, `Update`, 충돌 이벤트, 공개 API만 남긴다.
- 새 클래스 파일은 `Assets/Scripts/Player`에 둔다.
- 네임스페이스는 1차 리팩터링에서 기존 `Week14.Combat`를 유지하고, 참조 정리가 끝난 뒤 `Week14.Player` 전환을 검토한다.
- 기존 씬/프리팹 참조를 유지하기 위해 새 `MonoBehaviour` 추가는 마지막 단계에서만 검토한다.

## Unity Editor 확인 방법

각 단계마다 아래를 확인한다.

1. 콘솔 컴파일 에러가 없는지 확인한다.
2. 플레이어 프리팹의 `PlayerCombatController` 직렬화 참조가 유지되는지 확인한다.
3. Play Mode에서 좌클릭 사격, 우클릭 패링, 피격 시 탄환 감소, 처형 시작/종료가 기존처럼 동작하는지 확인한다.
4. 보스 최종 사망 연출 후 카메라와 승리 패널 흐름이 유지되는지 확인한다.
5. 마우스 레티클과 투사체 락온 인디케이터가 비활성/사망/처형 상태에서 꺼지는지 확인한다.
