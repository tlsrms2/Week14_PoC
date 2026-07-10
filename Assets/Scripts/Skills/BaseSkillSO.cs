using System;
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
        [Tooltip("이 스킬을 구매하는 데 필요한 챌린지 포인트입니다.")]
        [SerializeField, Min(0)] private int price;

        public string SkillId => skillId;
        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public string Description => description;
        public float CooldownSeconds => cooldownSeconds;
        public int Price => price;

        // true인 스킬은 사용 즉시 쿨타임이 시작되지 않고, 아래 SubscribeEffectEnd로 등록한 콜백이
        // 호출되는 시점(효과가 실제로 끝나는 시점)부터 CooldownSeconds만큼 쿨타임이 시작됩니다.
        public virtual bool HasDelayedCooldownStart => false;

        // HasDelayedCooldownStart가 true인 스킬만 구현하면 됩니다. 효과가 끝나는 시점에
        // onEffectEnd를 정확히 한 번 호출해야 합니다.
        public virtual void SubscribeEffectEnd(Action onEffectEnd) { }
        public virtual void UnsubscribeEffectEnd(Action onEffectEnd) { }

        public abstract void Execute(GameObject user);

        protected static PlayerCombatController ResolvePlayerController(GameObject user)
        {
            PlayerCombatController controller = user != null ? user.GetComponent<PlayerCombatController>() : null;
            return controller != null ? controller : PlayerCombatController.Active;
        }
    }
}
