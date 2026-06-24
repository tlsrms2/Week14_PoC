# Minion Graph Actions Guide

DronePilot 그래프에서 `Type`이 `Minion`인 노드에 넣는 액션 목록입니다.
미니언 액션은 DronePilot 본체가 소유한 `Minion`들을 한 그래프 패턴 안에서 소환, 이동, 발사, 정리하도록 제어합니다.

## 공통 규칙

- `projectileName`, `bossProjectileName`, `minionProjectileName`, `orbitProjectileName`, `stationaryProjectileName`은 DronePilot 인스펙터의 `Boss Graph 투사체` 목록에 있는 `Name`을 가리킵니다.
- 이름이 비어 있거나 찾지 못하면 DronePilot의 첫 번째 투사체 설정으로 fallback됩니다.
- `ensure/minimum/target count` 계열 값이 `0`이면 미니언 수 보장을 하지 않습니다.
- `waitForDuration`이 켜져 있으면 명령 예상 시간이 끝날 때까지 그래프가 다음 노드로 넘어가지 않습니다.
- `resumeIdle`이 켜져 있으면 액션이 끝난 뒤 미니언을 기본 대기 이동 상태로 되돌립니다.
- 패턴 끝에는 보통 `Minion/Pattern Cleanup`을 붙여 명령 대기, 동기화 발사 해제, 대기 복귀를 정리합니다.

## 추천 패턴 조합

- 기본 보스 버스트: `Minion/Auto Summon If Needed` -> `Minion/Boss Burst` -> `Minion/Pattern Cleanup`
- 기존 Pattern1 동기화 발사: `Minion/Auto Summon If Needed` -> `Minion/Synchronized Burst` -> `Minion/Pattern Cleanup`
- 기존 Pattern2 궤도 교차 사격: `Minion/Auto Summon If Needed` -> `Minion/Orbit Crossfire` -> `Minion/Pattern Cleanup`
- 기존 Pattern3 방사 발사: `Minion/Auto Summon If Needed` -> `Minion/Radial Burst` -> `Minion/Pattern Cleanup`
- 기존 Pattern4 돌진 양옆 발사: `Minion/Auto Summon If Needed` -> `Minion/Charge Side Fire` -> `Minion/Pattern Cleanup`
- 기존 Pattern5 진형 포격: `Minion/Auto Summon If Needed` -> `Minion/Formation Barrage` -> `Minion/Pattern Cleanup`

## Minion/Summon

DronePilot의 `summon` 설정을 사용해 미니언을 소환합니다.

| Field | 의미 |
| --- | --- |
| `summonCount` | 이번 액션에서 소환할 미니언 수입니다. `0`이면 DronePilot 인스펙터의 기본 `summonCount`를 사용합니다. |

## Minion/Ensure Count

패턴에 필요한 미니언 수를 보장합니다. 부족하면 소환하고, 이미 충분하면 바로 통과합니다.

| Field | 의미 |
| --- | --- |
| `targetCount` | 이 수만큼 소유 미니언이 있도록 보장합니다. |

## Minion/Auto Summon If Needed

DronePilot의 자동 소환 쿨타임과 최대 소유 수를 보고, 부족한 경우에만 소환합니다.
필드가 없습니다.

## Minion/Fire All

현재 소유한 모든 미니언이 플레이어 방향으로 같은 투사체를 발사합니다.

| Field | 의미 |
| --- | --- |
| `projectileName` | 미니언이 발사할 투사체 이름입니다. |
| `shotCount` | 모든 미니언이 반복 발사할 횟수입니다. |
| `fireInterval` | 반복 발사 사이 간격입니다. |

## Minion/Boss Burst

DronePilot 본체가 연속 발사합니다. 옵션으로 매 발사마다 모든 미니언도 함께 발사하게 할 수 있습니다.

| Field | 의미 |
| --- | --- |
| `bossProjectileName` | 보스 본체가 발사할 투사체 이름입니다. |
| `minionProjectileName` | `notifyMinions`가 켜졌을 때 미니언이 발사할 투사체 이름입니다. |
| `windupSeconds` | 첫 발사 전 대기 시간입니다. |
| `bulletCount` | 보스 본체가 연속 발사할 탄 수입니다. |
| `fireInterval` | 보스 본체 연속 발사 간격입니다. |
| `spawnSpacing` | 보스 탄환을 발사 방향의 좌우로 번갈아 벌리는 거리입니다. |
| `notifyMinions` | 켜면 보스가 한 발 쏠 때마다 모든 미니언도 한 번 발사합니다. |

## Minion/Synchronized Burst

기존 Drone Pattern1에 해당합니다. 보스 본체 발사 타이밍에 맞춰 미니언이 동기화 발사합니다.

| Field | 의미 |
| --- | --- |
| `bossProjectileName` | 보스 본체가 발사할 투사체 이름입니다. |
| `minionProjectileName` | 미니언이 발사할 투사체 이름입니다. |
| `useBossProjectileForMinions` | 켜면 미니언도 `bossProjectileName` 투사체를 사용합니다. |
| `ensureMinionCount` | 패턴 시작 전에 보장할 최소 미니언 수입니다. |
| `windupSeconds` | 첫 발사 전 대기 시간입니다. |
| `bulletCount` | 보스 발사 횟수이자 미니언 동기화 발사 횟수입니다. |
| `fireInterval` | 동기화 발사 간격입니다. |
| `spawnSpacing` | 보스 탄환을 발사 방향의 좌우로 번갈아 벌리는 거리입니다. |

## Minion/Stop And Fire

모든 미니언을 정지시키고 플레이어 방향으로 반복 발사시킵니다.

| Field | 의미 |
| --- | --- |
| `projectileName` | 발사할 투사체 이름입니다. |
| `bulletCount` | 각 미니언이 발사할 탄 수입니다. |
| `fireInterval` | 각 미니언의 반복 발사 간격입니다. |
| `resumeIdle` | 발사 종료 후 기본 대기 이동으로 복귀할지 여부입니다. |
| `waitForDuration` | 발사 시간이 끝날 때까지 다음 노드로 넘어가지 않을지 여부입니다. |

## Minion/Orbit Fire

모든 미니언이 플레이어 주변을 한 바퀴 돌며 일정 각도마다 발사합니다.

| Field | 의미 |
| --- | --- |
| `projectileName` | 발사할 투사체 이름입니다. |
| `orbitRadius` | 플레이어 기준 회전 반지름입니다. |
| `orbitSeconds` | 한 바퀴 도는 데 걸리는 시간입니다. |
| `fireAngleStepDegrees` | 회전 중 이 각도만큼 이동할 때마다 발사합니다. |
| `randomizeDirection` | 켜면 시계/반시계 방향을 랜덤으로 정합니다. |
| `clockwise` | `randomizeDirection`이 꺼져 있을 때 시계 방향 회전 여부입니다. |
| `waitForDuration` | 회전이 끝날 때까지 다음 노드로 넘어가지 않을지 여부입니다. |

## Minion/Orbit Crossfire

기존 Drone Pattern2에 해당합니다. 첫 번째 미니언은 궤도 이동 발사, 나머지 미니언은 정지 반복 발사를 합니다.

| Field | 의미 |
| --- | --- |
| `orbitProjectileName` | 궤도 이동 미니언이 발사할 투사체 이름입니다. |
| `stationaryProjectileName` | 정지 미니언들이 발사할 투사체 이름입니다. |
| `useOrbitProjectileForStationary` | 켜면 정지 미니언도 `orbitProjectileName` 투사체를 사용합니다. |
| `minimumMinionCount` | 패턴 시작 전에 보장할 최소 미니언 수입니다. |
| `orbitRadius` | 플레이어 기준 궤도 반지름입니다. |
| `orbitSeconds` | 궤도 미니언이 한 바퀴 도는 시간입니다. |
| `fireAngleStepDegrees` | 궤도 미니언이 발사하는 각도 간격입니다. |
| `randomizeDirection` | 켜면 궤도 방향을 랜덤으로 정합니다. |
| `clockwise` | `randomizeDirection`이 꺼져 있을 때 시계 방향 회전 여부입니다. |
| `stationaryBulletCount` | 정지 미니언이 발사할 탄 수입니다. |
| `stationaryFireInterval` | 정지 미니언의 반복 발사 간격입니다. |
| `resumeIdle` | 종료 후 기본 대기 이동으로 복귀할지 여부입니다. |

## Minion/Radial Burst

기존 Drone Pattern3에 해당합니다. 모든 미니언이 자신 위치에서 방사형 발리를 발사합니다.

| Field | 의미 |
| --- | --- |
| `projectileName` | 발사할 투사체 이름입니다. |
| `volleyCount` | 방사형 발리 반복 횟수입니다. |
| `directionCount` | 한 번에 발사할 방향 수입니다. |
| `volleyInterval` | 발리 사이 간격입니다. |
| `spreadDegrees` | 플레이어 방향을 중심으로 퍼지는 각도입니다. `0`이면 360도 방사로 쓰면 됩니다. |
| `resumeIdle` | 종료 후 기본 대기 이동으로 복귀할지 여부입니다. |
| `waitForDuration` | 발리 시간이 끝날 때까지 다음 노드로 넘어가지 않을지 여부입니다. |

## Minion/Charge Side Fire

기존 Drone Pattern4에 해당합니다. 미니언들이 플레이어 방향에서 좌우로 비껴 돌진하며 양옆으로 발사합니다.

| Field | 의미 |
| --- | --- |
| `projectileName` | 발사할 투사체 이름입니다. |
| `chargeSeconds` | 돌진 지속 시간입니다. |
| `chargeSpeed` | 돌진 속도입니다. |
| `aimOffsetDegrees` | 플레이어 방향에서 좌우로 틀어지는 기본 각도입니다. 미니언 순서에 따라 양쪽으로 나뉩니다. |
| `sideFireInterval` | 돌진 중 양옆 발사 간격입니다. |
| `sideFireAngleDegrees` | 돌진 방향 기준 양옆 발사 각도입니다. |
| `waitForDuration` | 돌진 시간이 끝날 때까지 다음 노드로 넘어가지 않을지 여부입니다. |

## Minion/Formation

모든 미니언을 플레이어 주변 진형 위치로 이동시킵니다. 발사는 하지 않습니다.

| Field | 의미 |
| --- | --- |
| `radius` | 플레이어 기준 진형 반지름입니다. |
| `angleSpacingDegrees` | 미니언들이 좌우로 벌어지는 각도 간격입니다. |
| `speedMultiplier` | 진형 위치로 따라붙는 속도 배율입니다. |
| `settleSeconds` | 진형을 잡았다고 보고 기다릴 시간입니다. |
| `waitForDuration` | `settleSeconds`만큼 다음 노드로 넘어가지 않을지 여부입니다. |

## Minion/Formation Barrage

기존 Drone Pattern5에 해당합니다. 미니언을 진형에 배치한 뒤 모든 미니언이 반복 포격합니다.

| Field | 의미 |
| --- | --- |
| `projectileName` | 미니언이 발사할 투사체 이름입니다. |
| `minimumMinionCount` | 패턴 시작 전에 보장할 최소 미니언 수입니다. |
| `formationRadius` | 플레이어 기준 진형 반지름입니다. |
| `formationAngleSpacingDegrees` | 미니언들이 좌우로 벌어지는 각도 간격입니다. |
| `formationSpeedMultiplier` | 진형 위치로 따라붙는 속도 배율입니다. |
| `settleSeconds` | 진형 연출 전체 대기 시간입니다. |
| `preFormationDelayRatio` | `settleSeconds` 중 진형 명령을 내리기 전에 먼저 기다리는 비율입니다. `0.5`면 기존 Pattern5처럼 절반 대기 후 진형 이동합니다. |
| `fireCount` | 진형을 잡은 뒤 모든 미니언이 발사할 횟수입니다. |
| `fireInterval` | 포격 반복 간격입니다. |
| `resumeIdle` | 포격 종료 후 기본 대기 이동으로 복귀할지 여부입니다. |

## Minion/Command

미니언 명령을 하나의 액션에서 모드로 선택하는 범용 액션입니다.
새 패턴은 위의 전용 액션을 우선 사용하고, 빠르게 테스트하거나 한 노드에서 모드를 바꾸고 싶을 때 사용합니다.

| Field | 적용 모드 | 의미 |
| --- | --- | --- |
| `mode` | 전체 | 실행할 명령입니다. `StopAndFire`, `OrbitFire`, `RadialBurst`, `ChargeSideFire`, `Formation` 중 선택합니다. |
| `projectileName` | Formation 제외 | 발사할 투사체 이름입니다. |
| `repeatCount` | StopAndFire, RadialBurst | 반복 발사 또는 발리 횟수입니다. |
| `fireInterval` | StopAndFire, RadialBurst | 반복 사이 간격입니다. |
| `directionCount` | RadialBurst | 한 발리에서 발사할 방향 수입니다. |
| `spreadDegrees` | RadialBurst | 방사 발사 각도입니다. |
| `orbitRadius` | OrbitFire | 플레이어 기준 궤도 반지름입니다. |
| `orbitSeconds` | OrbitFire | 한 바퀴 도는 시간입니다. |
| `fireAngleStepDegrees` | OrbitFire | 궤도 발사 각도 간격입니다. |
| `randomizeOrbitDirection` | OrbitFire | 켜면 회전 방향을 랜덤으로 정합니다. |
| `clockwise` | OrbitFire | 랜덤이 꺼졌을 때 시계 방향 여부입니다. |
| `chargeSeconds` | ChargeSideFire | 돌진 지속 시간입니다. |
| `chargeSpeed` | ChargeSideFire | 돌진 속도입니다. |
| `aimOffsetDegrees` | ChargeSideFire | 플레이어 방향에서 좌우로 틀어지는 각도입니다. |
| `sideFireInterval` | ChargeSideFire | 돌진 중 양옆 발사 간격입니다. |
| `sideFireAngleDegrees` | ChargeSideFire | 돌진 방향 기준 양옆 발사 각도입니다. |
| `formationRadius` | Formation | 플레이어 기준 진형 반지름입니다. |
| `formationAngleSpacingDegrees` | Formation | 미니언 간 각도 간격입니다. |
| `formationSpeedMultiplier` | Formation | 진형 이동 속도 배율입니다. |
| `settleSeconds` | Formation | 진형 정착 대기 시간입니다. |
| `resumeIdle` | StopAndFire, RadialBurst | 종료 후 기본 대기 이동으로 복귀할지 여부입니다. |
| `waitForDuration` | 전체 | 예상 실행 시간 동안 다음 노드로 넘어가지 않을지 여부입니다. |

## Minion/Wait Commands

현재 진행 중인 미니언 명령이 끝날 때까지 기다립니다.

| Field | 의미 |
| --- | --- |
| `timeoutSeconds` | 최대 대기 시간입니다. `0`이면 명령이 끝날 때까지 제한 없이 기다립니다. |

## Minion/Clear Synchronized Fire

예약된 동기화 발사 상태를 지웁니다.
필드가 없습니다.

## Minion/Pattern Cleanup

패턴 종료 지점에서 미니언 상태를 정리합니다.

| Field | 의미 |
| --- | --- |
| `waitForCommands` | 켜면 현재 미니언 명령이 끝날 때까지 기다립니다. |
| `waitTimeoutSeconds` | `waitForCommands` 대기 제한 시간입니다. `0`이면 제한 없이 기다립니다. |
| `clearSynchronizedFire` | 켜면 남아 있는 동기화 발사 예약을 지웁니다. |
| `stopAllMinions` | 켜면 모든 미니언의 현재 명령을 즉시 중지합니다. |
| `resumeIdle` | 켜면 모든 미니언을 기본 대기 이동으로 복귀시킵니다. |

## Minion/Stop All

모든 미니언의 현재 명령을 즉시 중지합니다.
필드가 없습니다.

## Minion/Resume Idle

모든 미니언을 기본 대기 이동 상태로 복귀시킵니다.
필드가 없습니다.
