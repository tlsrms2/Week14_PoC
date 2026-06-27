using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Scripting.APIUpdating;

namespace Week14.Enemy
{
    [MovedFrom(true, "Week14.Enemy", "Assembly-CSharp", "MinionFireAllAction")]
    [Serializable]
    public sealed class MinionRepeatFireAction : BossAction, ISerializationCallbackReceiver
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

                MinionGraphCommandRequest request = MinionGraphCommandRequest.RepeatFire(
                    projectile,
                    volley.BulletCount,
                    volley.FireInterval,
                    fireSpec);
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
