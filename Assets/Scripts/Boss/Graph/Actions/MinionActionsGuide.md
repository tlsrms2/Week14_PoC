# Boss Editor Minion 액션 설명서

## 0. 현재 스크린샷 대비 확인 사항

- `Aim > Mode`는 현재 `AtPlayer`, `FixedAngle`, `ClosestMinionToPlayer` 3개입니다. 스크린샷에 `ClosestMinionToPlayer` 설명이 없다면 추가해야 합니다.
- `Effects > Smoke Interval`은 `Smoke` 폴드아웃을 펼쳤을 때만 보입니다. 스크린샷처럼 Smoke 하위 필드로 설명하는 것이 맞습니다.
- `FireProjectileBurstAction`은 별도 파일이 아니라 `FireProjectileAction.cs` 안에 함께 정의되어 있습니다.
- `FireMachinegunProjectilesAction.cs`, `FireRotatingProjectilesAction.cs`는 현재 레거시 클래스가 제거된 빈 파일입니다. 설명서에서 실제 액션처럼 다루면 안 됩니다.
- 현재 Attack 계열에는 `SpawnChargedProjectileAction`, `ConfigureProjectileGrowthAction`, `ConfigureRadialSplitAction`, `WaitProjectileChargeEndAction`이 추가되어 있습니다.
- 현재 Move 계열에는 `MaintainPlayerDistanceAction`, `StartMoveAwayFromPlayerAction`이 추가되어 있고, `MoveTowardPlayerAction`/`StartMoveTowardPlayerAction`에는 `speedCurve`가 있습니다.
- `StartMoveTowardPlayerAction`은 `durationSeconds > 0`이면 해당 시간만 이동 후 자동 정지하고, `0`이면 이동 시작만 걸고 다음 액션으로 넘어갑니다.
- `Utility` 계열에는 `WindupAction`이 추가되어 있습니다.

## 0-1. 스크린샷에 없는 추가 필요 항목 모음

### 공통 필드

- `Aim > ClosestMinionToPlayer`: 플레이어와 가장 가까운 미니언의 조준 방향을 모든 미니언이 공유합니다. Minion 발사 액션에서 특히 중요합니다.
- `Effects > Smoke Interval`: Smoke 폴드아웃을 펼쳤을 때만 보이는 하위 필드입니다.
- `waitForDuration`: Minion 이동/발사 명령을 내린 뒤 해당 지속 시간을 기다릴지 정하는 공통 성격의 옵션입니다.
- `windupSeconds`: Minion Fire 액션 공통 발사 전 대기 시간입니다.

### Attack 액션

- `SpawnChargedProjectileAction`: 차지 상태의 투사체를 생성하고 `handleKey`로 저장합니다.
- `ConfigureProjectileGrowthAction`: 저장된 차지 투사체의 시작/최종 크기 배율을 설정합니다.
- `ConfigureRadialSplitAction`: 저장된 차지 투사체가 발사 후 방사형으로 분열하도록 설정합니다.
- `WaitProjectileChargeEndAction`: 저장된 차지 투사체의 차지가 끝날 때까지 기다리고, 추적 종료/연기/발사 이펙트를 처리합니다.
- `FireMachinegunProjectilesAction`, `FireRotatingProjectilesAction`: 현재 실제 액션 클래스가 제거된 레거시 파일이므로 설명서에서 제외해야 합니다.

### Move 액션

- `MaintainPlayerDistanceAction`: 지정 시간 동안 플레이어와 목표 거리를 유지합니다.
- `StartMoveAwayFromPlayerAction`: 플레이어 반대 방향 이동을 시작합니다.
- `speedCurve`: `MoveTowardPlayerAction`, `MaintainPlayerDistanceAction`, `StartMoveTowardPlayerAction`, `StartMoveAwayFromPlayerAction`에 있는 이동 속도 곡선입니다.
- `MoveBodyRootLocalAction > completeSfxId`: BodyRoot 이동 완료 후 재생할 SFX ID입니다.

### Utility 액션

- `WindupAction`: 지정 시간 동안 대기하며, 선택적으로 보스 이동을 멈추고 Smoke 이펙트를 반복 재생합니다.

### Animation 액션

- `PlayAnimationAction`: `Trigger` 모드에서는 트리거를 실행하고, `State` 모드에서는 지정 레이어/Normalized Time으로 상태를 재생합니다.
- `WaitForAnimationEventAction`: 지정한 `eventId` 애니메이션 이벤트를 기다립니다. `timeoutSeconds`가 `0`이면 이벤트가 올 때까지 계속 기다립니다.

### Minion 액션

- `Spawn`: `MinionSummonAction`, `MinionEnsureCountAction`, `MinionAutoSummonIfNeededAction` 설명이 필요합니다.
- `Movement`: Formation, Gather, Orbit, PlayerPath, Dash, Wander, HoldPosition 등 미니언 전용 이동 명령 설명이 필요합니다.
- `Fire`: Repeat, Sequential, RadialBurst, SideFire 등 미니언 전용 발사 명령 설명이 필요합니다.
- `Control`: `MinionPatternCleanupAction`으로 명령 대기/중지/Idle 복귀를 정리하는 설명이 필요합니다.
- `Wander/HoldPosition`의 시간 `0`: 액션 대기는 즉시 끝나지만, 미니언 명령 자체는 다음 명령 전까지 계속 유지됩니다.

## 1. Minion 액션 공통

- 모든 Minion 액션은 보스가 `IMinionPatternHost`이고 `minionPatternEnabled`가 켜져 있을 때만 실행됩니다. 조건이 맞지 않으면 아무 일도 하지 않고 종료됩니다.
- `waitForDuration`이 켜져 있으면 액션이 계산한 지속 시간만큼 다음 액션 실행을 기다립니다. 꺼져 있으면 미니언에게 명령만 내리고 다음 액션으로 넘어갑니다.
- `speedMultiplier`는 대부분 내부 기본 이동 속도 `24`에 곱해집니다. `0`이면 목표 위치를 잡지만 실제 이동 속도는 0이 됩니다.
- `settleSeconds`는 정렬/이동 명령을 유지하며 기다리는 시간입니다. 정렬형 이동은 이 시간이 지나도 다음 명령 전까지 계속 포메이션 루틴을 유지합니다.

## 2. Minion 발사 공통 필드

### Projectile Name

- 미니언이 발사할 투사체 설정 이름입니다.
- 이름을 찾지 못하면 기본 투사체 설정을 다시 찾고, 그래도 없으면 액션은 종료됩니다.

### Minion Origin

- `ProjectileOrigin`: 미니언의 Projectile Origin 위치에서 발사합니다.
- `MinionRoot`: 미니언 루트 위치에서 발사합니다.
- `MinionChild`: 지정한 미니언 자식 경로 위치에서 발사합니다.
- `MinionChildList`: 발사 순서에 맞는 자식 경로를 사용합니다. 인덱스가 목록보다 크면 마지막 경로를 계속 씁니다.
- `AlternatingMinionChildList`: 발사 순서에 따라 자식 경로 목록을 반복 순환합니다.
- `AlternatingMinionChildren`: `firstMinionChildPath`, `secondMinionChildPath`를 번갈아 씁니다.
- `fallbackSpacing`: `ProjectileOrigin` 모드에서 2발 이상 같은 위치에 겹치지 않도록 발사 방향의 좌우로 벌리는 간격입니다.

### Aim

- `AtPlayer`: 각 발사 위치에서 플레이어를 향합니다.
- `FixedAngle`: `angleDegrees` 월드 각도로 발사합니다.
- `ClosestMinionToPlayer`: 플레이어와 가장 가까운 미니언이 계산한 플레이어 방향을 모든 미니언이 공유합니다.

### Effects

- `Explosion`: 발사 위치에 폭발 파티클을 재생합니다.
- `Smoke`: 대기/차지 중 연기 파티클을 반복 재생합니다.
- `Smoke Interval`: Smoke가 켜져 있을 때 연기 재생 간격입니다.
- `Muzzle Flash`: 발사 위치에 총구 섬광을 재생합니다.
- `Camera Shake`: 카메라 흔들림을 재생합니다.

## 3. Spawn Minion 액션

### Minion Summon Action

- `summonCount`: 이번 액션에서 소환할 수입니다. `0`이면 보스의 `MinionSummonSettings.SummonCount`를 사용합니다.
- `stopBossWhileSummoning`: 켜면 소환 중 보스 이동을 멈춥니다.
- 최대 보유 수가 설정되어 있으면 남은 슬롯만큼만 소환됩니다.

### Minion Ensure Count Action

- `targetCount`: 현재 조종 중인 미니언 수가 이 값보다 적으면 부족한 수만큼 소환합니다.
- `0` 이하는 아무것도 하지 않습니다.

### Minion Auto Summon If Needed Action

- 별도 필드가 없습니다.
- 자동 소환 쿨타임이 지났고 최대 보유 수보다 적을 때만 보스의 소환 설정으로 소환합니다.

## 4. Movement Minion 액션

### Minion Formation Action

- 미니언을 플레이어 주변 원형 포메이션으로 배치합니다.
- `radius`: 플레이어 기준 배치 반경입니다.
- `sideBySide`: 켜면 보스와 플레이어 사이 거리를 기준으로 좌우 나란히 배치합니다.
- `angleSpacingDegrees`: 미니언 사이 각도 간격입니다.
- `speedMultiplier`: 포메이션 위치로 이동하는 속도 배율입니다.
- `settleSeconds`: 다음 액션으로 넘어가기 전 기다릴 시간입니다.
- `waitForDuration`: `settleSeconds`만큼 기다릴지 정합니다.

### Minion Formation Straight Action

- 미니언을 플레이어 주변 직선 포메이션으로 배치합니다.
- `mode`
  - `PlayerForward`: 플레이어 진행 방향을 기준으로 플레이어 앞쪽에 직선을 만듭니다. 시작 시에는 보스에서 플레이어를 향한 방향을 기본값으로 씁니다.
  - `BetweenBossAndPlayer`: 보스와 플레이어 사이 방향에 직선을 만듭니다.
- `distanceFromPlayer`: 직선 중심이 플레이어에서 떨어질 거리입니다.
- `spacing`: 미니언 사이 간격입니다.
- `speedMultiplier`: 포메이션 위치로 이동하는 속도 배율입니다.
- `settleSeconds`: 다음 액션으로 넘어가기 전 기다릴 시간입니다.
- `waitForDuration`: `settleSeconds`만큼 기다릴지 정합니다.

### Minion Gather Action

- 미니언을 플레이어 주변 한 지점 또는 레이아웃으로 모읍니다.
- `anchorMode`
  - `ClosestToPlayer`: 플레이어와 가장 가까운 미니언 방향을 기준으로 모읍니다.
  - `FarthestFromPlayer`: 플레이어와 가장 먼 미니언 방향을 기준으로 모읍니다.
  - `MiddleDistanceToPlayer`: 거리 순서 중간 미니언 방향을 기준으로 모읍니다.
  - `FixedAngle`: `angleDegrees`를 기준 각도로 사용합니다.
- `layout`
  - `Circle`: 플레이어 주변 원형으로 배치합니다.
  - `Vertical`: 기준 방향으로 한 줄로 세웁니다.
  - `Orthogonal`: 기준 방향 반경 위치에서 접선 방향으로 나란히 세웁니다.
  - `Random`: 반경 안 무작위 위치로 흩어 모읍니다.
- `radius`: 플레이어 기준 반경입니다.
- `spacing`: 줄형 배치에서 미니언 사이 간격입니다.
- `moveSpeed`: 모이는 속도입니다.
- `settleSeconds`: 다음 액션으로 넘어가기 전 기다릴 시간입니다.
- `waitForDuration`: `settleSeconds`만큼 기다릴지 정합니다.

### Minion Angle Distance Move Action

- 각 미니언을 플레이어 기준 지정 각도/거리 슬롯으로 이동시킵니다.
- `slots`: `angleDegrees`, `distance` 목록입니다. 미니언 수가 슬롯 수보다 많으면 슬롯을 반복해서 씁니다.
- `angleDegrees`: 월드 각도 기준 방향입니다.
- `distance`: 플레이어에서 떨어질 거리입니다.
- `speedMultiplier`: 이동 속도 배율입니다.
- `settleSeconds`: 다음 액션으로 넘어가기 전 기다릴 시간입니다.
- `waitForDuration`: `settleSeconds`만큼 기다릴지 정합니다.

### Minion Orbit Action

- 미니언을 플레이어 주변으로 공전시킵니다.
- `orbitRadius`: 공전 반경입니다.
- `orbitSeconds`: 한 바퀴 공전에 사용할 시간입니다.
- `moveSpeed`: 공전 목표 위치로 따라가는 속도입니다.
- `useStartPlayerPosition`: 켜면 공전 중심을 액션 시작 시점의 플레이어 위치로 고정합니다.
- `randomizeDirection`: 켜면 시계/반시계 방향을 무작위로 정합니다.
- `clockwise`: `randomizeDirection`이 꺼졌을 때 시계 방향 여부입니다.
- `waitForDuration`: `orbitSeconds`만큼 기다릴지 정합니다.

### Minion Player Path Action

- 미니언을 플레이어 주변 한쪽에서 반대쪽으로 통과시킵니다.
- `mode`
  - `HorizontalVertical`: 미니언 순서대로 좌→우, 위→아래, 우→좌, 아래→위 경로를 반복합니다.
  - `Diagonal`: 미니언 순서대로 네 대각선 경로를 반복합니다.
- `distanceFromPlayer`: 시작/도착 지점이 플레이어에서 떨어질 거리입니다.
- `moveToStartSeconds`: 시작 지점으로 이동하는 시간입니다. `0`이면 즉시 시작 위치로 이동합니다.
- `moveSeconds`: 시작 지점에서 도착 지점까지 이동하는 시간입니다.
- `waitForDuration`: `moveToStartSeconds + moveSeconds`만큼 기다릴지 정합니다.

### Minion Dash Action

- 미니언을 Aim 방향으로 돌진시킵니다.
- `aim`: 돌진 방향 기준입니다.
- `dashSeconds`: 돌진 지속 시간입니다.
- `dashSpeed`: 돌진 속도입니다.
- `aimOffsetDegrees`: 미니언 순서에 따라 `+`, `-` 방향으로 번갈아 적용되는 각도 오프셋입니다.
- `waitForDuration`: `dashSeconds`만큼 기다릴지 정합니다.

### Minion Wander Action

- 미니언을 액션 시작 위치 주변에서 배회시킵니다.
- `wanderSeconds`: 배회 시간입니다. `0`이면 다음 명령 전까지 계속 배회합니다.
- `speed`: 배회 이동 속도입니다.
- `radius`: 액션 시작 위치 기준 배회 반경입니다.
- `retargetSeconds`: 새 배회 목표를 다시 잡는 간격입니다.
- `waitForDuration`: `wanderSeconds`만큼 기다릴지 정합니다. `wanderSeconds`가 `0`이면 대기 시간이 없어서 바로 다음 액션으로 넘어갑니다.

### Minion Hold Position Action

- 미니언을 현재 위치에서 정지시킵니다.
- `holdSeconds`: 정지 유지 시간입니다. `0`이면 다음 명령 전까지 계속 정지합니다.
- `waitForDuration`: `holdSeconds`만큼 기다릴지 정합니다. `holdSeconds`가 `0`이면 대기 시간이 없어서 바로 다음 액션으로 넘어갑니다.

## 5. Fire Minion 액션

### Minion Repeat Fire Action

- 모든 미니언에게 같은 반복 발사 명령을 내립니다.
- 공통 필드: `projectileName`, `minionOrigin`, `aim`, `effects`, `windupSeconds`
- `windupSeconds`: 발사 전 대기 시간입니다.
- `Volleys`: 반복 발사 묶음 목록입니다.
- `bulletCount`: 해당 묶음에서 미니언마다 발사할 탄 수입니다.
- `fireInterval`: 같은 묶음 안에서 탄 사이 간격입니다.
- `restSeconds`: 다음 묶음으로 넘어가기 전 쉬는 시간입니다.
- `waitForDuration`: 마지막 발사 묶음의 완료를 기다릴지 정합니다. 다음 묶음이 있으면 이 값과 무관하게 묶음 사이 진행을 위해 기다립니다.

### Minion Sequential Fire Action

- 미니언을 순서대로 하나씩 발사시킵니다.
- 공통 필드: `projectileName`, `minionOrigin`, `aim`, `effects`, `windupSeconds`
- `cycleCount`: 전체 미니언 순차 발사를 몇 사이클 반복할지 정합니다.
- `fireInterval`: 다음 미니언 발사까지의 간격입니다.
- 이 액션은 순차 발사 코루틴이 끝날 때까지 기다립니다.

### Minion Radial Burst Action

- 각 미니언이 Aim 방향을 중심으로 방사형 탄막을 발사합니다.
- 공통 필드: `projectileName`, `minionOrigin`, `aim`, `effects`, `windupSeconds`
- `volleyCount`: 방사형 발사 묶음 수입니다.
- `directionCount`: 한 묶음에서 나갈 방향 수입니다.
- `volleyInterval`: 묶음 사이 간격입니다.
- `spreadDegrees`: Aim 방향 기준 부채꼴 각도입니다. `0`이면 360도 전방위로 발사합니다.
- `waitForDuration`: 전체 발사 예상 시간만큼 기다릴지 정합니다.

### Minion Side Fire Action

- 각 미니언이 Aim 방향의 좌우로 두 발씩 반복 발사합니다.
- 공통 필드: `projectileName`, `minionOrigin`, `aim`, `effects`, `windupSeconds`
- `fireSeconds`: 좌우 발사 지속 시간입니다.
- `fireInterval`: 좌우 한 쌍을 다시 발사하는 간격입니다.
- `sideFireAngleDegrees`: Aim 방향에서 좌우로 벌어질 각도입니다.
- `originMode`
  - `SharedOrigin`: 두 발 모두 같은 origin에서 나갑니다.
  - `BodySides`: 미니언 몸체 좌우 위치에서 한 발씩 나갑니다.
- `bodySideSpacing`: `BodySides`일 때 몸체 중심에서 좌우로 벌릴 거리입니다.
- `waitForDuration`: `fireSeconds`만큼 기다릴지 정합니다.

## 6. Control Minion 액션

### Minion Pattern Cleanup Action

- 미니언 커맨드를 정리하고 필요하면 대기 상태로 돌립니다.
- `waitForCommands`: 현재 명령 중인 미니언이 끝날 때까지 기다립니다.
- `waitTimeoutSeconds`: 대기 제한 시간입니다. `0`이면 제한 없이 기다립니다.
- `stopAllMinions`: 켜면 모든 미니언의 현재 명령을 즉시 중지합니다.
- `resumeIdle`: 켜면 미니언을 기본 대기 행동으로 되돌립니다.
- 실행 순서는 `stopAllMinions` → `resumeIdle` → `waitForCommands`입니다.

## 7. Unity Editor에서 확인할 방법

- Boss Graph/Attack Sequence 에셋에서 Action 추가 메뉴를 열고 `Minion` 하위 액션들이 `Spawn`, `Movement`, `Fire`, `Control` 기준으로 보이는지 확인합니다.
- Minion 발사 액션을 하나 추가한 뒤 `Minion Origin`, `Aim`, `Effects` 폴드아웃을 펼쳐 위 필드명이 실제 인스펙터와 같은지 확인합니다.
- `Minion Wander Action`과 `Minion Hold Position Action`은 시간을 `0`으로 두고 다음 액션을 이어 붙여, 미니언 명령은 유지되지만 그래프는 바로 다음 액션으로 넘어가는지 확인합니다.
