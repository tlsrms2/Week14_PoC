# Minion Graph Actions Guide

Boss Graph에서 `Type`을 `Minion`으로 둔 노드에서 선택하는 액션 목록입니다.
Minion 액션은 특정 보스 클래스를 직접 참조하지 않고, 모든 `BossAI`가 공통으로 구현한 `IMinionPatternHost`를 통해 소환, 이동, 발사, 정리를 요청합니다.
새 보스가 Minion을 쓰려면 보스 인스펙터의 설정 탭에서 `소환수 사용`을 켜고 소환수 설정을 채우면 됩니다.

## 공통 규칙

- 투사체를 발사하는 액션은 항상 `Origin`, `Aim`, `Effects` 계열 필드를 가집니다.
- `소환수 사용`이 꺼진 보스에서는 Minion 노드가 실행되지 않습니다.
- 소환하지 않고 씬에 배치된 미니언만 지휘하려면 보스 설정 탭의 소환수 설정에서 `prefab`은 비워두고 `claimSceneMinions`를 켭니다.
- 미니언만 발사하는 액션은 `minionOrigin`, `aim`, `effects`를 사용합니다.
- 소환자만 발사하는 액션은 `ownerOrigin`, `aim`, `effects`를 사용합니다.
- 소환자와 미니언이 함께 발사하는 액션은 `ownerOrigin`, `minionOrigin`, `aim`, `effects`를 사용합니다.
- `projectileName`, `bossProjectileName`, `minionProjectileName`, `orbitProjectileName`, `stationaryProjectileName`은 호스트 보스의 Boss Graph 투사체 목록에 있는 `Name`입니다.
- 투사체 이름 해석은 호스트 보스의 `ResolveMinionProjectileSettings` 구현을 따릅니다. 결과가 `null`이면 해당 액션은 실행되지 않습니다.
- 패턴 끝에는 보통 `Minion/Control/Pattern Cleanup`을 붙여 명령 대기, 동기화 발사 해제, 정지, 대기 복귀를 정리합니다.

## 발사 공통 필드

| Field | 의미 |
| --- | --- |
| `ownerOrigin` | 소환자 본체가 투사체를 발사할 기준 위치입니다. 보스 원점, 보스 자식, 자식 목록, 교대 자식을 선택할 수 있습니다. |
| `minionOrigin` | 미니언이 투사체를 발사할 기준 위치입니다. 미니언의 Projectile Origin, 루트, 자식, 자식 목록, 교대 자식을 선택할 수 있습니다. |
| `aim` | 발사 방향입니다. 플레이어 조준 또는 고정 각도를 선택할 수 있습니다. |
| `effects` | 발사 지점 폭발/연기, 총구 섬광, 카메라 흔들림 설정입니다. |

## Minion/Spawn/Auto Summon If Needed

호스트 보스의 자동 소환 조건을 확인하고 필요할 때만 기본 소환 수만큼 미니언을 보충합니다. 별도 필드는 없습니다.

## Minion/Spawn/Summon

| Field | 기본값 | 의미 |
| --- | --- | --- |
| `summonCount` | `0` | 이번 액션에서 소환할 미니언 수입니다. `0`이면 호스트 보스의 기본 소환 수를 사용합니다. |

## Minion/Spawn/Ensure Count

| Field | 기본값 | 의미 |
| --- | --- | --- |
| `targetCount` | `1` | 최소 보유 미니언 수입니다. 이미 충분하면 바로 통과합니다. |

## Minion/Fire/Fire All

현재 관리 중인 모든 미니언이 같은 타이밍에 발사합니다.

| Field | 기본값 | 의미 |
| --- | --- | --- |
| `projectileName` | `Default` | 미니언이 발사할 투사체 이름입니다. |
| `minionOrigin` | `ProjectileOrigin` | 각 미니언의 발사 위치입니다. |
| `aim` | `AtPlayer` | 각 미니언의 조준 방향입니다. |
| `effects` | 꺼짐 | 발사 이펙트입니다. |
| `shotCount` | `1` | 전체 미니언 반복 발사 횟수입니다. |
| `fireInterval` | `0` | 반복 발사 간격입니다. |

## Minion/Fire/Boss Burst

소환자가 연속 발사합니다. `notifyMinions`를 켜면 소환자가 한 발 쏠 때마다 미니언도 함께 발사합니다.

| Field | 기본값 | 의미 |
| --- | --- | --- |
| `bossProjectileName` | `Default` | 소환자가 발사할 투사체 이름입니다. |
| `minionProjectileName` | `Default` | `notifyMinions`가 켜졌을 때 미니언이 발사할 투사체 이름입니다. |
| `ownerOrigin` | `BossOrigin` | 소환자의 발사 위치입니다. |
| `minionOrigin` | `ProjectileOrigin` | 보조 발사 미니언의 발사 위치입니다. `notifyMinions`가 꺼져 있으면 사용하지 않습니다. |
| `aim` | `AtPlayer` | 소환자와 미니언의 조준 방향입니다. |
| `effects` | 꺼짐 | 소환자와 미니언 발사 이펙트입니다. |
| `windupSeconds` | `0.45` | 첫 발사 전 대기 시간입니다. |
| `bulletCount` | `5` | 소환자 발사 횟수입니다. |
| `fireInterval` | `0.18` | 소환자 반복 발사 간격입니다. |
| `spawnSpacing` | `0.12` | 소환자 연속 발사를 좌우로 벌리는 거리입니다. |
| `notifyMinions` | `false` | 켜면 소환자 발사마다 미니언도 한 번 발사합니다. |

## Minion/Fire/Synchronized Burst

소환자 연속 발사 타이밍에 맞춰 미니언이 동기화 발사합니다.

| Field | 기본값 | 의미 |
| --- | --- | --- |
| `bossProjectileName` | `Default` | 소환자가 발사할 투사체 이름입니다. |
| `minionProjectileName` | `Default` | 미니언이 별도 투사체를 쓸 때의 이름입니다. |
| `useBossProjectileForMinions` | `true` | 켜면 미니언도 `bossProjectileName` 투사체를 사용합니다. |
| `ownerOrigin` | `BossOrigin` | 소환자 발사 위치입니다. |
| `minionOrigin` | `ProjectileOrigin` | 미니언 발사 위치입니다. |
| `aim` | `AtPlayer` | 소환자와 미니언의 조준 방향입니다. |
| `effects` | 꺼짐 | 소환자와 미니언 발사 이펙트입니다. |
| `ensureMinionCount` | `1` | 발사 전 보장할 최소 미니언 수입니다. |
| `windupSeconds` | `0.45` | 첫 발사 전 대기 시간입니다. |
| `bulletCount` | `5` | 소환자와 미니언의 동기화 발사 횟수입니다. |
| `fireInterval` | `0.18` | 동기화 발사 간격입니다. |
| `spawnSpacing` | `0.12` | 소환자 연속 발사를 좌우로 벌리는 거리입니다. |

## Minion/Fire/Stop And Fire

모든 미니언을 정지시키고 반복 발사시킵니다.

| Field | 기본값 | 의미 |
| --- | --- | --- |
| `projectileName` | `Default` | 미니언이 발사할 투사체 이름입니다. |
| `minionOrigin` | `ProjectileOrigin` | 미니언 발사 위치입니다. |
| `aim` | `AtPlayer` | 미니언 조준 방향입니다. |
| `effects` | 꺼짐 | 발사 이펙트입니다. |
| `bulletCount` | `3` | 각 미니언의 발사 횟수입니다. |
| `fireInterval` | `0.2` | 각 미니언의 반복 발사 간격입니다. |
| `resumeIdle` | `true` | 종료 후 기본 대기 이동으로 복귀할지 여부입니다. |
| `waitForDuration` | `true` | 예상 발사 시간이 끝날 때까지 다음 노드로 넘어가지 않을지 여부입니다. |

## Minion/Fire/Radial Burst

모든 미니언이 자신 위치 기준으로 방사형 발리를 발사합니다.

| Field | 기본값 | 의미 |
| --- | --- | --- |
| `projectileName` | `Default` | 미니언이 발사할 투사체 이름입니다. |
| `minionOrigin` | `ProjectileOrigin` | 미니언 발사 위치입니다. |
| `aim` | `AtPlayer` | 방사 중심 방향입니다. |
| `effects` | 꺼짐 | 발사 이펙트입니다. |
| `volleyCount` | `1` | 방사 발리 반복 횟수입니다. |
| `directionCount` | `5` | 한 발리에서 발사할 방향 수입니다. |
| `volleyInterval` | `0.35` | 발리 사이 간격입니다. |
| `spreadDegrees` | `75` | 방사 각도입니다. `0`이면 전방향 처리로 사용합니다. |
| `resumeIdle` | `true` | 종료 후 기본 대기 이동으로 복귀할지 여부입니다. |
| `waitForDuration` | `true` | 예상 발리 시간이 끝날 때까지 대기할지 여부입니다. |

## Minion/Movement/Orbit Fire

모든 미니언이 플레이어 주변을 회전하며 일정 각도마다 발사합니다.

| Field | 기본값 | 의미 |
| --- | --- | --- |
| `projectileName` | `Default` | 미니언이 발사할 투사체 이름입니다. |
| `minionOrigin` | `ProjectileOrigin` | 미니언 발사 위치입니다. |
| `aim` | `AtPlayer` | 미니언 조준 방향입니다. |
| `effects` | 꺼짐 | 발사 이펙트입니다. |
| `orbitRadius` | `2.6` | 플레이어 기준 회전 반지름입니다. |
| `orbitSeconds` | `3` | 회전에 사용할 시간입니다. |
| `fireAngleStepDegrees` | `30` | 이 각도만큼 이동할 때마다 발사합니다. |
| `randomizeDirection` | `true` | 회전 방향을 랜덤으로 정합니다. |
| `clockwise` | `false` | 랜덤이 꺼져 있을 때 시계 방향 회전 여부입니다. |
| `waitForDuration` | `true` | 회전 시간이 끝날 때까지 대기할지 여부입니다. |

## Minion/Movement/Orbit Crossfire

첫 번째 미니언은 궤도 이동 발사, 나머지 미니언은 정지 반복 발사를 수행합니다.

| Field | 기본값 | 의미 |
| --- | --- | --- |
| `orbitProjectileName` | `Default` | 궤도 미니언이 발사할 투사체 이름입니다. |
| `stationaryProjectileName` | `Default` | 정지 미니언이 발사할 투사체 이름입니다. |
| `useOrbitProjectileForStationary` | `false` | 켜면 정지 미니언도 `orbitProjectileName`을 사용합니다. |
| `minionOrigin` | `ProjectileOrigin` | 미니언 발사 위치입니다. |
| `aim` | `AtPlayer` | 미니언 조준 방향입니다. |
| `effects` | 꺼짐 | 발사 이펙트입니다. |
| `minimumMinionCount` | `2` | 발사 전 보장할 최소 미니언 수입니다. |
| `orbitRadius` | `2.6` | 궤도 반지름입니다. |
| `orbitSeconds` | `3` | 궤도 이동 시간입니다. |
| `fireAngleStepDegrees` | `30` | 궤도 미니언의 발사 각도 간격입니다. |
| `randomizeDirection` | `true` | 궤도 방향을 랜덤으로 정합니다. |
| `clockwise` | `false` | 랜덤이 꺼져 있을 때 시계 방향 회전 여부입니다. |
| `stationaryBulletCount` | `5` | 정지 미니언의 발사 횟수입니다. |
| `stationaryFireInterval` | `0.25` | 정지 미니언의 반복 발사 간격입니다. |
| `resumeIdle` | `true` | 종료 후 기본 대기 이동으로 복귀할지 여부입니다. |

## Minion/Movement/Charge Side Fire

미니언들이 플레이어 방향에서 좌우로 비껴 돌진하며 측면으로 발사합니다.

| Field | 기본값 | 의미 |
| --- | --- | --- |
| `projectileName` | `Default` | 미니언이 발사할 투사체 이름입니다. |
| `minionOrigin` | `ProjectileOrigin` | 미니언 발사 위치입니다. |
| `aim` | `AtPlayer` | 돌진 기준 방향입니다. |
| `effects` | 꺼짐 | 발사 이펙트입니다. |
| `chargeSeconds` | `1` | 돌진 지속 시간입니다. |
| `chargeSpeed` | `7` | 돌진 속도입니다. |
| `aimOffsetDegrees` | `22` | 기준 방향에서 좌우로 틀어지는 각도입니다. |
| `sideFireInterval` | `0.18` | 돌진 중 측면 발사 간격입니다. |
| `sideFireAngleDegrees` | `90` | 돌진 방향 기준 측면 발사 각도입니다. |
| `waitForDuration` | `true` | 돌진 시간이 끝날 때까지 대기할지 여부입니다. |

## Minion/Movement/Formation

미니언을 플레이어 주변 진형 위치로 이동시킵니다. 발사하지 않습니다.

| Field | 기본값 | 의미 |
| --- | --- | --- |
| `radius` | `2.8` | 플레이어 기준 진형 반지름입니다. |
| `angleSpacingDegrees` | `28` | 미니언들이 좌우로 벌어지는 각도 간격입니다. |
| `speedMultiplier` | `1.2` | 진형 위치로 이동하는 속도 배율입니다. |
| `settleSeconds` | `1` | 진형에 맞춘 뒤 대기할 시간입니다. |
| `waitForDuration` | `true` | `settleSeconds`만큼 다음 노드로 넘어가지 않을지 여부입니다. |

## Minion/Movement/Formation Barrage

미니언을 진형에 배치한 뒤 모든 미니언이 반복 발사합니다.

| Field | 기본값 | 의미 |
| --- | --- | --- |
| `projectileName` | `Default` | 미니언이 발사할 투사체 이름입니다. |
| `minionOrigin` | `ProjectileOrigin` | 미니언 발사 위치입니다. |
| `aim` | `AtPlayer` | 미니언 조준 방향입니다. |
| `effects` | 꺼짐 | 발사 이펙트입니다. |
| `minimumMinionCount` | `1` | 발사 전 보장할 최소 미니언 수입니다. |
| `formationRadius` | `2.8` | 플레이어 기준 진형 반지름입니다. |
| `formationAngleSpacingDegrees` | `28` | 미니언들이 좌우로 벌어지는 각도 간격입니다. |
| `formationSpeedMultiplier` | `1.2` | 진형 위치로 이동하는 속도 배율입니다. |
| `settleSeconds` | `1` | 진형 연출 전체 시간입니다. |
| `preFormationDelayRatio` | `0.5` | 진형 명령 전 먼저 기다리는 비율입니다. |
| `fireCount` | `6` | 진형 후 전체 미니언 발사 횟수입니다. |
| `fireInterval` | `0.22` | 발사 반복 간격입니다. |
| `resumeIdle` | `true` | 종료 후 기본 대기 이동으로 복귀할지 여부입니다. |

## Minion/Control/Command

미니언 명령을 하나의 액션에서 모드로 선택하는 범용 액션입니다. 빠른 테스트용이며, 실제 패턴은 전용 액션을 우선 사용합니다.

| Field | 기본값 | 적용 모드 | 의미 |
| --- | --- | --- | --- |
| `mode` | `StopAndFire` | 전체 | 실행할 명령입니다. |
| `projectileName` | `Default` | Formation 제외 | 발사할 투사체 이름입니다. |
| `minionOrigin` | `ProjectileOrigin` | Formation 제외 | 미니언 발사 위치입니다. |
| `aim` | `AtPlayer` | Formation 제외 | 미니언 조준 방향입니다. |
| `effects` | 꺼짐 | Formation 제외 | 발사 이펙트입니다. |
| `repeatCount` | `3` | StopAndFire, RadialBurst | 반복 발사 또는 발리 횟수입니다. |
| `fireInterval` | `0.2` | StopAndFire, RadialBurst | 반복 사이 간격입니다. |
| `directionCount` | `5` | RadialBurst | 방사 방향 수입니다. |
| `spreadDegrees` | `75` | RadialBurst | 방사 각도입니다. |
| `orbitRadius` | `2.6` | OrbitFire | 궤도 반지름입니다. |
| `orbitSeconds` | `3` | OrbitFire | 궤도 이동 시간입니다. |
| `fireAngleStepDegrees` | `30` | OrbitFire | 궤도 발사 각도 간격입니다. |
| `randomizeOrbitDirection` | `true` | OrbitFire | 회전 방향을 랜덤으로 정합니다. |
| `clockwise` | `false` | OrbitFire | 랜덤이 꺼졌을 때 시계 방향 회전 여부입니다. |
| `chargeSeconds` | `1` | ChargeSideFire | 돌진 지속 시간입니다. |
| `chargeSpeed` | `7` | ChargeSideFire | 돌진 속도입니다. |
| `aimOffsetDegrees` | `22` | ChargeSideFire | 기준 방향에서 틀어지는 각도입니다. |
| `sideFireInterval` | `0.18` | ChargeSideFire | 측면 발사 간격입니다. |
| `sideFireAngleDegrees` | `90` | ChargeSideFire | 측면 발사 각도입니다. |
| `formationRadius` | `2.8` | Formation | 진형 반지름입니다. |
| `formationAngleSpacingDegrees` | `28` | Formation | 진형 각도 간격입니다. |
| `formationSpeedMultiplier` | `1.2` | Formation | 진형 이동 속도 배율입니다. |
| `settleSeconds` | `1` | Formation | 진형 대기 시간입니다. |
| `resumeIdle` | `true` | StopAndFire, RadialBurst | 종료 후 기본 대기 이동으로 복귀할지 여부입니다. |
| `waitForDuration` | `true` | 전체 | 예상 실행 시간 동안 다음 노드로 넘어가지 않을지 여부입니다. |

## Minion/Control/Wait Commands

| Field | 기본값 | 의미 |
| --- | --- | --- |
| `timeoutSeconds` | `0` | 현재 미니언 명령이 끝날 때까지 기다릴 최대 시간입니다. `0`이면 제한 없이 기다립니다. |

## Minion/Control/Clear Synchronized Fire

예약된 동기화 발사 상태를 지웁니다. 별도 필드는 없습니다.

## Minion/Control/Pattern Cleanup

| Field | 기본값 | 의미 |
| --- | --- | --- |
| `waitForCommands` | `true` | 현재 미니언 명령이 끝날 때까지 기다립니다. |
| `waitTimeoutSeconds` | `0` | 명령 대기 제한 시간입니다. `0`이면 제한 없이 기다립니다. |
| `clearSynchronizedFire` | `true` | 남아 있는 동기화 발사 예약을 지웁니다. |
| `stopAllMinions` | `false` | 모든 미니언의 현재 명령을 즉시 중지합니다. |
| `resumeIdle` | `true` | 모든 미니언을 기본 대기 이동으로 복귀시킵니다. |

## Minion/Control/Stop All

모든 미니언의 현재 명령을 즉시 중지합니다. 별도 필드는 없습니다.

## Minion/Control/Resume Idle

모든 미니언을 기본 대기 이동 상태로 되돌립니다. 별도 필드는 없습니다.
