using System;

namespace Week14.UI
{
    // 보스 패널의 보스슬롯들이 공유하는 호버 하이라이트 신호입니다. LoadoutHoverHighlight와 동일한 패턴으로,
    // "지금 호버된 보스 ID"를 방송하고 각 슬롯이 자기 보스 ID와 비교해서 스스로 아웃라인을 켜고 끕니다.
    public static class BossHoverHighlight
    {
        public static event Action<string> HoveredBossIdChanged;

        // 마지막으로 방송된 값을 기억해둔다. 슬롯의 OnEnable/구독 시점이 SetHovered 호출보다
        // 늦을 수도 있어서(Awake/OnEnable 실행 순서는 오브젝트끼리 보장되지 않음), 이벤트만으로는
        // 늦게 구독한 슬롯이 "이미 지나간 방송"을 놓칠 수 있다. 그래서 구독 직후 이 값으로
        // 한 번 더 동기화하면 순서와 무관하게 항상 맞는 상태로 시작할 수 있다.
        public static string CurrentHoveredBossId { get; private set; }

        public static void SetHovered(string bossId)
        {
            CurrentHoveredBossId = bossId;
            HoveredBossIdChanged?.Invoke(bossId);
        }

        public static void ClearHovered()
        {
            CurrentHoveredBossId = null;
            HoveredBossIdChanged?.Invoke(null);
        }
    }
}
