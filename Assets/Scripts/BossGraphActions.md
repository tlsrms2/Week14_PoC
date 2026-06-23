## Attack

### FireProjectileAction

메뉴: `Projectile/Fire Projectile`

동작: 단발 투사체를 발사합니다. 조준은 플레이어 방향 또는 고정 각도를 사용합니다.

필드:
- `string projectileName`: 보스 인스펙터의 공통 투사체 이름.
- `BossProjectileSettings projectile`: 숨김 fallback 투사체 설정.
- `BossGraphAimMode aimMode`: `Player`, `Angle`.
- `float angleDegrees`: `aimMode`가 `Angle`일 때 발사 각도.
- `float spawnRadius`: 발사 위치를 발사 방향으로 밀어낼 거리.

패턴 탄 줄 표시: 1개.

### FireRadialProjectilesAction

메뉴: `Projectile/Fire Radial Projectiles`

동작: 원형 또는 부채꼴로 여러 투사체를 발사합니다.

필드:
- `string projectileName`: 보스 인스펙터의 공통 투사체 이름.
- `BossProjectileSettings projectile`: 숨김 fallback 투사체 설정.
- `int bulletCount`: 발사 개수.
- `bool centerOnPlayer`: 부채꼴 중심을 플레이어 방향에 맞출지 여부.
- `float arcDegrees`: 전체 발사 각도.
- `float spawnRadius`: 중심에서 각 탄 방향으로 떨어진 생성 거리.
- `float fireInterval`: 탄 사이 발사 간격.
- `string fireSfxId`: 발사 SFX.
- `BossGraphEffectSettings effects`: 이펙트 설정 묶음.
- `Vector2 cameraShakeDirection`: 카메라 흔들림 방향.

패턴 탄 줄 표시: `bulletCount`.

### FireRotatingProjectilesAction

메뉴: `Projectile/Fire Rotating Projectiles`

동작: 시작 각도를 랜덤으로 잡고 360도를 나누어 연속 발사합니다. 두 발사 원점을 교대로 사용할 수 있습니다.

필드:
- `string projectileName`: 보스 인스펙터의 공통 투사체 이름.
- `BossProjectileSettings projectile`: 숨김 fallback 투사체 설정.
- `string firstProjectileOriginPath`: 첫 번째 발사 원점.
- `string secondProjectileOriginPath`: 두 번째 발사 원점.
- `int bulletCount`: 발사 개수.
- `float spawnRadius`: 원점에서 각 탄 방향으로 떨어진 생성 거리.
- `float fireInterval`: 탄 사이 발사 간격.
- `string fireSfxId`: 발사 SFX.
- `string launchSfxId`: 투사체 Launch 시점 SFX.
- `BossGraphEffectSettings effects`: 이펙트 설정 묶음.

패턴 탄 줄 표시: `bulletCount`.

### FireMachinegunProjectilesAction

메뉴: `Projectile/Fire Machinegun Projectiles`

동작: 발리 목록을 순회하며 플레이어 방향으로 머신건처럼 연사합니다. 발사/대기 중 보스가 플레이어 쪽으로 이동할 수 있습니다.

필드:
- `string projectileName`: 보스 인스펙터의 공통 투사체 이름.
- `BossProjectileSettings projectile`: 숨김 fallback 투사체 설정.
- `string firstProjectileOriginPath`: 첫 번째 발사 원점.
- `string secondProjectileOriginPath`: 두 번째 발사 원점.
- `float moveSpeedMultiplier`: 연사 중 보스 이동 속도 배율.
- `float spawnSpacing`: 발사 원점이 없을 때 좌우로 번갈아 벌릴 간격.
- `string fireSfxId`: 발사 SFX.
- `string launchSfxId`: 투사체 Launch 시점 SFX.
- `BossGraphEffectSettings effects`: 이펙트 설정 묶음.
- `List<Volley> volleys`: 발리 목록.

`Volley` 필드:
- `int bulletCount`: 해당 발리의 탄 수.
- `float fireInterval`: 같은 발리 안의 탄 사이 간격.
- `float restSeconds`: 다음 발리 전 대기 시간.

패턴 탄 줄 표시: 모든 `Volley.bulletCount` 합.

### FireSweepProjectilesAction

메뉴: `Projectile/Fire Sweep Projectiles`

동작: 준비 시간 후 플레이어 방향을 기준으로 좌우 각도를 쓸듯이 바꾸며 연사합니다.

필드:
- `string projectileName`: 보스 인스펙터의 공통 투사체 이름.
- `BossProjectileSettings projectile`: 숨김 fallback 투사체 설정.
- `string projectileOriginPath`: 발사 원점.
- `float windupSeconds`: 발사 전 준비 시간.
- `int bulletCount`: 발사 개수.
- `float fireInterval`: 탄 사이 발사 간격.
- `float spawnSpacing`: 좌우 교대 생성 간격.
- `float sweepStepDegrees`: 탄마다 바뀌는 각도.
- `float maxSweepAngle`: 좌우 최대 스윕 각도.
- `string fireSfxId`: 발사 SFX.
- `BossGraphEffectSettings effects`: 이펙트 설정 묶음.

패턴 탄 줄 표시: `bulletCount`.

### FireFanVolleyProjectilesAction

메뉴: `Projectile/Fire Fan Volley Projectiles`

동작: 준비 시간 후 플레이어 방향을 고정하고, 3발 부채꼴 일반 발리를 여러 번 발사합니다. 첫 발리 때 보조 투사체도 발사할 수 있습니다.

필드:
- `string normalProjectileName`: 일반탄 공통 투사체 이름.
- `BossProjectileSettings normalProjectile`: 숨김 fallback 일반탄 설정.
- `string secondaryProjectileName`: 보조탄 공통 투사체 이름.
- `BossProjectileSettings secondaryProjectile`: 숨김 fallback 보조탄 설정.
- `string projectileOriginPath`: 일반탄 발사 원점.
- `List<string> secondaryOriginPaths`: 보조탄 발사 원점 목록.
- `float windupSeconds`: 발사 전 준비 시간.
- `int normalVolleyCount`: 일반탄 발리 횟수.
- `float normalVolleyInterval`: 일반탄 발리 사이 간격.
- `int secondaryBulletCount`: 보조탄 개수.
- `float fanAngleDegrees`: 3발 부채꼴 전체 각도.
- `float normalSpawnSpacing`: 일반탄 좌우 생성 간격.
- `float secondarySpawnForwardOffset`: 보조탄을 조준 방향 앞으로 밀어낼 거리.
- `string normalVolleySfxId`: 일반탄 발리 SFX.
- `string secondaryFireSfxId`: 보조탄 발사 SFX.
- `string secondaryLaunchSfxId`: 보조탄 Launch 시점 SFX.
- `BossGraphEffectSettings effects`: 이펙트 설정 묶음.

패턴 탄 줄 표시: `normalVolleyCount * 3 + secondaryBulletCount`.

### FireChargedRadialSplitProjectileAction

메뉴: `Projectile/Fire Charged Radial Split Projectile`

동작: 원점에 차징 투사체를 생성하고, 발사 후 일정 시간 뒤 방사형으로 분열시킵니다.

필드:
- `string projectileName`: 보스 인스펙터의 공통 투사체 이름.
- `BossProjectileSettings projectile`: 숨김 fallback 투사체 설정.
- `string projectileOriginPath`: 차징/발사 원점.
- `float windupSeconds`: 차징 시간.
- `float aimTrackingSeconds`: 차징 중 플레이어 조준을 따라가는 시간.
- `float projectileRadiusMultiplier`: 차징 투사체 반지름 배율.
- `float startScaleMultiplier`: 차징 시작 스케일 배율.
- `float finalScaleMultiplier`: 차징 최종 스케일 배율.
- `float aimSpreadDegrees`: 최초 조준 랜덤 퍼짐 각도.
- `int radialSplitBulletCount`: 분열 탄 수.
- `float radialSplitStartAngleOffset`: 분열 시작 각도 오프셋.
- `float splitDelaySeconds`: 발사 후 분열까지 지연 시간.
- `float splitSpeedMultiplier`: 분열탄 속도 배율.
- `float splitRadiusMultiplier`: 분열탄 반지름 배율.
- `float splitLifetimeMultiplier`: 분열탄 수명 배율.
- `float bombSfxLeadSeconds`: 분열 임박 SFX 선행 시간.
- `string launchSfxId`: Launch 시점 SFX.
- `string radialSplitImminentSfxId`: 분열 임박 SFX.
- `BossGraphEffectSettings effects`: 이펙트 설정 묶음.

패턴 탄 줄 표시: `radialSplitBulletCount`.

### CustomEventAction

메뉴: `Event/Custom Event`

동작: 보스 오브젝트 또는 자식 오브젝트에 `SendMessage`/`BroadcastMessage` 방식의 커스텀 이벤트를 보냅니다.

필드:
- `string methodName`: 호출할 메서드명.
- `bool broadcastToChildren`: 자식 오브젝트까지 브로드캐스트할지 여부.

### SpawnPrefabAction

메뉴: `Spawn/Spawn Prefab`

동작: 프리팹을 보스 기준 위치에 생성합니다. 필요하면 보스 자식으로 붙이고 일정 시간 뒤 제거합니다.

필드:
- `GameObject prefab`: 생성할 프리팹.
- `Vector2 positionOffset`: 생성 위치 오프셋.
- `Vector3 rotationEuler`: 생성 회전값.
- `bool parentToBoss`: 생성물을 보스 자식으로 붙일지 여부.
- `float destroyAfterSeconds`: 0보다 크면 해당 시간 뒤 제거.

## Move

### MoveTowardPlayerAction

메뉴: `Move/Move Toward Player`

동작: 지정 시간 동안 플레이어 방향으로 이동합니다.

필드:
- `float seconds`: 이동 시간.
- `float speedMultiplier`: 보스 기본 이동 속도 배율.
- `bool stopWhenFinished`: 종료 시 속도를 0으로 만들지 여부.

### MoveBodyRootLocalAction

메뉴: `Move/Move Body Root Local`

동작: 보스 `BodyRoot`의 로컬 오프셋을 지정 값까지 이동시킵니다.

필드:
- `Vector3 targetLocalOffset`: 목표 로컬 오프셋.
- `float duration`: 이동 시간.
- `bool releaseBaseAfterMove`: 이동 후 저장된 기준 로컬 위치를 해제할지 여부.

### ResetBodyRootLocalAction

메뉴: `Move/Reset Body Root Local`

동작: 보스 `BodyRoot` 로컬 오프셋을 초기화합니다.

필드: 없음.

## Animation

### PlayAnimationAction

메뉴: `Animation/Play Animation`

동작: Animator Trigger를 실행하거나 Animator State를 직접 재생합니다.

필드:
- `BossAnimationPlayMode playMode`: `Trigger`, `State`.
- `string triggerName`: `Trigger` 모드에서 실행할 트리거 이름.
- `string stateName`: `State` 모드에서 재생할 스테이트 이름.
- `int layer`: `State` 모드에서 사용할 Animator 레이어.
- `float normalizedTime`: `State` 모드에서 재생 시작 normalized time.

### WaitForAnimationEventAction

메뉴: `Animation/Wait For Event`

동작: 지정한 애니메이션 이벤트 ID가 올 때까지 대기합니다.

필드:
- `string eventId`: 기다릴 이벤트 ID.
- `float timeoutSeconds`: 0이면 이벤트가 올 때까지 계속 대기합니다.

## Utility

### WaitAction

메뉴: `Utility/Wait`

동작: 지정 시간 동안 대기합니다.

필드:
- `float seconds`: 대기 시간.

### AimBossChildAtPlayerAction

메뉴: `Utility/Aim Boss Child At Player`

동작: Start/End 노드 쌍으로 보스 자식 오브젝트가 플레이어를 바라보도록 유지하거나 해제합니다.

필드:
- `BossChildAimActionMode mode`: `Start`, `End`.
- `string startNodeId`: `End` 모드에서 종료할 Start 노드 ID.
- `string targetPath`: `Start` 모드에서 조준시킬 보스 자식 경로.
- `bool activateOnStart`: Start 시 대상 오브젝트를 활성화할지 여부.
- `bool flipYByFacing`: 바라보는 방향에 따라 Y축 플립할지 여부.
- `bool deactivateOnEnd`: End 시 대상 오브젝트를 비활성화할지 여부.
- `bool deactivateOnPatternEnd`: 패턴 종료 시 대상 오브젝트를 비활성화할지 여부.

`End` 모드에서는 `startNodeId`, `deactivateOnEnd`, `deactivateOnPatternEnd`만 의미가 있고, 나머지 Start용 필드는 에디터에서 비활성화됩니다.
