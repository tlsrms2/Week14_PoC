using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Scripting.APIUpdating;
using Week14.Audio;
using Week14.Combat;

namespace Week14.Enemy
{
    [MovedFrom(true, "Week14.Enemy", "Assembly-CSharp", "MinionFireAllAction")]
    [Serializable]
    public sealed class MinionRepeatFireAction : BossAction, ISerializationCallbackReceiver, IBossProjectileEmissionAction
    {
        [Serializable]
        public sealed class Volley
        {
            [SerializeField, Min(1)] private int bulletCount = 3;
            [SerializeField, Min(0f)] private float fireInterval = 0.2f;
            [SerializeField, Min(0f)] private float restSeconds = 0.35f;

            public Volley()
            {
            }

            public Volley(int bulletCount, float fireInterval, float restSeconds)
            {
                this.bulletCount = Mathf.Max(1, bulletCount);
                this.fireInterval = Mathf.Max(0f, fireInterval);
                this.restSeconds = Mathf.Max(0f, restSeconds);
            }

            public int BulletCount => Mathf.Max(1, bulletCount);
            public float FireInterval => Mathf.Max(0f, fireInterval);
            public float RestSeconds => Mathf.Max(0f, restSeconds);
        }

        [SerializeField, BossGraphProjectileName] private string projectileName = "Default";
        [SerializeField] private MinionGraphProjectileOriginSpec minionOrigin = new();
        [SerializeField] private BossGraphProjectileAimSpec aim = new();
        [SerializeField] private BossGraphEffectSettings effects = new();
        [SerializeField, Min(0f)] private float windupSeconds;
        [FormerlySerializedAs("shotCount")]
        [FormerlySerializedAs("bulletCount")]
        [FormerlySerializedAs("volleys")]
        [SerializeField, HideInInspector] private int legacyVolleyCount;
        [FormerlySerializedAs("fireInterval")]
        [SerializeField, HideInInspector] private float legacyFireInterval = -1f;
        [SerializeField, InspectorName("Volleys")] private List<Volley> volleyGroups = new() { new Volley() };
        [SerializeField] private bool waitForDuration = true;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (!MinionGraphActionHost.TryResolveProjectile(
                context,
                projectileName,
                out IMinionPatternHost host,
                out BossProjectileSettings projectile))
            {
                yield break;
            }

            if (volleyGroups == null || volleyGroups.Count == 0)
            {
                yield break;
            }

            MinionGraphProjectileFireSpec fireSpec = new(minionOrigin, aim, effects, context);
            yield return MinionGraphCommandRunner.WaitWindupIfNeeded(context, windupSeconds);
            for (int volleyIndex = 0; volleyIndex < volleyGroups.Count; volleyIndex++)
            {
                Volley volley = volleyGroups[volleyIndex];
                if (volley == null)
                {
                    continue;
                }

                MinionGraphProjectileFireSpec volleyFireSpec =
                    fireSpec.WithOnFired(CreateShotSfxHandler(context, volley.BulletCount));
                MinionGraphCommandRequest request = MinionGraphCommandRequest.RepeatFire(
                    projectile,
                    volley.BulletCount,
                    volley.FireInterval,
                    volleyFireSpec);
                float duration = host.CommandMinions(request);

                bool hasNextVolley = HasNextVolley(volleyIndex + 1);
                if ((waitForDuration || hasNextVolley) && duration > 0f)
                {
                    yield return context.WaitSeconds(duration);
                }

                if (hasNextVolley && volley.RestSeconds > 0f)
                {
                    yield return context.WaitSeconds(volley.RestSeconds);
                }
            }
        }

        public void OnBeforeSerialize()
        {
            legacyVolleyCount = 0;
            legacyFireInterval = -1f;
        }

        public void OnAfterDeserialize()
        {
            if (legacyVolleyCount <= 0)
            {
                return;
            }

            float migratedFireInterval = legacyFireInterval >= 0f ? legacyFireInterval : 0.2f;
            volleyGroups = new List<Volley>
            {
                new(legacyVolleyCount, migratedFireInterval, 0f)
            };
            legacyVolleyCount = 0;
            legacyFireInterval = -1f;
        }

        // 미니언이 몇 마리든, 같은 shotIndex에서 처음 실제로 발사에 성공한 투사체 하나만 대표로 삼아
        // 볼리당(=shotIndex당) 사운드가 정확히 한 번만 나게 한다. 발사음은 그 투사체의 실제
        // Launched 이벤트에 걸리므로, 차징 중 패링/파괴되면 소리가 나지 않는다.
        private Action<int, EnemyProjectile> CreateShotSfxHandler(BossActionContext context, int bulletCount)
        {
            bool[] handledShots = new bool[Mathf.Max(1, bulletCount)];
            return (shotIndex, firedProjectile) =>
            {
                if (shotIndex < 0 || shotIndex >= handledShots.Length || handledShots[shotIndex])
                {
                    return;
                }

                handledShots[shotIndex] = true;
                context.PlaySfx(SoundEvent.Boss_ProjectileFire);
                context.PlaySfxOnLaunch(firedProjectile, SoundEvent.Boss_ProjectileLaunch);
            };
        }

        private bool HasNextVolley(int startIndex)
        {
            if (volleyGroups == null)
            {
                return false;
            }

            for (int i = startIndex; i < volleyGroups.Count; i++)
            {
                if (volleyGroups[i] != null)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
