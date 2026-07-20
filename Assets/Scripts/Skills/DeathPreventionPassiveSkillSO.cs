using UnityEngine;
using Week14.Combat;

namespace Week14.Skills
{
    [CreateAssetMenu(menuName = "Week14/Skills/Passive/Death Prevention", fileName = "DeathPreventionPassiveSkill")]
    public sealed class DeathPreventionPassiveSkillSO : BasePassiveSkillSO
    {
        [Tooltip("씬에 재입장(재도전)할 때마다 이 횟수만큼 사망을 막아줍니다.")]
        [SerializeField, Min(1)] private int charges = 1;
        [Tooltip("사망을 막을 때 플레이어 주변 이 반경(미터) 안의 적 투사체를 지웁니다.")]
        [SerializeField, Min(0f)] private float clearRadius = 3f;
        [Tooltip("사망을 막은 직후 부여할 무적 시간(초)입니다. 0이면 무적을 주지 않습니다.")]
        [SerializeField, Min(0f)] private float invulnerabilitySeconds = 1f;
        [Header("VFX")]
        [Tooltip("사망을 막으며 주변 탄막을 제거했을 때 재생할 이펙트 프리팹입니다. 비워두면 표시하지 않습니다.")]
        [SerializeField] private GameObject blankVfxPrefab;

        public override void ApplyPassive(GameObject player)
        {
            ResolvePlayerController(player)?.ConfigureDeathPrevention(charges, clearRadius, invulnerabilitySeconds, blankVfxPrefab);
        }

        public override void RemovePassive(GameObject player)
        {
            ResolvePlayerController(player)?.ClearDeathPrevention();
        }
    }
}
