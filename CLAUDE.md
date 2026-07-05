# CLAUDE.md

## 적 전용 시간 슬로우(불릿타임) — 새 보스 패턴 작성 시 체크리스트

`Assets/Scripts/Boss/Core/EnemyTimeScale.cs`는 `Time.timeScale`을 건드리지 않고 "적/적탄"만 느려지게 만드는 전역 배율입니다(`TimeSlowSkillSO`가 사용). 플레이어 쪽 코드는 이 배율을 참조하지 않으므로 자동으로 영향을 안 받습니다.

**이미 배율이 적용되어 있어 그대로 쓰면 되는 공용 통로:**
- `BossAI.SetMovementVelocity(...)` — 보스 이동 속도
- `Minion.SetVelocity(...)` — 미니언 이동 속도
- `BossActionContext.WaitSeconds(...)` — 대부분의 발사 패턴/대기 액션이 공유
- `EnemyProjectile`(및 그 하위 클래스) — 상속만 하면 이동/소멸 시각까지 자동 처리됨

**새 보스 패턴이 위 통로를 안 쓰고 직접 `Update()`/`LateUpdate()`에 이동·타이머 코드를 짤 경우, 아래 3가지를 빼먹지 말 것:**
1. `Rigidbody2D.linearVelocity`에 속도를 직접 대입하는 코드 → 마지막에 `* EnemyTimeScale.Current` 곱하기
2. `elapsed += Time.deltaTime` 같은 진행 타이머 → `Time.deltaTime` 대신 `EnemyTimeScale.DeltaTime` 사용
3. `xxxEndsAt = Time.time + duration` 후 `Time.time >= xxxEndsAt`로 체크하는 절대 마감시각 → 매 틱마다 `xxxEndsAt += EnemyTimeScale.DeltaTimeDebt` 보정 추가 (또는 애초에 절대시각 대신 `EnemyTimeScale.DeltaTime`으로 누적하는 elapsed 카운터로 짜면 보정이 아예 필요 없음 — 새로 짤 땐 이 방식을 우선 고려)

이 체크리스트를 빼먹으면: 이동은 안 느려지거나, 액션 타이밍이 안 늘어나거나, 투사체가 느려진 채로 원래 실시간 안에 소멸해서 훨씬 짧은 거리만 날아가다 사라지는 식의 버그가 생깁니다.
