using System;

namespace Week14.UI
{
    // 로드아웃 패널의 스킬칸/스킬슬롯 아이콘들이 공유하는 호버 하이라이트 신호입니다.
    // 같은 스킬이 스킬칸과 스킬슬롯에 동시에 존재할 때, 어느 쪽을 호버해도 둘 다 아웃라인이 켜지도록
    // "지금 호버된 스킬 ID"를 방송하고, 각 아이콘이 자기 스킬 ID와 비교해서 스스로 켜고 끕니다.
    public static class LoadoutHoverHighlight
    {
        public static event Action<string> HoveredSkillIdChanged;

        public static void SetHovered(string skillId)
        {
            HoveredSkillIdChanged?.Invoke(skillId);
        }

        public static void ClearHovered()
        {
            HoveredSkillIdChanged?.Invoke(null);
        }
    }
}
