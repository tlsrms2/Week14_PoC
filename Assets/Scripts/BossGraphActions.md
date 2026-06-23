# Boss Graph Actions

현재 Boss Graph는 **노드 하나가 액션 하나를 직접 가진다**. 노드 이름은 별도 입력값이 아니라 설정된 액션 타입 이름으로 표시된다.

노드 타입은 `BossGraphNodeKind` 기준으로 `Attack`, `Move`, `Animation`, `Utility`가 있으며, `BossGraphActionCategoryAsset`에서 액션별 타입을 조정할 수 있다. 기본 타입 기준으로 노드의 액션 선택 목록이 필터링된다.

## 공통 설정

### BossGraphProjectileOriginSpec

투사체나 이펙트가 생성될 기준 위치를 정한다.

필드:
- `mode`: `BossOrigin`, `BossChild`, `BossChildList`, `AlternatingBossChildList`, `AlternatingBossChildren`.
- `bossChildPath`: 단일 보스 자식 경로.
- `bossChildPaths`: 여러 보스 자식 경로.
- `firstBossChildPath`: 교대 사용 첫 번째 경로.
- `secondBossChildPath`: 교대 사용 두 번째 경로.
- `fallbackSpacing`: `BossOrigin`일 때 여러 발을 좌우로 벌리는 기본 간격.

### BossGraphProjectileAimSpec

투사체가 향할 방향을 정한다.

필드:
- `mode`: `AtPlayer`, `FixedAngle`.
- `angleDegrees`: `FixedAngle`일 때 사용할 고정 각도.

### BossGraphEffectSettings

액션과 함께 재생할 공통 이펙트 묶음이다. SFX와 이펙트는 액션 안에 유지한다.

필드:
- `explosion`: 폭발 파티클 설정.
- `smoke`: 연기 파티클 설정.
- `smokeInterval`: 연기 반복 재생 간격.
- `muzzleFlash`: 총구 섬광 설정.
- `cameraShake`: 카메라 흔들림 설정.

`BossGraphParticleEffectSettings` 필드:
- `enabled`: 사용 여부.
- `color`: 색.
- `scale`: 크기.
- `count`: 개수.

`BossGraphCameraShakeSettings` 필드:
- `enabled`: 사용 여부.
- `seconds`: 지속 시간.
- `distance`: 흔들림 거리.
- `frequency`: 흔들림 주기.

## Attack

### FireProjectileAction

메뉴: `Projectile/Fire Projectile`

단발 투사체를 발사한다. 간단한 1발 발사용 액션이다.

필드:
- `projectileName`: 보스 인스펙터의 공통 투사체 이름.
- `projectile`: 그래프 내부 fallback 투사체 설정. 에디터에서는 숨김.
- `aimMode`: `Player`, `Angle`.
- `angleDegrees`: `aimMode`가 `Angle`일 때 발사 각도.
- `spawnRadius`: 발사 위치를 발사 방향으로 밀어낼 거리.

### FireProjectileBurstAction

메뉴: `Projectile/Fire Projectile Burst`

한 방향으로 여러 발을 연속 발사한다. 머신건류 패턴의 발사 부분을 담당한다.

필드:
- `projectileName`: 보스 인스펙터의 공통 투사체 이름.
- `projectile`: 그래프 내부 fallback 투사체 설정. 에디터에서는 숨김.
- `origin`: 발사 기준 위치.
- `aim`: 발사 방향.
- `spawnForwardOffset`: 발사 위치를 최종 방향 앞으로 밀어낼 거리.
- `fireSfxId`: 발사 즉시 재생할 SFX.
- `launchSfxId`: 투사체 Launch 시점에 재생할 SFX.
- `effects`: 발사 이펙트 묶음.
- `cameraShakeDirection`: 카메라 흔들림 방향.
- `volleys`: 발리 목록.

`Volley` 필드:
- `bulletCount`: 해당 발리의 발사 수.
- `fireInterval`: 같은 발리 안에서 발 사이 간격.
- `restSeconds`: 다음 발리 전 대기 시간.

### FireRadialEmissionAction

메뉴: `Projectile/Fire Radial Emission`

원형 또는 부채꼴로 투사체를 방사한다. 기존 `FireRadialProjectilesAction`, `FireRotatingProjectilesAction`의 대체 액션이다.

필드:
- `projectileName`: 보스 인스펙터의 공통 투사체 이름.
- `projectile`: 그래프 내부 fallback 투사체 설정. 에디터에서는 숨김.
- `origin`: 방사 중심 위치.
- `aim`: 기준 방향. 보통 플레이어 방향 또는 고정 각도.
- `bulletCount`: 발사 수.
- `arcDegrees`: 전체 방사 각도. `360`이면 원형.
- `startAngleOffset`: 시작 각도 보정값.
- `randomizeStartAngle`: 시작 각도를 매번 랜덤화할지 여부.
- `spawnRadius`: 중심에서 각 발사 방향으로 밀어낼 거리.
- `fireInterval`: 발 사이 발사 간격.
- `fireSfxId`: 발사 즉시 재생할 SFX.
- `launchSfxId`: 투사체 Launch 시점에 재생할 SFX.
- `effects`: 발사 이펙트 묶음.
- `cameraShakeDirection`: 카메라 흔들림 방향.

### FireSweepEmissionAction

메뉴: `Projectile/Fire Sweep Emission`

기준 방향을 좌우로 흔들며 연속 발사한다. 준비 시간은 `WindupAction`과 분리해서 구성한다.

필드:
- `projectileName`: 보스 인스펙터의 공통 투사체 이름.
- `projectile`: 그래프 내부 fallback 투사체 설정. 에디터에서는 숨김.
- `origin`: 발사 기준 위치.
- `aim`: 기준 방향.
- `bulletCount`: 발사 수.
- `fireInterval`: 발 사이 발사 간격.
- `spawnSpacing`: 발사 위치를 좌우로 번갈아 벌리는 간격.
- `sweepStepDegrees`: 매 발마다 바뀌는 각도.
- `maxSweepAngle`: 좌우 최대 스윕 각도.
- `fireSfxId`: 발사 즉시 재생할 SFX.
- `launchSfxId`: 투사체 Launch 시점에 재생할 SFX.
- `effects`: 발사 이펙트 묶음.

### FireFanEmissionAction

메뉴: `Projectile/Fire Fan Emission`

부채꼴 발리를 여러 번 발사한다. 준비 시간은 `WindupAction`과 분리해서 구성한다.

필드:
- `projectileName`: 보스 인스펙터의 공통 투사체 이름.
- `projectile`: 그래프 내부 fallback 투사체 설정. 에디터에서는 숨김.
- `origin`: 발사 기준 위치.
- `aim`: 발리 기준 방향.
- `volleyCount`: 발리 반복 수.
- `projectilesPerVolley`: 발리당 투사체 수.
- `volleyInterval`: 발리 사이 간격.
- `fanAngleDegrees`: 부채꼴 전체 각도.
- `spawnSpacing`: 발사 위치를 좌우로 벌리는 간격.
- `fireSfxId`: 발리 발사 SFX.
- `launchSfxId`: 투사체 Launch 시점 SFX.
- `effects`: 발사 이펙트 묶음.

### SpawnChargedProjectileAction

메뉴: `Projectile/Spawn Charged Projectile`

차징 투사체를 생성하고 핸들에 저장한다. 이후 성장, 분열, 차징 종료 대기 액션이 같은 핸들을 사용한다.

필드:
- `handleKey`: 이후 액션이 참조할 투사체 핸들 이름.
- `projectileName`: 보스 인스펙터의 공통 투사체 이름.
- `projectile`: 그래프 내부 fallback 투사체 설정. 에디터에서는 숨김.
- `projectileOriginPath`: 차징 투사체 생성 위치.
- `chargeSeconds`: 차징 시간.
- `projectileRadiusMultiplier`: 투사체 반지름 배율.
- `aimSpreadDegrees`: 최초 조준 랜덤 퍼짐 각도.
- `launchSfxId`: 투사체 Launch 시점 SFX.
- `effects`: 생성 이펙트 묶음.

### ConfigureProjectileGrowthAction

메뉴: `Projectile/Configure Projectile Growth`

핸들에 저장된 차징 투사체의 크기 성장을 설정한다.

필드:
- `handleKey`: 대상 투사체 핸들 이름.
- `startScaleMultiplier`: 시작 스케일 배율.
- `finalScaleMultiplier`: 최종 스케일 배율.

### ConfigureRadialSplitAction

메뉴: `Projectile/Configure Radial Split`

핸들에 저장된 차징 투사체가 Launch 이후 방사형으로 분열되도록 설정한다.

필드:
- `handleKey`: 대상 투사체 핸들 이름.
- `radialSplitBulletCount`: 분열 투사체 수.
- `radialSplitStartAngleOffset`: 분열 시작 각도 보정값.
- `splitDelaySeconds`: Launch 이후 분열까지 지연 시간.
- `splitSpeedMultiplier`: 분열 투사체 속도 배율.
- `splitRadiusMultiplier`: 분열 투사체 반지름 배율.
- `splitLifetimeMultiplier`: 분열 투사체 수명 배율.
- `splitSfxLeadSeconds`: 분열 예고 SFX 선행 시간.
- `splitImminentSfxId`: 분열 예고 SFX.

### WaitProjectileChargeEndAction

메뉴: `Projectile/Wait Projectile Charge End`

핸들에 저장된 차징 투사체가 차징을 끝낼 때까지 대기한다.

필드:
- `handleKey`: 대상 투사체 핸들 이름.
- `projectileOriginPath`: 차징 중 연기와 Launch 이펙트 기준 위치.
- `aimTrackingSeconds`: 차징 중 플레이어 추적을 유지할 시간.
- `aimSpreadDegrees`: 추적 중단 시 적용할 조준 퍼짐 각도.
- `effects`: 차징 중 연기와 Launch 이펙트 묶음.

## Move

### MoveTowardPlayerAction

메뉴: `Move/Move Toward Player`

지정 시간 동안 플레이어 방향으로 이동한다.

필드:
- `seconds`: 이동 시간.
- `speedMultiplier`: 보스 기본 이동 속도 배율.
- `stopWhenFinished`: 종료 시 이동을 멈출지 여부.

### StartMoveTowardPlayerAction

메뉴: `Move/Start Move Toward Player`

이후 대기나 발사 액션이 실행되는 동안 플레이어 방향 이동을 계속 켠다.

필드:
- `speedMultiplier`: 보스 기본 이동 속도 배율.

### StopMovementAction

메뉴: `Move/Stop Movement`

`StartMoveTowardPlayerAction`으로 켠 지속 이동을 끈다.

필드: 없음.

### MoveBodyRootLocalAction

메뉴: `Move/Move Body Root Local`

보스 `BodyRoot`의 로컬 오프셋을 목표값까지 이동시킨다.

필드:
- `targetLocalOffset`: 목표 로컬 오프셋.
- `duration`: 이동 시간.
- `releaseBaseAfterMove`: 이동 후 저장된 기준 로컬 위치를 해제할지 여부.

### ResetBodyRootLocalAction

메뉴: `Move/Reset Body Root Local`

보스 `BodyRoot`의 로컬 오프셋을 초기화한다.

필드: 없음.

## Animation

### PlayAnimationAction

메뉴: `Animation/Play Animation`

Animator Trigger를 실행하거나 Animator State를 직접 재생한다.

필드:
- `playMode`: `Trigger`, `State`.
- `triggerName`: `Trigger` 모드에서 실행할 트리거 이름.
- `stateName`: `State` 모드에서 재생할 상태 이름.
- `layer`: `State` 모드에서 사용할 Animator 레이어.
- `normalizedTime`: `State` 모드에서 재생 시작 normalized time.

### WaitForAnimationEventAction

메뉴: `Animation/Wait For Event`

지정한 애니메이션 이벤트 ID가 들어올 때까지 대기한다.

필드:
- `eventId`: 기다릴 이벤트 ID.
- `timeoutSeconds`: `0`이면 이벤트가 올 때까지 계속 대기한다.

## Utility

### WaitAction

메뉴: `Utility/Wait`

지정 시간 동안 대기한다. 지속 이동이 켜져 있으면 대기 중에도 `BossActionContext`가 지속 이동을 갱신한다.

필드:
- `seconds`: 대기 시간.

### WindupAction

메뉴: `Utility/Windup`

공격 전 준비 시간을 담당한다. 준비 중 이동 정지와 연기 이펙트를 처리할 수 있다.

필드:
- `seconds`: 준비 시간.
- `stopMovement`: 준비 중 보스 이동을 멈출지 여부.
- `effectOrigin`: 준비 이펙트 기준 위치.
- `effects`: 준비 이펙트 묶음.

### AimBossChildAtPlayerAction

메뉴: `Utility/Aim Boss Child At Player`

보스 자식 오브젝트가 플레이어를 바라보도록 켜거나 끈다. `Start` 노드와 `End` 노드 한 쌍으로 사용한다.

필드:
- `mode`: `Start`, `End`.
- `startNodeId`: `End` 모드에서 종료할 `Start` 노드.
- `targetPath`: `Start` 모드에서 조준시킬 보스 자식 경로.
- `activateOnStart`: `Start` 시 대상 오브젝트를 활성화할지 여부.
- `flipYByFacing`: 바라보는 방향에 따라 Y축 플립을 적용할지 여부.
- `deactivateOnEnd`: `End` 시 대상 오브젝트를 비활성화할지 여부.
- `deactivateOnPatternEnd`: 패턴 종료 시 대상 오브젝트를 비활성화할지 여부.

`End` 모드에서는 종료 대상 선택과 비활성화 옵션만 의미가 있고, 조준 대상 설정은 `Start` 노드의 값을 사용한다.

## 기타 메뉴 액션

아래 액션은 메뉴 경로는 별도지만, 기본 타입 매핑에서는 `Attack`으로 분류된다. 필요하면 `BossGraphActionCategoryAsset`에서 `Utility` 등으로 재분류한다.

### CustomEventAction

메뉴: `Event/Custom Event`

보스 오브젝트에 `SendMessage` 또는 `BroadcastMessage` 방식으로 커스텀 이벤트를 보낸다.

필드:
- `methodName`: 호출할 메서드 이름.
- `broadcastToChildren`: 자식 오브젝트까지 `BroadcastMessage`를 보낼지 여부.

### SpawnPrefabAction

메뉴: `Spawn/Spawn Prefab`

프리팹을 보스 기준 위치에 생성한다. 필요하면 보스 자식으로 붙이고 일정 시간 뒤 제거한다.

필드:
- `prefab`: 생성할 프리팹.
- `positionOffset`: 생성 위치 오프셋.
- `rotationEuler`: 생성 회전값.
- `parentToBoss`: 생성물을 보스 자식으로 붙일지 여부.
- `destroyAfterSeconds`: `0`보다 크면 해당 시간 뒤 제거한다.

## Legacy Composite Actions

아래 액션들은 기존 Hog 패턴 호환 또는 마이그레이션용으로만 남아 있다. 새 그래프에서는 작은 액션 조합으로 대체한다.

- `FireRadialProjectilesAction` -> `FireRadialEmissionAction`
- `FireRotatingProjectilesAction` -> `FireRadialEmissionAction`
- `FireMachinegunProjectilesAction` -> `StartMoveTowardPlayerAction` + `FireProjectileBurstAction` + `StopMovementAction`
- `FireSweepProjectilesAction` -> `WindupAction` + `FireSweepEmissionAction`
- `FireFanVolleyProjectilesAction` -> `WindupAction` + `FireFanEmissionAction` + 필요 시 `FireProjectileBurstAction`
- `FireChargedRadialSplitProjectileAction` -> `SpawnChargedProjectileAction` + `ConfigureProjectileGrowthAction` + `ConfigureRadialSplitAction` + `WaitProjectileChargeEndAction`
