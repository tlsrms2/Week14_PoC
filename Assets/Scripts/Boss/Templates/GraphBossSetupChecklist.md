# Graph Boss Setup Checklist

1. 새 보스 스크립트는 `BossAI`가 아니라 `GraphBossAI`를 상속한다.
2. 코드가 필요 없는 데이터 주도형 보스는 `GraphBossAI` 컴포넌트를 그대로 사용한다.
3. 보스 프리팹에서 `Boss Graph`에 그래프 에셋을 연결한다.
4. `Graph Projectiles` 첫 항목은 기본 투사체로 둔다.
5. 그래프 액션에서 이름 있는 투사체를 쓰면 `Graph Projectiles`에 같은 이름을 추가한다.
6. 미니언을 쓰면 `Minions > Prefab`에 `Minion` 컴포넌트가 붙은 프리팹을 연결한다.
7. BossGraph에는 `Minion/Spawn/Summon` 또는 `Minion/Spawn/Ensure Count` 뒤에 이동/발사 액션을 배치한다.
8. Play 모드에서 감지, 페이즈 전환, 처형, 사망 때 그래프와 미니언 명령이 멈추는지 확인한다.
