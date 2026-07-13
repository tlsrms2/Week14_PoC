using System;
using System.Collections;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    public enum HackerWeaponOrbitDirection
    {
        CounterClockwise,
        Clockwise
    }

    [Serializable]
    public sealed class HackerWeaponWireOrbitAction : BossAction, IBossActionDurationProvider
    {
        [Header("Weapon")]
        [SerializeField] private HackerRecallWeaponSelection weaponSelection = HackerRecallWeaponSelection.RandomAvailable;
        [SerializeField, BossGraphBossChildPath] private string bossWireAnchorPath;
        [SerializeField] private string weaponWireAnchorPath = "WireAnchor";
        [SerializeField, BossGraphBossChildPath] private string returnAnchorPath;

        [Header("Animation")]
        [SerializeField] private string windupTriggerName = "WeaponWireOrbitWindup";
        [SerializeField] private string orbitTriggerName = "WeaponWireOrbit";
        [SerializeField] private string recallTriggerName = "RecallWeapon";
        [SerializeField, Min(0f)] private float windupSeconds = 0.45f;
        [SerializeField, Min(0f)] private float recoverySeconds = 0.25f;

        [Header("Orbit")]
        [SerializeField, Min(0.05f)] private float orbitSeconds = 2f;
        [SerializeField, Min(0.05f)] private float startWireLength = 3f;
        [SerializeField, Min(0.05f)] private float endWireLength = 4.5f;
        [SerializeField] private AnimationCurve wireLengthCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField, Min(1f)] private float rotationDegrees = 360f;
        [SerializeField] private HackerWeaponOrbitDirection rotationDirection;
        [SerializeField] private AnimationCurve rotationSpeedCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
        [SerializeField] private float weaponRotationOffsetDegrees;

        [Header("Orbit Advance")]
        [SerializeField, Min(0f)] private float advanceSpeed = 2f;
        [SerializeField] private AnimationCurve advanceSpeedCurve = AnimationCurve.EaseInOut(0f, 0.35f, 1f, 0.7f);

        [Header("Sweep Damage")]
        [SerializeField, Min(0f)] private float safeInnerRadius = 1f;
        [SerializeField, Min(0.01f)] private float hitRadius = 0.2f;
        [SerializeField, Min(1)] private int damage = 1;

        [Header("Recall")]
        [SerializeField, Min(0.05f)] private float recallSeconds = 0.55f;
        [SerializeField] private float recallRotationDegrees = 360f;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context?.Boss is not HackerBossAI hacker
                || !TryGetWeapon(hacker, out HackerThrownWeapon weapon)
                || weapon == null
                || !weapon.BeginOrbit())
            {
                yield break;
            }

            Transform bossWireAnchor = context.GetBossChildTransform(bossWireAnchorPath) ?? hacker.transform;
            Transform weaponWireAnchor = weapon.GetChildTransform(weaponWireAnchorPath) ?? weapon.transform;
            HackerWireSettings wireSettings = hacker.WireSettings;
            HackerRecallWireVisual wireVisual = HackerRecallWireVisual.Create(
                bossWireAnchor,
                weaponWireAnchor,
                wireSettings.Color,
                wireSettings.Width);
            HackerAttackRangeIndicator rangeIndicator = HackerAttackRangeIndicator.CreateRing(
                bossWireAnchor.position,
                safeInnerRadius,
                startWireLength);

            try
            {
                context.PlayAnimationTrigger(windupTriggerName);
                yield return WaitWithRingIndicator(
                    context,
                    bossWireAnchor,
                    rangeIndicator,
                    startWireLength,
                    windupSeconds);

                if (weapon == null)
                {
                    yield break;
                }

                context.PlayAnimationTrigger(orbitTriggerName);
                rangeIndicator.SetFillVisible(true);

                Vector2 initialDirection = context.GetDirectionToPlayer(bossWireAnchor.position);
                float initialAngle = Mathf.Atan2(initialDirection.y, initialDirection.x) * Mathf.Rad2Deg;
                Vector2 advanceDirection = context.GetDirectionToPlayer(hacker.transform.position);
                float directionMultiplier = rotationDirection == HackerWeaponOrbitDirection.Clockwise ? -1f : 1f;
                float previousAngle = initialAngle;
                float previousLength = startWireLength;
                bool playerHit = false;
                float elapsed = 0f;
                while (elapsed < orbitSeconds && weapon != null)
                {
                    if (context.IsExecutionPaused)
                    {
                        context.Stop();
                        yield return null;
                        continue;
                    }

                    float progress = Mathf.Clamp01(elapsed / orbitSeconds);
                    float wireLength = GetWireLength(progress);
                    float angle = initialAngle + directionMultiplier * rotationDegrees * GetCurveProgress(rotationSpeedCurve, progress);
                    float advanceMultiplier = EvaluateSpeedCurve(advanceSpeedCurve, progress);
                    hacker.SetMovementVelocity(advanceDirection * (advanceSpeed * advanceMultiplier));
                    SetWeaponOrbitPose(
                        weapon,
                        bossWireAnchor.position,
                        wireLength,
                        angle,
                        safeInnerRadius);
                    rangeIndicator.SetRing(bossWireAnchor.position, safeInnerRadius, wireLength);
                    TryApplySweepDamage(
                        bossWireAnchor.position,
                        previousLength,
                        wireLength,
                        previousAngle,
                        angle,
                        ref playerHit);

                    previousLength = wireLength;
                    previousAngle = angle;
                    elapsed += EnemyTimeScale.DeltaTime;
                    yield return null;
                }

                if (weapon != null)
                {
                    float finalLength = GetWireLength(1f);
                    float finalAngle = initialAngle + directionMultiplier * rotationDegrees;
                    SetWeaponOrbitPose(
                        weapon,
                        bossWireAnchor.position,
                        finalLength,
                        finalAngle,
                        safeInnerRadius);
                    rangeIndicator.SetRing(bossWireAnchor.position, safeInnerRadius, finalLength);
                    TryApplySweepDamage(
                        bossWireAnchor.position,
                        previousLength,
                        finalLength,
                        previousAngle,
                        finalAngle,
                        ref playerHit);
                }
            }
            finally
            {
                if (wireVisual != null)
                {
                    UnityEngine.Object.Destroy(wireVisual.gameObject);
                }

                HackerAttackRangeIndicator.Destroy(rangeIndicator);
                weapon?.EndOrbit();
                context.Stop();
            }

            if (weapon == null)
            {
                yield break;
            }

            context.PlayAnimationTrigger(recallTriggerName);
            Transform returnAnchor = context.GetBossChildTransform(returnAnchorPath) ?? hacker.transform;
            weaponWireAnchor = weapon.GetChildTransform(weaponWireAnchorPath) ?? weapon.transform;
            if (weapon.BeginRecall(returnAnchor, weaponWireAnchor, recallSeconds, recallRotationDegrees))
            {
                HackerRecallWireVisual.Create(returnAnchor, weaponWireAnchor, wireSettings.Color, wireSettings.Width);
                yield return HackerMeleeAttackAction.Wait(context, recallSeconds);
            }

            yield return HackerMeleeAttackAction.Wait(context, recoverySeconds);
        }

        public bool TryGetDurationSeconds(out float seconds)
        {
            seconds = Mathf.Max(0f, windupSeconds)
                + Mathf.Max(0.05f, orbitSeconds)
                + Mathf.Max(0.05f, recallSeconds)
                + Mathf.Max(0f, recoverySeconds);
            return true;
        }

        private IEnumerator WaitWithRingIndicator(
            BossActionContext context,
            Transform orbitPivot,
            HackerAttackRangeIndicator indicator,
            float outerRadius,
            float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                if (context.IsExecutionPaused)
                {
                    context.Stop();
                    yield return null;
                    continue;
                }

                indicator?.SetRing(
                    orbitPivot != null ? orbitPivot.position : Vector3.zero,
                    safeInnerRadius,
                    outerRadius);
                elapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }
        }

        private bool TryGetWeapon(HackerBossAI hacker, out HackerThrownWeapon weapon)
        {
            if (weaponSelection != HackerRecallWeaponSelection.RandomAvailable)
            {
                return hacker.TryGetGroundedWeapon((HackerThrownWeaponType)weaponSelection, out weapon)
                    && weapon != null
                    && weapon.IsGrounded;
            }

            HackerThrownWeaponType[] types =
            {
                HackerThrownWeaponType.Bayonet,
                HackerThrownWeaponType.Gun,
                HackerThrownWeaponType.Sword
            };
            int firstIndex = UnityEngine.Random.Range(0, types.Length);
            for (int i = 0; i < types.Length; i++)
            {
                HackerThrownWeaponType type = types[(firstIndex + i) % types.Length];
                if (hacker.TryGetGroundedWeapon(type, out weapon) && weapon != null && weapon.IsGrounded)
                {
                    return true;
                }
            }

            weapon = null;
            return false;
        }

        private float GetWireLength(float progress)
        {
            return Mathf.Max(0.05f, Mathf.Lerp(startWireLength, endWireLength, GetCurveProgress(wireLengthCurve, progress)));
        }

        private void SetWeaponOrbitPose(
            HackerThrownWeapon weapon,
            Vector3 center,
            float orbitRadius,
            float angleDegrees,
            float innerRadius)
        {
            float radians = angleDegrees * Mathf.Deg2Rad;
            Vector2 radialDirection = new(Mathf.Cos(radians), Mathf.Sin(radians));
            float weaponAngle = Mathf.Atan2(radialDirection.y, radialDirection.x) * Mathf.Rad2Deg
                + weaponRotationOffsetDegrees;
            float weaponRadius = Mathf.Lerp(Mathf.Max(0f, innerRadius), Mathf.Max(0.05f, orbitRadius), 0.5f);
            Vector3 weaponPosition = center + (Vector3)(radialDirection * weaponRadius);
            weaponPosition.z = weapon.transform.position.z;
            weapon.SetOrbitPose(weaponPosition, Quaternion.Euler(0f, 0f, weaponAngle));
        }

        private void TryApplySweepDamage(
            Vector2 center,
            float previousLength,
            float currentLength,
            float previousAngle,
            float currentAngle,
            ref bool playerHit)
        {
            if (playerHit)
            {
                return;
            }

            PlayerCombatController player = PlayerCombatController.Active;
            if (player == null || player.Health == null || player.Health.IsDead)
            {
                return;
            }

            Vector2 toPlayer = (Vector2)player.transform.position - center;
            float playerRadius = toPlayer.magnitude;
            float innerRadius = Mathf.Max(0f, safeInnerRadius);
            float outerRadius = Mathf.Max(previousLength, currentLength);
            if (playerRadius <= innerRadius || playerRadius > outerRadius + hitRadius)
            {
                return;
            }

            float sweepDegrees = currentAngle - previousAngle;
            float absoluteSweep = Mathf.Abs(sweepDegrees);
            if (absoluteSweep <= 0.001f)
            {
                return;
            }

            float playerAngle = Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg;
            float travelledDegrees = sweepDegrees > 0f
                ? Mathf.Repeat(playerAngle - previousAngle, 360f)
                : Mathf.Repeat(previousAngle - playerAngle, 360f);
            float anglePadding = Mathf.Atan2(hitRadius, Mathf.Max(0.01f, playerRadius)) * Mathf.Rad2Deg;
            if (absoluteSweep < 360f && travelledDegrees > absoluteSweep + anglePadding)
            {
                return;
            }

            playerHit = true;
            player.ReceiveAttack(damage, center, toPlayer.normalized);
        }

        private static float GetCurveProgress(AnimationCurve curve, float progress)
        {
            const int sampleCount = 24;
            float clampedProgress = Mathf.Clamp01(progress);
            if (curve == null || curve.length == 0 || clampedProgress <= 0f)
            {
                return clampedProgress;
            }

            float fullArea = IntegrateCurve(curve, 1f, sampleCount);
            if (fullArea <= 0.0001f)
            {
                return clampedProgress;
            }

            return Mathf.Clamp01(IntegrateCurve(curve, clampedProgress, sampleCount) / fullArea);
        }

        private static float IntegrateCurve(AnimationCurve curve, float endTime, int sampleCount)
        {
            float clampedEnd = Mathf.Clamp01(endTime);
            float previous = Mathf.Max(0f, curve.Evaluate(0f));
            float area = 0f;
            for (int i = 1; i <= sampleCount; i++)
            {
                float time = clampedEnd * i / sampleCount;
                float current = Mathf.Max(0f, curve.Evaluate(time));
                area += (previous + current) * 0.5f * (clampedEnd / sampleCount);
                previous = current;
            }

            return area;
        }

        private static float EvaluateSpeedCurve(AnimationCurve curve, float progress)
        {
            return curve != null && curve.length > 0
                ? Mathf.Max(0f, curve.Evaluate(progress))
                : 1f;
        }
    }
}
