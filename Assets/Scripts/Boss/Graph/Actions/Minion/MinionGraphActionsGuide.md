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
- `projectileName`은 호스트 보스의 Boss Graph 투사체 목록에 있는 `Name`입니다.
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

## Minion/Fire/Sequential Fire

현재 관리 중인 미니언들이 한 번에 쏘지 않고 순서대로 하나씩 발사합니다.

| Field | 기본값 | 설명 |
| --- | --- | --- |
| `projectileName` | `Default` | 미니언이 발사할 투사체 이름입니다. |
| `minionOrigin` | `ProjectileOrigin` | 각 미니언의 발사 위치입니다. |
| `aim` | `AtPlayer` | 각 미니언의 조준 방향입니다. |
| `effects` | 꺼짐 | 발사 이펙트입니다. |
| `windupSeconds` | `0` | 첫 발사 전 대기 시간입니다. |
| `cycleCount` | `1` | 전체 미니언 순차 발사를 반복할 횟수입니다. |
| `fireInterval` | `0.12` | 미니언 한 마리씩 발사하는 간격입니다. |

## Minion/Fire/Repeat Fire

모든 미니언이 현재 움직임을 유지한 채 Volley 목록 순서대로 반복 발사합니다.

| Field | 기본값 | 의미 |
| --- | --- | --- |
| `projectileName` | `Default` | 미니언이 발사할 투사체 이름입니다. |
| `minionOrigin` | `ProjectileOrigin` | 미니언 발사 위치입니다. |
| `aim` | `AtPlayer` | 미니언 조준 방향입니다. |
| `effects` | 꺼짐 | 발사 이펙트입니다. |
| `Volleys[].bulletCount` | `3` | 해당 Volley에서 각 미니언이 발사할 횟수입니다. |
| `Volleys[].fireInterval` | `0.2` | 해당 Volley 안에서 반복 발사 간격입니다. |
| `Volleys[].restSeconds` | `0.35` | 다음 Volley로 넘어가기 전 대기 시간입니다. |
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

## Minion/Movement/Orbit

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
| `useStartPlayerPosition` | `false` | 켜면 Orbit 시작 순간의 플레이어 위치를 원 중심으로 고정합니다. |
| `randomizeDirection` | `true` | 회전 방향을 랜덤으로 정합니다. |
| `clockwise` | `false` | 랜덤이 꺼져 있을 때 시계 방향 회전 여부입니다. |
| `waitForDuration` | `true` | 회전 시간이 끝날 때까지 대기할지 여부입니다. |

## Minion/Movement/Legacy Orbit

모든 미니언이 궤도 이동 발사를 수행합니다.

| Field | 기본값 | 의미 |
| --- | --- | --- |
| `orbitProjectileName` | `Default` | 궤도 미니언이 발사할 투사체 이름입니다. |
| `minionOrigin` | `ProjectileOrigin` | 미니언 발사 위치입니다. |
| `aim` | `AtPlayer` | 미니언 조준 방향입니다. |
| `effects` | 꺼짐 | 발사 이펙트입니다. |
| `orbitRadius` | `2.6` | 궤도 반지름입니다. |
| `orbitSeconds` | `3` | 궤도 이동 시간입니다. |
| `fireAngleStepDegrees` | `30` | 궤도 미니언의 발사 각도 간격입니다. |
| `randomizeDirection` | `true` | 궤도 방향을 랜덤으로 정합니다. |
| `clockwise` | `false` | 랜덤이 꺼져 있을 때 시계 방향 회전 여부입니다. |
| `resumeIdle` | `true` | 종료 후 기본 대기 이동으로 복귀할지 여부입니다. |

## Minion/Movement/Dash

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

## Minion/Movement/Gather

미니언을 플레이어 기준 배치로 집합시킵니다.

| Field | 기본값 | 설명 |
| --- | --- | --- |
| `anchorMode` | `ClosestToPlayer` | 가까운/먼/중간 거리 미니언 중 어떤 미니언을 첫 슬롯으로 삼을지 정합니다. |
| `angleDegrees` | `0` | `FixedAngle`일 때 플레이어 기준 원의 시작 각도입니다. |
| `layout` | `Circle` | `Circle`, `Vertical`, `Orthogonal`, `Random` 중 하나로 집합 배치를 정합니다. `Vertical`은 원의 법선 방향 바깥쪽, `Orthogonal`은 그 법선에 직교하는 접선 방향입니다. |
| `radius` | `2` | 원형/직교/무작위 배치의 기준 반지름입니다. |
| `spacing` | `0.75` | 수직/직교 배치에서 슬롯 사이 간격입니다. |
| `moveSpeed` | `24` | 집합 위치로 이동하는 속도입니다. |
| `settleSeconds` | `0.5` | 다음 노드로 넘어가기 전 대기 시간입니다. |
| `waitForDuration` | `true` | `settleSeconds` 동안 기다릴지 정합니다. |

## Minion/Movement/Formation Circle

미니언을 플레이어 주변 진형 위치로 이동시킵니다. 발사하지 않습니다.

| Field | 기본값 | 의미 |
| --- | --- | --- |
| `radius` | `2.8` | 플레이어 기준 진형 반지름입니다. |
| `sideBySide` | `false` | 켜면 플레이어에서 보스를 바라보는 방향을 중심으로, 보스와 같은 거리의 원호 양옆에 미니언을 배치합니다. |
| `angleSpacingDegrees` | `28` | 미니언들이 좌우로 벌어지는 각도 간격입니다. |
| `speedMultiplier` | `1.2` | 진형 위치로 이동하는 속도 배율입니다. |
| `settleSeconds` | `1` | 진형에 맞춘 뒤 대기할 시간입니다. |
| `waitForDuration` | `true` | `settleSeconds`만큼 다음 노드로 넘어가지 않을지 여부입니다. |

## Minion/Movement/Formation Straight

미니언을 플레이어 기준 1자 진형으로 배치합니다. 플레이어가 움직이면 같은 거리와 간격을 유지하며 따라갑니다.

| Field | 기본값 | 의미 |
| --- | --- | --- |
| `mode` | `PlayerForward` | `PlayerForward`는 플레이어 진행 방향 앞, `BetweenBossAndPlayer`는 보스와 플레이어 사이에 배치합니다. |
| `distanceFromPlayer` | `2` | 플레이어로부터 진형 중심까지의 거리입니다. |
| `spacing` | `0.7` | 미니언 사이의 좌우 간격입니다. |
| `speedMultiplier` | `1.2` | 진형 위치로 이동하는 속도 배율입니다. |
| `settleSeconds` | `1` | 진형에 맞춘 뒤 대기할 시간입니다. |
| `waitForDuration` | `true` | `settleSeconds`만큼 다음 노드로 넘어가지 않을지 여부입니다. |

## Minion/Control/Pattern Cleanup

| Field | 기본값 | 의미 |
| --- | --- | --- |
| `waitForCommands` | `true` | 현재 미니언 명령이 끝날 때까지 기다립니다. |
| `waitTimeoutSeconds` | `0` | 명령 대기 제한 시간입니다. `0`이면 제한 없이 기다립니다. |
| `clearSynchronizedFire` | `true` | 남아 있는 동기화 발사 예약을 지웁니다. |
| `stopAllMinions` | `false` | 모든 미니언의 현재 명령을 즉시 중지합니다. |
| `resumeIdle` | `true` | 모든 미니언을 기본 대기 이동으로 복귀시킵니다. |
