using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class MinionRadialBurstAction : BossAction, ISerializationCallbackReceiver, IBossProjectileEmissionAction
    {
        [Serializable]
        public sealed class Volley
        {
            [SerializeField, Min(1)] private int directionCount = 5;
            [SerializeField, Range(0f, 360f)] private float spreadDegrees = 75f;
            [SerializeField, Min(0f)] private float restSeconds = 0.35f;

            public Volley()
            {
            }

            public Volley(int directionCount, float spreadDegrees, float restSeconds)
            {
                this.directionCount = Mathf.Max(1, directionCount);
                this.spreadDegrees = Mathf.Clamp(spreadDegrees, 0f, 360f);
                this.restSeconds = Mathf.Max(0f, restSeconds);
            }

            public int DirectionCount => Mathf.Max(1, directionCount);
            public float SpreadDegrees => Mathf.Clamp(spreadDegrees, 0f, 360f);
            public float RestSeconds => Mathf.Max(0f, restSeconds);
        }

        [SerializeField, BossGraphProjectileName] private string projectileName = "Default";
        [SerializeField] private MinionGraphProjectileOriginSpec minionOrigin = new();
        [SerializeField] private BossGraphProjectileAimSpec aim = new();
        [SerializeField] private BossGraphEffectSettings effects = new();
        [SerializeField, Min(0f)] private float windupSeconds;
        [FormerlySerializedAs("volleyCount")]
        [SerializeField, HideInInspector] private int legacyVolleyCount;
        [FormerlySerializedAs("directionCount")]
        [SerializeField, HideInInspector] private int legacyDirectionCount;
        [FormerlySerializedAs("volleyInterval")]
        [SerializeField, HideInInspector] private float legacyVolleyInterval = -1f;
        [FormerlySerializedAs("spreadDegrees")]
        [SerializeField, HideInInspector] private float legacySpreadDegrees = -1f;
        [SerializeField, InspectorName("Volleys")] private List<Volley> volleyGroups = new() { new Volley() };
        [SerializeField, HideInInspector] private bool resumeIdle = true;
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

                MinionGraphCommandRequest request = MinionGraphCommandRequest.RadialBurst(
                    projectile,
                    1,
                    volley.DirectionCount,
                    0f,
                    volley.SpreadDegrees,
                    fireSpec,
                    resumeIdle);
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
            legacyDirectionCount = 0;
            legacyVolleyInterval = -1f;
            legacySpreadDegrees = -1f;
        }

        public void OnAfterDeserialize()
        {
            if (legacyVolleyCount <= 0
                && legacyDirectionCount <= 0
                && legacyVolleyInterval < 0f
                && legacySpreadDegrees < 0f)
            {
                return;
            }

            int count = Mathf.Max(1, legacyVolleyCount);
            int directions = legacyDirectionCount > 0 ? legacyDirectionCount : 5;
            float interval = legacyVolleyInterval >= 0f ? legacyVolleyInterval : 0.35f;
            float spread = legacySpreadDegrees >= 0f ? legacySpreadDegrees : 75f;
            volleyGroups = new List<Volley>();
            for (int i = 0; i < count; i++)
            {
                volleyGroups.Add(new Volley(directions, spread, i < count - 1 ? interval : 0f));
            }

            legacyVolleyCount = 0;
            legacyDirectionCount = 0;
            legacyVolleyInterval = -1f;
            legacySpreadDegrees = -1f;
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
