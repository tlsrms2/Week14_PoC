using System;
using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class HackerWeaponThrowAction : BossAction, IBossActionDurationProvider
    {
        [Header("Throwing Weapon")]
        [SerializeField] private GameObject throwingWeaponPrefab;
        [SerializeField, BossGraphBossChildPath] private string equippedThrowingWeaponPath;

        [Header("Throw")]
        [SerializeField, BossGraphBossChildPath] private string throwOriginPath;
        [SerializeField] private string animationTriggerName = "Throw";
        [SerializeField, Min(0f)] private float windupSeconds = 0.45f;
        [SerializeField, Min(0.01f)] private float flightSpeed = 6f;
        [SerializeField, Min(0f)] private float recoverySeconds = 0.25f;
        [SerializeField, Min(0f)] private float landingDistance = 2.5f;
        [SerializeField, Min(0f)] private float landingSideOffset;
        [SerializeField] private AnimationCurve flightSpeedCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
        [SerializeField, BossGraphSfxId] private string landingSfxId = HackerSfxIds.PutTurret;

        [Header("Weapon Sprite")]
        [Tooltip("무기가 땅에 완전히 떨어지기 전까지 사용할 스프라이트입니다. 비어 있으면 프리팹 원본을 사용합니다.")]
        [SerializeField] private Sprite flyingSprite;
        [Tooltip("무기가 땅에 완전히 떨어진 뒤 사용할 스프라이트입니다. 비어 있으면 프리팹 원본을 사용합니다.")]
        [SerializeField] private Sprite groundedSprite;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context?.Boss is not HackerBossAI hacker)
            {
                yield break;
            }

            if (throwingWeaponPrefab == null)
            {
                yield break;
            }

            Transform equippedWeapon = context.GetBossChildTransform(equippedThrowingWeaponPath);
            bool isHologram = hacker is HackerHologramBoss;
            if (!isHologram
                && equippedWeapon != null
                && !equippedWeapon.gameObject.activeSelf)
            {
                yield break;
            }

            context.RestartAnimationTrigger(animationTriggerName);
            yield return HackerMeleeAttackAction.Wait(context, windupSeconds);

            Transform throwOrigin = equippedWeapon
                ?? context.GetBossChildTransform(throwOriginPath)
                ?? hacker.transform;
            Vector2 direction = context.GetDirectionToPlayer(throwOrigin.position);
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = Vector2.left;
            }

            hacker.FaceHorizontalDirection(direction.x);
            using IDisposable facingLock = context.AcquireFacingLock();
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);
            Vector3 landingPosition = throwOrigin.position
                + (Vector3)(direction * landingDistance)
                + (Vector3)(perpendicular * landingSideOffset);
            landingPosition.z = throwOrigin.position.z;

            float playerAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Quaternion rotation = Quaternion.Euler(0f, 0f, playerAngle + 180f);
            GameObject weaponObject = UnityEngine.Object.Instantiate(throwingWeaponPrefab, throwOrigin.position, rotation);
            if (hacker is HackerHologramBoss hologram)
            {
                hologram.ApplyHologramStyle(weaponObject);
            }

            HackerThrownWeapon weapon = weaponObject.GetComponent<HackerThrownWeapon>();
            if (weapon == null)
            {
                weapon = weaponObject.AddComponent<HackerThrownWeapon>();
            }

            float flightSeconds = GetFlightSeconds(throwOrigin.position, landingPosition);
            weapon.ThrowTo(
                hacker,
                HackerThrownWeaponType.ThrowingWeapon,
                throwOrigin.position,
                landingPosition,
                flightSeconds,
                flightSpeedCurve,
                equippedWeapon,
                flyingSprite,
                groundedSprite,
                HackerSfxIds.Resolve(landingSfxId, HackerSfxIds.PutTurret));
            yield return HackerMeleeAttackAction.Wait(context, flightSeconds);
            yield return HackerMeleeAttackAction.Wait(context, recoverySeconds);
        }

        public bool TryGetDurationSeconds(out float seconds)
        {
            seconds = Mathf.Max(0f, windupSeconds) + GetFlightSeconds() + Mathf.Max(0f, recoverySeconds);
            return true;
        }

        private float GetFlightSeconds()
        {
            float distance = new Vector2(landingDistance, landingSideOffset).magnitude;
            return GetFlightSeconds(Vector3.zero, new Vector3(distance, 0f));
        }

        private float GetFlightSeconds(Vector3 startPosition, Vector3 endPosition)
        {
            float distance = Vector2.Distance(startPosition, endPosition);
            return Mathf.Max(0.01f, distance / Mathf.Max(0.01f, flightSpeed));
        }

    }
}
