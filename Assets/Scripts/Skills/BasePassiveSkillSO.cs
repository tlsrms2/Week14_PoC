using UnityEngine;
using Week14.Combat;

namespace Week14.Skills
{
    public abstract class BasePassiveSkillSO : ScriptableObject
    {
        [Tooltip("패시브 스킬 고유 식별자입니다.")]
        [SerializeField] private string skillId;
        [Tooltip("UI에 표시할 패시브 스킬 이름입니다.")]
        [SerializeField] private string displayName;
        [Tooltip("UI에 표시할 패시브 스킬 아이콘입니다.")]
        [SerializeField] private Sprite icon;
        [Tooltip("UI에 표시할 패시브 스킬 설명입니다.")]
        [SerializeField, TextArea] private string description;
        [Tooltip("이 패시브 스킬을 구매하는 데 필요한 챌린지 포인트입니다.")]
        [SerializeField, Min(0)] private int price;

        public string SkillId => skillId;
        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public string Description => description;
        public int Price => price;

        // ApplyPassive/RemovePassive는 무기 장착/해제, 씬 재로드 시 여러 번 호출될 수 있으므로
        // 누적되지 않고 항상 같은 상태로 수렴하도록(멱등하게) 구현해야 합니다.
        public abstract void ApplyPassive(GameObject player);

        public abstract void RemovePassive(GameObject player);

        protected static PlayerCombatController ResolvePlayerController(GameObject player)
        {
            PlayerCombatController controller = player != null ? player.GetComponent<PlayerCombatController>() : null;
            return controller != null ? controller : PlayerCombatController.Active;
        }
    }
}
