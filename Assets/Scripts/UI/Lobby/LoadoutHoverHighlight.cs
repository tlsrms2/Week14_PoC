using System;

namespace Week14.UI
{
    // 로드아웃 패널의 스킬칸/스킬슬롯 아이콘들이 공유하는 호버 하이라이트 신호입니다.
    // 같은 스킬이 스킬칸과 스킬슬롯에 동시에 존재할 때, 어느 쪽을 호버해도 둘 다 아웃라인이 켜지도록
    // "지금 호버된 스킬 ID"를 방송하고, 각 아이콘이 자기 스킬 ID와 비교해서 스스로 켜고 끕니다.
    public static class LoadoutHoverHighlight
    {
        public static event Action<string> HoveredSkillIdChanged;

        // 마지막으로 방송된 값을 기억해둔다. 아이콘의 OnEnable/구독 시점이 SetHovered 호출보다
        // 늦을 수도 있어서(Awake/OnEnable 실행 순서는 오브젝트끼리 보장되지 않음), 이벤트만으로는
        // 늦게 구독한 아이콘이 "이미 지나간 방송"을 놓칠 수 있다. 그래서 구독 직후 이 값으로
        // 한 번 더 동기화하면 순서와 무관하게 항상 맞는 상태로 시작할 수 있다.
        public static string CurrentHoveredSkillId { get; private set; }

        public static void SetHovered(string skillId)
        {
            CurrentHoveredSkillId = skillId;
            HoveredSkillIdChanged?.Invoke(skillId);
        }

        public static void ClearHovered()
        {
            CurrentHoveredSkillId = null;
            HoveredSkillIdChanged?.Invoke(null);
        }
    }
}
