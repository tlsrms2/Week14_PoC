using UnityEngine;
using Week14.Audio;

namespace Week14.Enemy
{
    [AddComponentMenu("Week14/Boss/Template Graph Boss AI")]
    public sealed class TemplateGraphBossAI : GraphBossAI
    {
        [Header("Template Example")]
        [SerializeField, Tooltip("비워두면 BGM을 재생하지 않습니다.")]
        private string bgmId;
        [SerializeField, Tooltip("켜면 BossAI 기본 동작처럼 플레이어 방향으로 몸체를 회전합니다.")]
        private bool rotateBodyToPlayer = true;

        protected override bool RotatesBodyToPlayer => rotateBodyToPlayer;

        protected override void OnCombatStarted()
        {
            if (!string.IsNullOrWhiteSpace(bgmId))
            {
                SoundManager.PlayBgm(bgmId);
            }
        }

        // 보스별 페이즈 연출이 필요하면 이 메서드를 복사해서 확장하세요.
        // base 호출은 BossGraph 런타임 리셋에 필요하므로 제거하지 마세요.
        protected override void OnBossPhaseChanged(int phaseIndex, int phaseNumber)
        {
            base.OnBossPhaseChanged(phaseIndex, phaseNumber);
        }
    }
}
