# Minion Graph Actions Guide

Boss Graph에서 `Type`이 `Minion`인 노드에 넣는 액션 목록입니다.
Minion 액션은 특정 보스 클래스를 직접 참조하지 않고, 현재 보스가 구현한 `IMinionPatternHost`를 통해 미니언 소환, 이동, 발사, 정리를 요청합니다.
다른 보스에서 재사용하려면 해당 보스가 `IMinionPatternHost`를 구현해야 합니다.

## 공통 규칙

- 액션 경로는 그래프 에디터의 `Minion` 분류 기준입니다.
- 현재 보스가 `IMinionPatternHost`를 구현하지 않으면 Minion 액션은 실행할 대상이 없어 바로 종료됩니다.
- `projectileName`, `bossProjectileName`, `minionProjectileName`, `orbitProjectileName`, `stationaryProjectileName`은 호스트 보스의 `Boss Graph 투사체` 목록에 있는 `Name`입니다.
- 투사체 이름 해석과 fallback은 호스트 보스의 `ResolveMinionProjectileSettings` 구현을 따릅니다. 최종 결과가 `null`이면 해당 액션은 실행되지 않습니다.
- `summonCount`가 `0`이면 호스트 보스의 기본 소환 수를 사용합니다.
- `targetCount`, `ensureMinionCount`, `minimumMinionCount`가 `0`이면 미니언 수 보장을 생략합니다.
- `waitForDuration`이 켜져 있으면 액션이 계산한 예상 시간 동안 다음 노드로 넘어가지 않습니다.
- `resumeIdle`이 켜져 있으면 액션 종료 후 미니언을 기본 대기 이동 상태로 되돌립니다.
- 패턴 끝에는 보통 `Minion/Control/Pattern Cleanup`을 붙여 명령 대기, 동기화 발사 해제, 정지, 대기 복귀를 한 번에 정리합니다.

## 추천 조합

- 단순 보스 연사: `Minion/Spawn/Auto Summon If Needed` -> `Minion/Fire/Boss Burst` -> `Minion/Control/Pattern Cleanup`
- 보스와 미니언 동기화 사격: `Minion/Spawn/Auto Summon If Needed` -> `Minion/Fire/Synchronized Burst` -> `Minion/Control/Pattern Cleanup`
- 궤도 미니언 + 고정 미니언 교차 사격: `Minion/Spawn/Auto Summon If Needed` -> `Minion/Movement/Orbit Crossfire` -> `Minion/Control/Pattern Cleanup`
- 미니언 전체 방사 사격: `Minion/Spawn/Auto Summon If Needed` -> `Minion/Fire/Radial Burst` -> `Minion/Control/Pattern Cleanup`
- 미니언 돌진 측면 사격: `Minion/Spawn/Auto Summon If Needed` -> `Minion/Movement/Charge Side Fire` -> `Minion/Control/Pattern Cleanup`
- 진형 배치 후 포격: `Minion/Spawn/Auto Summon If Needed` -> `Minion/Movement/Formation Barrage` -> `Minion/Control/Pattern Cleanup`

## Minion/Spawn/Auto Summon If Needed

호스트 보스의 자동 소환 조건을 확인하고, 필요할 때만 기본 소환 수만큼 미니언을 소환합니다.
필드는 없습니다.

## Minion/Spawn/Summon

호스트 보스의 소환 로직으로 미니언을 소환합니다.

| Field | 기본값 | 의미 |
| --- | --- | --- |
| `summonCount` | `0` | 이번 액션에서 소환할 미니언 수입니다. `0`이면 호스트 보스의 기본 소환 수를 사용합니다. |

## Minion/Spawn/Ensure Count

패턴 시작 전에 필요한 미니언 수를 보장합니다. 이미 충분하면 바로 통과합니다.

| Field | 기본값 | 의미 |
| --- | --- | --- |
| `targetCount` | `1` | 이 수만큼 소유 미니언이 있도록 보장합니다. `0`이면 보장하지 않습니다. |

## Minion/Fire/Fire All

현재 소유한 모든 미니언이 플레이어 방향으로 같은 투사체를 발사합니다.

| Field | 기본값 | 의미 |
| --- | --- | --- |
| `projectileName` | `Default` | 미니언이 발사할 투사체 이름입니다. |
| `shotCount` | `1` | 모든 미니언이 반복 발사할 횟수입니다. 최소 `1`로 보정됩니다. |
| `fireInterval` | `0` | 반복 발사 사이 간격입니다. `0`이면 연속으로 발사합니다. |

## Minion/Fire/Boss Burst

호스트 보스 본체가 연속 발사합니다. `notifyMinions`를 켜면 보스가 한 발 쏠 때마다 모든 미니언도 함께 발사합니다.

| Field | 기본값 | 의미 |
| --- | --- | --- |
| `bossProjectileName` | `Default` | 보스 본체가 발사할 투사체 이름입니다. |
| `minionProjectileName` | `Default` | `notifyMinions`가 켜졌을 때 미니언이 발사할 투사체 이름입니다. |
| `windupSeconds` | `0.45` | 첫 발사 전 대기 시간입니다. |
| `bulletCount` | `5` | 보스 본체가 연속 발사할 탄 수입니다. |
| `fireInterval` | `0.18` | 보스 본체 연속 발사 간격입니다. |
| `spawnSpacing` | `0.12` | 보스 탄환을 발사 방향의 좌우로 번갈아 벌리는 거리입니다. |
| `notifyMinions` | `false` | 켜면 보스가 한 발 쏠 때마다 모든 미니언도 한 번 발사합니다. |

## Minion/Fire/Synchronized Burst

보스 본체 발사 타이밍에 맞춰 미니언이 동기화 발사합니다.

| Field | 기본값 | 의미 |
| --- | --- | --- |
| `bossProjectileName` | `Default` | 보스 본체가 발사할 투사체 이름입니다. |
| `minionProjectileName` | `Default` | `useBossProjectileForMinions`가 꺼졌을 때 미니언이 발사할 투사체 이름입니다. |
| `useBossProjectileForMinions` | `true` | 켜면 미니언도 `bossProjectileName` 투사체를 사용합니다. |
| `ensureMinionCount` | `1` | 패턴 시작 전에 보장할 최소 미니언 수입니다. `0`이면 보장하지 않습니다. |
| `windupSeconds` | `0.45` | 첫 발사 전 대기 시간입니다. |
| `bulletCount` | `5` | 보스 발사 횟수이자 미니언 동기화 발사 횟수입니다. |
| `fireInterval` | `0.18` | 동기화 발사 간격입니다. |
| `spawnSpacing` | `0.12` | 보스 탄환을 발사 방향의 좌우로 번갈아 벌리는 거리입니다. |

## Minion/Fire/Stop And Fire

모든 미니언을 정지시키고 플레이어 방향으로 반복 발사시킵니다.

| Field | 기본값 | 의미 |
| --- | --- | --- |
| `projectileName` | `Default` | 발사할 투사체 이름입니다. |
| `bulletCount` | `3` | 각 미니언이 발사할 탄 수입니다. 최소 `1`로 보정됩니다. |
| `fireInterval` | `0.2` | 각 미니언의 반복 발사 간격입니다. |
| `resumeIdle` | `true` | 발사 종료 후 기본 대기 이동으로 복귀할지 여부입니다. |
| `waitForDuration` | `true` | 발사 예상 시간이 끝날 때까지 다음 노드로 넘어가지 않을지 여부입니다. |

## Minion/Movement/Orbit Fire

모든 미니언이 플레이어 주변을 회전하며 일정 각도마다 발사합니다.

| Field | 기본값 | 의미 |
| --- | --- | --- |
| `projectileName` | `Default` | 발사할 투사체 이름입니다. |
| `orbitRadius` | `2.6` | 플레이어 기준 회전 반지름입니다. |
| `orbitSeconds` | `3` | 회전에 사용할 시간입니다. |
| `fireAngleStepDegrees` | `30` | 회전 중 이 각도만큼 이동할 때마다 발사합니다. |
| `randomizeDirection` | `true` | 켜면 시계/반시계 방향을 랜덤으로 정합니다. |
| `clockwise` | `false` | `randomizeDirection`이 꺼져 있을 때 시계 방향으로 회전할지 여부입니다. |
| `waitForDuration` | `true` | 회전 시간이 끝날 때까지 다음 노드로 넘어가지 않을지 여부입니다. |

## Minion/Movement/Orbit Crossfire

첫 번째 미니언은 궤도 이동 발사, 나머지 미니언은 정지 반복 발사를 수행합니다.

| Field | 기본값 | 의미 |
| --- | --- | --- |
| `orbitProjectileName` | `Default` | 궤도 이동 미니언이 발사할 투사체 이름입니다. |
| `stationaryProjectileName` | `Default` | 정지 미니언들이 발사할 투사체 이름입니다. |
| `useOrbitProjectileForStationary` | `false` | 켜면 정지 미니언도 `orbitProjectileName` 투사체를 사용합니다. |
| `minimumMinionCount` | `2` | 패턴 시작 전에 보장할 최소 미니언 수입니다. `0`이면 보장하지 않습니다. |
| `orbitRadius` | `2.6` | 플레이어 기준 궤도 반지름입니다. |
| `orbitSeconds` | `3` | 궤도 미니언이 회전에 사용할 시간입니다. |
| `fireAngleStepDegrees` | `30` | 궤도 미니언이 발사하는 각도 간격입니다. |
| `randomizeDirection` | `true` | 켜면 궤도 방향을 랜덤으로 정합니다. |
| `clockwise` | `false` | `randomizeDirection`이 꺼져 있을 때 시계 방향으로 회전할지 여부입니다. |
| `stationaryBulletCount` | `5` | 정지 미니언이 발사할 탄 수입니다. |
| `stationaryFireInterval` | `0.25` | 정지 미니언의 반복 발사 간격입니다. |
| `resumeIdle` | `true` | 종료 후 기본 대기 이동으로 복귀할지 여부입니다. |

## Minion/Fire/Radial Burst

모든 미니언이 자신 위치에서 방사형 발리를 발사합니다.

| Field | 기본값 | 의미 |
| --- | --- | --- |
| `projectileName` | `Default` | 발사할 투사체 이름입니다. |
| `volleyCount` | `1` | 방사형 발리 반복 횟수입니다. 최소 `1`로 보정됩니다. |
| `directionCount` | `5` | 한 번에 발사할 방향 수입니다. 최소 `1`로 보정됩니다. |
| `volleyInterval` | `0.35` | 발리 사이 간격입니다. |
| `spreadDegrees` | `75` | 플레이어 방향을 중심으로 퍼지는 각도입니다. `0`이면 호스트 구현에 따라 전방 집중 또는 특수 처리될 수 있습니다. |
| `resumeIdle` | `true` | 종료 후 기본 대기 이동으로 복귀할지 여부입니다. |
| `waitForDuration` | `true` | 발리 예상 시간이 끝날 때까지 다음 노드로 넘어가지 않을지 여부입니다. |

## Minion/Movement/Charge Side Fire

미니언들이 플레이어 방향에서 좌우로 비껴 돌진하며 양옆으로 발사합니다.

| Field | 기본값 | 의미 |
| --- | --- | --- |
| `projectileName` | `Default` | 발사할 투사체 이름입니다. |
| `chargeSeconds` | `1` | 돌진 지속 시간입니다. |
| `chargeSpeed` | `7` | 돌진 속도입니다. |
| `aimOffsetDegrees` | `22` | 플레이어 방향에서 좌우로 틀어지는 기본 각도입니다. |
| `sideFireInterval` | `0.18` | 돌진 중 양옆 발사 간격입니다. |
| `sideFireAngleDegrees` | `90` | 돌진 방향 기준 측면 발사 각도입니다. |
| `waitForDuration` | `true` | 돌진 예상 시간이 끝날 때까지 다음 노드로 넘어가지 않을지 여부입니다. |

## Minion/Movement/Formation

모든 미니언을 플레이어 주변 진형 위치로 이동시킵니다. 발사는 하지 않습니다.

| Field | 기본값 | 의미 |
| --- | --- | --- |
| `radius` | `2.8` | 플레이어 기준 진형 반지름입니다. |
| `angleSpacingDegrees` | `28` | 미니언들이 좌우로 벌어지는 각도 간격입니다. |
| `speedMultiplier` | `1.2` | 진형 위치로 따라붙는 속도 배율입니다. |
| `settleSeconds` | `1` | 진형을 잡았다고 보고 기다릴 시간입니다. |
| `waitForDuration` | `true` | `settleSeconds`만큼 다음 노드로 넘어가지 않을지 여부입니다. |

## Minion/Movement/Formation Barrage

미니언을 진형에 배치한 뒤 모든 미니언이 반복 포격합니다.

| Field | 기본값 | 의미 |
| --- | --- | --- |
| `projectileName` | `Default` | 미니언이 발사할 투사체 이름입니다. |
| `minimumMinionCount` | `1` | 패턴 시작 전에 보장할 최소 미니언 수입니다. `0`이면 보장하지 않습니다. |
| `formationRadius` | `2.8` | 플레이어 기준 진형 반지름입니다. |
| `formationAngleSpacingDegrees` | `28` | 미니언들이 좌우로 벌어지는 각도 간격입니다. |
| `formationSpeedMultiplier` | `1.2` | 진형 위치로 따라붙는 속도 배율입니다. |
| `settleSeconds` | `1` | 진형 연출 전체 시간입니다. |
| `preFormationDelayRatio` | `0.5` | `settleSeconds` 중 진형 명령을 내리기 전에 먼저 기다리는 비율입니다. 남은 시간이 실제 진형 이동 대기 시간이 됩니다. |
| `fireCount` | `6` | 진형을 잡은 뒤 모든 미니언이 발사할 횟수입니다. |
| `fireInterval` | `0.22` | 포격 반복 간격입니다. |
| `resumeIdle` | `true` | 포격 종료 후 기본 대기 이동으로 복귀할지 여부입니다. |

## Minion/Control/Command

미니언 명령을 하나의 액션에서 모드로 선택하는 범용 액션입니다.
새 패턴은 전용 액션을 우선 사용하고, 빠른 테스트나 커스텀 조합이 필요할 때 사용합니다.

| Field | 기본값 | 적용 모드 | 의미 |
| --- | --- | --- | --- |
| `mode` | `StopAndFire` | 전체 | 실행할 명령입니다. `StopAndFire`, `OrbitFire`, `RadialBurst`, `ChargeSideFire`, `Formation` 중 선택합니다. |
| `projectileName` | `Default` | Formation 제외 | 발사할 투사체 이름입니다. |
| `repeatCount` | `3` | StopAndFire, RadialBurst | 반복 발사 또는 발리 횟수입니다. 최소 `1`로 보정됩니다. |
| `fireInterval` | `0.2` | StopAndFire, RadialBurst | 반복 사이 간격입니다. |
| `directionCount` | `5` | RadialBurst | 한 발리에서 발사할 방향 수입니다. 최소 `1`로 보정됩니다. |
| `spreadDegrees` | `75` | RadialBurst | 방사 발사 각도입니다. |
| `orbitRadius` | `2.6` | OrbitFire | 플레이어 기준 궤도 반지름입니다. |
| `orbitSeconds` | `3` | OrbitFire | 회전에 사용할 시간입니다. |
| `fireAngleStepDegrees` | `30` | OrbitFire | 궤도 발사 각도 간격입니다. |
| `randomizeOrbitDirection` | `true` | OrbitFire | 켜면 회전 방향을 랜덤으로 정합니다. |
| `clockwise` | `false` | OrbitFire | 랜덤이 꺼졌을 때 시계 방향으로 회전할지 여부입니다. |
| `chargeSeconds` | `1` | ChargeSideFire | 돌진 지속 시간입니다. |
| `chargeSpeed` | `7` | ChargeSideFire | 돌진 속도입니다. |
| `aimOffsetDegrees` | `22` | ChargeSideFire | 플레이어 방향에서 좌우로 틀어지는 각도입니다. |
| `sideFireInterval` | `0.18` | ChargeSideFire | 돌진 중 측면 발사 간격입니다. |
| `sideFireAngleDegrees` | `90` | ChargeSideFire | 돌진 방향 기준 측면 발사 각도입니다. |
| `formationRadius` | `2.8` | Formation | 플레이어 기준 진형 반지름입니다. |
| `formationAngleSpacingDegrees` | `28` | Formation | 미니언 간 각도 간격입니다. |
| `formationSpeedMultiplier` | `1.2` | Formation | 진형 이동 속도 배율입니다. |
| `settleSeconds` | `1` | Formation | 진형 정착 대기 시간입니다. |
| `resumeIdle` | `true` | StopAndFire, RadialBurst | 종료 후 기본 대기 이동으로 복귀할지 여부입니다. |
| `waitForDuration` | `true` | 전체 | 예상 실행 시간 동안 다음 노드로 넘어가지 않을지 여부입니다. |

## Minion/Control/Wait Commands

현재 진행 중인 미니언 명령이 끝날 때까지 기다립니다.

| Field | 기본값 | 의미 |
| --- | --- | --- |
| `timeoutSeconds` | `0` | 최대 대기 시간입니다. `0`이면 명령이 끝날 때까지 제한 없이 기다립니다. |

## Minion/Control/Clear Synchronized Fire

예약된 동기화 발사 상태를 지웁니다.
필드는 없습니다.

## Minion/Control/Pattern Cleanup

패턴 종료 지점에서 미니언 상태를 정리합니다. 옵션은 위에서 아래 순서로 실행됩니다.

| Field | 기본값 | 의미 |
| --- | --- | --- |
| `waitForCommands` | `true` | 켜면 현재 미니언 명령이 끝날 때까지 기다립니다. |
| `waitTimeoutSeconds` | `0` | `waitForCommands` 대기 제한 시간입니다. `0`이면 제한 없이 기다립니다. |
| `clearSynchronizedFire` | `true` | 켜면 남아 있는 동기화 발사 예약을 지웁니다. |
| `stopAllMinions` | `false` | 켜면 모든 미니언의 현재 명령을 즉시 중지합니다. |
| `resumeIdle` | `true` | 켜면 모든 미니언을 기본 대기 이동으로 복귀시킵니다. |

## Minion/Control/Stop All

모든 미니언의 현재 명령을 즉시 중지합니다.
필드는 없습니다.

## Minion/Control/Resume Idle

모든 미니언을 기본 대기 이동 상태로 복귀시킵니다.
필드는 없습니다.
