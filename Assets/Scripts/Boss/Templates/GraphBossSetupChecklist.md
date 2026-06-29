# Graph Boss Setup Checklist

1. 빠른 테스트는 `TemplateGraphBossAI` 컴포넌트를 보스 프리팹에 붙여 시작한다.
2. 새 보스 전용 코드가 필요하면 `TemplateGraphBossAI.cs`를 복사하고 클래스명/파일명을 보스 이름으로 바꾼다.
3. 새 보스 스크립트는 `BossAI`가 아니라 `GraphBossAI`를 상속한다.
4. 보스 프리팹에서 `Boss Graph`에 그래프 에셋을 연결한다.
5. `Graph Projectiles` 첫 항목은 기본 투사체로 둔다.
6. 그래프 액션에서 이름 있는 투사체를 쓰면 `Graph Projectiles`에 같은 이름을 추가한다.
7. 미니언을 쓰면 `Minions > Prefab`에 `Minion` 컴포넌트가 붙은 프리팹을 연결한다.
8. BossGraph에는 `Minion/Spawn/Summon` 또는 `Minion/Spawn/Ensure Count` 뒤에 이동/발사 액션을 배치한다.
9. Play 모드에서 감지, 페이즈 전환, 처형, 사망 때 그래프와 미니언 명령이 멈추는지 확인한다.
