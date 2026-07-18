using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
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
        private const float DefaultWeaponPositioningSeconds = 0.35f;

        [Header("Weapon")]
        [SerializeField] private HackerRecallWeaponSelection weaponSelection = HackerRecallWeaponSelection.RandomAvailable;
        [SerializeField, BossGraphBossChildPath] private string bossWireAnchorPath;
        [SerializeField] private string weaponWireAnchorPath = "WireAnchor";
        [SerializeField, BossGraphBossChildPath] private string returnAnchorPath;
        [SerializeField, Min(0f)] private float weaponReadyWaitSeconds = 2f;

        [Header("Animation")]
        [SerializeField] private string windupTriggerName = "WeaponWireOrbitWindup";
        [SerializeField] private string orbitTriggerName = "WeaponWireOrbit";
        [SerializeField] private string recallTriggerName = "RecallWeapon";
        [SerializeField, Min(0f)] private float windupSeconds = 0.45f;
        [SerializeField, Min(0f)] private float recoverySeconds = 0.25f;

        [Header("Weapon Positioning")]
        [SerializeField, Min(0.01f)] private float weaponPositioningSeconds = DefaultWeaponPositioningSeconds;

        [Header("Orbit")]
        [SerializeField, Min(0.05f)] private float orbitSeconds = 2f;
        [FormerlySerializedAs("endWireLength")]
        [SerializeField, Min(0.05f)] private float orbitRadius = 4.5f;
        [FormerlySerializedAs("wireLengthCurve")]
        [SerializeField] private AnimationCurve weaponPositioningSpeedCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
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

        [Header("Hacking")]
        [SerializeField, Min(1)] private int hackingPerHit = 1;

        [Header("Attack Effect")]
        [SerializeField] private BossActionPrefabEffectSettings attackEffect = new();

        [Header("Recall")]
        [SerializeField, Min(0.05f)] private float recallSeconds = 0.55f;
        [SerializeField] private float recallRotationDegrees = 360f;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context?.Boss is not HackerBossAI hacker)
            {
                yield break;
            }

            HackerThrownWeapon weapon = null;
            bool isHologramReplayWeapon = false;
            float weaponWaitElapsed = 0f;
            while (!TryGetWeapon(hacker, out weapon, out isHologramReplayWeapon)
                || weapon == null
                || !weapon.BeginOrbit())
            {
                if (isHologramReplayWeapon && weapon != null)
                {
                    UnityEngine.Object.Destroy(weapon.gameObject);
                }

                weapon = null;
                isHologramReplayWeapon = false;
                if (weaponWaitElapsed >= weaponReadyWaitSeconds)
                {
                    yield break;
                }

                if (context.IsExecutionPaused)
                {
                    context.Stop();
                    yield return null;
                    continue;
                }

                weaponWaitElapsed += EnemyTimeScale.DeltaTime;
                yield return null;
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
                orbitRadius);
            rangeIndicator.SetHologramStyle(hacker is HackerHologramBoss);
            rangeIndicator.SetFillVisible(true);
            Vector3 initialWeaponPosition = weapon.transform.position;
            Quaternion initialWeaponRotation = weapon.transform.rotation;
            Vector2 initialDirection = context.GetDirectionToPlayer(bossWireAnchor.position);
            float initialAngle = Mathf.Atan2(initialDirection.y, initialDirection.x) * Mathf.Rad2Deg;
            float preparationSeconds = GetPreparationSeconds();

            try
            {
                context.PlayAnimationTrigger(windupTriggerName);
                rangeIndicator.SetRing(bossWireAnchor.position, safeInnerRadius, orbitRadius);
                yield return null;
                yield return MoveWeaponIntoOrbitPosition(
                    context,
                    bossWireAnchor,
                    rangeIndicator,
                    weapon,
                    initialWeaponPosition,
                    initialWeaponRotation,
                    initialAngle,
                    preparationSeconds);

                if (weapon == null)
                {
                    yield break;
                }

                context.PlayAnimationTrigger(orbitTriggerName);
                attackEffect?.Play(context);

                Vector2 advanceDirection = context.GetDirectionToPlayer(hacker.transform.position);
                float directionMultiplier = rotationDirection == HackerWeaponOrbitDirection.Clockwise ? -1f : 1f;
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
                    float angle = initialAngle + directionMultiplier * rotationDegrees * GetCurveProgress(rotationSpeedCurve, progress);
                    float advanceMultiplier = EvaluateSpeedCurve(advanceSpeedCurve, progress);
                    hacker.SetMovementVelocity(advanceDirection * (advanceSpeed * advanceMultiplier));
                    SetWeaponOrbitPose(
                        weapon,
                        bossWireAnchor.position,
                        orbitRadius,
                        angle,
                        safeInnerRadius);
                    rangeIndicator.SetRing(bossWireAnchor.position, safeInnerRadius, orbitRadius);
                    TryApplyRingDamage(
                        hacker,
                        bossWireAnchor.position,
                        orbitRadius,
                        ref playerHit);

                    elapsed += EnemyTimeScale.DeltaTime;
                    yield return null;
                }

                if (weapon != null)
                {
                    float finalAngle = initialAngle + directionMultiplier * rotationDegrees;
                    SetWeaponOrbitPose(
                        weapon,
                        bossWireAnchor.position,
                        orbitRadius,
                        finalAngle,
                        safeInnerRadius);
                    rangeIndicator.SetRing(bossWireAnchor.position, safeInnerRadius, orbitRadius);
                    TryApplyRingDamage(
                        hacker,
                        bossWireAnchor.position,
                        orbitRadius,
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
            if (isHologramReplayWeapon && weapon != null)
            {
                UnityEngine.Object.Destroy(weapon.gameObject);
            }
        }

        public bool TryGetDurationSeconds(out float seconds)
        {
            seconds = GetPreparationSeconds()
                + Mathf.Max(0f, weaponReadyWaitSeconds)
                + Mathf.Max(0.05f, orbitSeconds)
                + Mathf.Max(0.05f, recallSeconds)
                + Mathf.Max(0f, recoverySeconds);
            return true;
        }

        private float GetPreparationSeconds()
        {
            float positioningSeconds = weaponPositioningSeconds > 0f
                ? weaponPositioningSeconds
                : DefaultWeaponPositioningSeconds;
            return Mathf.Max(windupSeconds, positioningSeconds);
        }

        private IEnumerator MoveWeaponIntoOrbitPosition(
            BossActionContext context,
            Transform orbitPivot,
            HackerAttackRangeIndicator indicator,
            HackerThrownWeapon weapon,
            Vector3 initialPosition,
            Quaternion initialRotation,
            float initialAngle,
            float seconds)
        {
            if (weapon == null)
            {
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < seconds)
            {
                if (context.IsExecutionPaused)
                {
                    context.Stop();
                    yield return null;
                    continue;
                }

                float progress = seconds > 0f ? Mathf.Clamp01(elapsed / seconds) : 1f;
                GetWeaponOrbitPose(
                    weapon,
                    orbitPivot != null ? orbitPivot.position : Vector3.zero,
                    orbitRadius,
                    initialAngle,
                    safeInnerRadius,
                    out Vector3 targetPosition,
                    out Quaternion targetRotation);
                float moveProgress = GetCurveProgress(weaponPositioningSpeedCurve, progress);
                weapon.SetOrbitPose(
                    Vector3.Lerp(initialPosition, targetPosition, moveProgress),
                    Quaternion.Slerp(initialRotation, targetRotation, moveProgress));
                indicator?.SetRing(
                    orbitPivot != null ? orbitPivot.position : Vector3.zero,
                    safeInnerRadius,
                    orbitRadius);
                elapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }

            if (weapon != null)
            {
                SetWeaponOrbitPose(
                    weapon,
                    orbitPivot != null ? orbitPivot.position : Vector3.zero,
                    orbitRadius,
                    initialAngle,
                    safeInnerRadius);
            }
        }

        private bool TryGetWeapon(
            HackerBossAI hacker,
            out HackerThrownWeapon weapon,
            out bool isHologramReplayWeapon)
        {
            isHologramReplayWeapon = false;
            if (TryGetGroundedWeapon(hacker, out weapon))
            {
                return true;
            }

            if (hacker is HackerHologramBoss hologram
                && hologram.TryCreateOrbitReplayWeapon(weaponSelection, out weapon))
            {
                isHologramReplayWeapon = true;
                return true;
            }

            weapon = null;
            return false;
        }

        private bool TryGetGroundedWeapon(HackerBossAI hacker, out HackerThrownWeapon weapon)
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

        private void SetWeaponOrbitPose(
            HackerThrownWeapon weapon,
            Vector3 center,
            float orbitRadius,
            float angleDegrees,
            float innerRadius)
        {
            GetWeaponOrbitPose(
                weapon,
                center,
                orbitRadius,
                angleDegrees,
                innerRadius,
                out Vector3 weaponPosition,
                out Quaternion weaponRotation);
            weapon.SetOrbitPose(weaponPosition, weaponRotation);
        }

        private void GetWeaponOrbitPose(
            HackerThrownWeapon weapon,
            Vector3 center,
            float outerRadius,
            float angleDegrees,
            float innerRadius,
            out Vector3 weaponPosition,
            out Quaternion weaponRotation)
        {
            float radians = angleDegrees * Mathf.Deg2Rad;
            Vector2 radialDirection = new(Mathf.Cos(radians), Mathf.Sin(radians));
            float weaponAngle = Mathf.Atan2(radialDirection.y, radialDirection.x) * Mathf.Rad2Deg
                + weaponRotationOffsetDegrees;
            float weaponRadius = Mathf.Lerp(Mathf.Max(0f, innerRadius), Mathf.Max(0.05f, outerRadius), 0.5f);
            weaponPosition = center + (Vector3)(radialDirection * weaponRadius);
            weaponPosition.z = weapon.transform.position.z;
            weaponRotation = Quaternion.Euler(0f, 0f, weaponAngle);
        }

        private void TryApplyRingDamage(
            HackerBossAI hacker,
            Vector2 center,
            float outerRadius,
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
            if (playerRadius <= innerRadius || playerRadius > Mathf.Max(0.05f, outerRadius) + hitRadius)
            {
                return;
            }

            playerHit = true;
            if (player.ReceiveAttack(damage, center, toPlayer.normalized))
            {
                hacker.ApplyHacking(player, hackingPerHit);
            }
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
