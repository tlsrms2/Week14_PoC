using System;
using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class HackerWeaponThrowAction : BossAction, IBossActionDurationProvider
    {
        [Header("Weapon Prefabs")]
        [SerializeField] private GameObject bayonetPrefab;
        [SerializeField] private GameObject gunPrefab;
        [SerializeField] private GameObject swordPrefab;

        [Header("Equipped Weapons")]
        [SerializeField, BossGraphBossChildPath] private string bayonetEquippedWeaponPath;
        [SerializeField, BossGraphBossChildPath] private string gunEquippedWeaponPath;
        [SerializeField, BossGraphBossChildPath] private string swordEquippedWeaponPath;

        [Header("Throw")]
        [SerializeField, BossGraphBossChildPath] private string throwOriginPath;
        [SerializeField] private string animationTriggerName = "Throw";
        [SerializeField, Min(0f)] private float windupSeconds = 0.45f;
        [SerializeField, Min(0.01f)] private float flightSpeed = 6f;
        [SerializeField, Min(0f)] private float recoverySeconds = 0.25f;
        [SerializeField, Min(0f)] private float landingDistance = 2.5f;
        [SerializeField, Min(0f)] private float landingSideOffset;
        [SerializeField] private AnimationCurve flightSpeedCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context?.Boss is not HackerBossAI hacker)
            {
                yield break;
            }

            if (!TryResolveWeapon(hacker.CurrentPhaseIndex, out HackerThrownWeaponType weaponType, out GameObject weaponPrefab))
            {
                yield break;
            }

            context.PlayAnimationTrigger(animationTriggerName);
            yield return HackerMeleeAttackAction.Wait(context, windupSeconds);

            Transform equippedWeapon = GetEquippedWeapon(context, weaponType);
            if (equippedWeapon != null && !equippedWeapon.gameObject.activeSelf)
            {
                yield break;
            }

            Transform throwOrigin = equippedWeapon
                ?? context.GetBossChildTransform(throwOriginPath)
                ?? hacker.transform;
            Vector2 direction = context.GetDirectionToPlayer(throwOrigin.position);
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = Vector2.left;
            }

            Vector2 perpendicular = new Vector2(-direction.y, direction.x);
            Vector3 landingPosition = throwOrigin.position
                + (Vector3)(direction * landingDistance)
                + (Vector3)(perpendicular * landingSideOffset);
            landingPosition.z = throwOrigin.position.z;

            float playerAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Quaternion rotation = Quaternion.Euler(0f, 0f, playerAngle + 180f);
            GameObject weaponObject = UnityEngine.Object.Instantiate(weaponPrefab, throwOrigin.position, rotation);
            HackerThrownWeapon weapon = weaponObject.GetComponent<HackerThrownWeapon>();
            if (weapon == null)
            {
                weapon = weaponObject.AddComponent<HackerThrownWeapon>();
            }

            float flightSeconds = GetFlightSeconds(throwOrigin.position, landingPosition);
            weapon.ThrowTo(
                hacker,
                weaponType,
                throwOrigin.position,
                landingPosition,
                flightSeconds,
                flightSpeedCurve,
                equippedWeapon);
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

        private Transform GetEquippedWeapon(BossActionContext context, HackerThrownWeaponType weaponType)
        {
            string weaponPath = weaponType switch
            {
                HackerThrownWeaponType.Bayonet => bayonetEquippedWeaponPath,
                HackerThrownWeaponType.Gun => gunEquippedWeaponPath,
                HackerThrownWeaponType.Sword => swordEquippedWeaponPath,
                _ => null
            };
            return context.GetBossChildTransform(weaponPath);
        }

        private bool TryResolveWeapon(int phaseIndex, out HackerThrownWeaponType weaponType, out GameObject weaponPrefab)
        {
            if (phaseIndex > 0)
            {
                weaponType = HackerThrownWeaponType.Bayonet;
                weaponPrefab = bayonetPrefab;
                return weaponPrefab != null;
            }

            weaponType = HackerThrownWeaponType.Gun;
            weaponPrefab = gunPrefab;
            return weaponPrefab != null;
        }
    }
}
