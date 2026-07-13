using System;
using System.Collections;
using UnityEngine;
using Week14.Audio;
using Week14.Combat;
using Week14.Enemy;

namespace Week14.Skills
{
    [CreateAssetMenu(menuName = "Week14/Skills/Active/Bullet Absorb Skill", fileName = "BulletAbsorbSkill")]
    public sealed class BulletAbsorbSkillSO : BaseSkillSO
    {
        private const int BulletAbsorbVfxSortingOrder = 74;

        [Tooltip("탄막을 흡수하는 지속시간(초)입니다.")]
        [SerializeField, Min(0.1f)] private float durationSeconds = 3f;
        [Tooltip("플레이어 주변 이 반경(미터) 안의 적 투사체를 흡수합니다. 이 범위 밖의 공격이나 근접공격은 평소처럼 그대로 맞습니다.")]
        [SerializeField, Min(0f)] private float absorbRadius = 3f;
        [Tooltip("투사체 하나를 흡수할 때마다 회복시킬 탄환 수입니다.")]
        [SerializeField, Min(0)] private int ammoPerAbsorbedProjectile = 1;
        [Tooltip("스킬 발동 시 재생할 SFX의 SoundLibrary ID입니다. 비워두면 재생하지 않습니다.")]
        [BossGraphSfxId]
        [SerializeField] private string activateSfxId;

        [Header("VFX")]
        [Tooltip("스킬 지속시간 동안 플레이어 중심에 표시할 흡수 이펙트 프리팹입니다. 비워두면 표시하지 않습니다.")]
        [SerializeField] private GameObject bulletAbsorbVfxPrefab;
        [Tooltip("흡수된 투사체가 플레이어에게 빨려들어가는 연출 시간입니다. 0이면 흡수 연출을 끕니다.")]
        [SerializeField, Min(0f)] private float absorbVfxDuration = 0.18f;
        [Tooltip("흡수 연출 색상입니다. 알파로 투명도를 조절합니다.")]
        [SerializeField] private Color absorbVfxColor = new Color(0.45f, 0.95f, 1f, 0.85f);

        private event Action effectEnd;
        private Coroutine activeRoutine;
        private GameObject activeBulletAbsorbVfx;

        public override bool HasDelayedCooldownStart => true;

        public override void SubscribeEffectEnd(Action onEffectEnd) => effectEnd += onEffectEnd;
        public override void UnsubscribeEffectEnd(Action onEffectEnd) => effectEnd -= onEffectEnd;

        public override void Execute(GameObject user)
        {
            PlayerCombatController controller = ResolvePlayerController(user);
            if (controller == null)
            {
                effectEnd?.Invoke();
                return;
            }

            if (activeRoutine != null)
            {
                controller.StopCoroutine(activeRoutine);
                activeRoutine = null;
                ClearActiveBulletAbsorbVfx();
            }

            if (!string.IsNullOrEmpty(activateSfxId))
            {
                SoundManager.PlaySfx(activateSfxId);
            }

            activeRoutine = controller.StartCoroutine(AbsorbRoutine(controller));
        }

        private IEnumerator AbsorbRoutine(PlayerCombatController controller)
        {
            RollSkillVfxSettings vfxSettings = CreateVfxSettings();
            SpawnBulletAbsorbVfx(controller);
            float startTime = Time.time;

            while (Time.time - startTime < durationSeconds)
            {
                Vector2 center = controller.Context.CombatCenterOrigin.position;
                int absorbedCount = controller.AutoParryProjectilesNear(center, absorbRadius, vfxSettings);
                if (absorbedCount > 0 && ammoPerAbsorbedProjectile > 0 && controller.Bullets != null
                    && controller.Bullets.Restore(absorbedCount * ammoPerAbsorbedProjectile, BulletChangeSource.Parry))
                {
                    PlayerBulletAudio.PlayBulletRestoreSfx(controller.Bullets.CurrentBullets, controller.Bullets.MaxBullets);
                }

                yield return null;
            }

            ClearActiveBulletAbsorbVfx();
            activeRoutine = null;
            effectEnd?.Invoke();
        }

        private void SpawnBulletAbsorbVfx(PlayerCombatController controller)
        {
            ClearActiveBulletAbsorbVfx();
            if (bulletAbsorbVfxPrefab == null || controller == null)
            {
                return;
            }

            Transform followTarget = controller.Context.CombatCenterOrigin;
            activeBulletAbsorbVfx = Instantiate(
                bulletAbsorbVfxPrefab,
                followTarget.position,
                Quaternion.identity,
                followTarget);
            activeBulletAbsorbVfx.transform.localPosition = Vector3.zero;
            activeBulletAbsorbVfx.transform.localRotation = Quaternion.identity;
            PlayBulletAbsorbParticles(activeBulletAbsorbVfx);
        }

        private void ClearActiveBulletAbsorbVfx()
        {
            if (activeBulletAbsorbVfx == null)
            {
                return;
            }

            Destroy(activeBulletAbsorbVfx);
            activeBulletAbsorbVfx = null;
        }

        private static void PlayBulletAbsorbParticles(GameObject vfxRoot)
        {
            if (vfxRoot == null)
            {
                return;
            }

            ParticleSystemRenderer[] renderers = vfxRoot.GetComponentsInChildren<ParticleSystemRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].sortingOrder = Mathf.Max(renderers[i].sortingOrder, BulletAbsorbVfxSortingOrder);
                }
            }

            ParticleSystem[] particles = vfxRoot.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particles.Length; i++)
            {
                if (particles[i] != null)
                {
                    particles[i].Play(true);
                }
            }
        }

        private RollSkillVfxSettings CreateVfxSettings()
        {
            return new RollSkillVfxSettings(
                0.045f,
                0f,
                Color.clear,
                absorbVfxDuration,
                absorbVfxColor);
        }
    }
}
