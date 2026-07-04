using UnityEngine;
using Week14.Combat;

namespace Week14.Skills
{
    public abstract class BaseSkillSO : ScriptableObject
    {
        [Tooltip("스킬 고유 식별자입니다. 저장 데이터와 SkillDatabase에서 이 값으로 스킬을 찾습니다.")]
        [SerializeField] private string skillId;
        [Tooltip("UI에 표시할 스킬 이름입니다.")]
        [SerializeField] private string displayName;
        [Tooltip("UI에 표시할 스킬 아이콘입니다.")]
        [SerializeField] private Sprite icon;
        [Tooltip("UI에 표시할 스킬 설명입니다.")]
        [SerializeField, TextArea] private string description;
        [Tooltip("스킬을 다시 사용할 수 있을 때까지 걸리는 시간(초)입니다.")]
        [SerializeField, Min(0f)] private float cooldownSeconds = 1f;

        public string SkillId => skillId;
        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public string Description => description;
        public float CooldownSeconds => cooldownSeconds;

        public abstract void Execute(GameObject user);

        protected static PlayerCombatController ResolvePlayerController(GameObject user)
        {
            PlayerCombatController controller = user != null ? user.GetComponent<PlayerCombatController>() : null;
            return controller != null ? controller : PlayerCombatController.Active;
        }
    }
}
