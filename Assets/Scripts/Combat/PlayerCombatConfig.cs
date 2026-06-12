using UnityEngine;
using UnityEngine.Serialization;

namespace Week14.Combat
{
    [CreateAssetMenu(menuName = "Week14/Combat/Player Combat Config", fileName = "PlayerCombatConfig")]
    public sealed class PlayerCombatConfig : ScriptableObject
    {
        [Header("상태")]
        [SerializeField, FormerlySerializedAs("maxHealth"), Min(1f)] private float maxDurability = 100f;
        [SerializeField, Min(1f)] private float maxHeat = 100f;
        [SerializeField, Min(0f)] private float heatCoolingPerSecond = 12f;
        [SerializeField, Min(0f)] private float overheatSeconds = 1.4f;
        [SerializeField, Range(0f, 1f)] private float heatAfterOverheatRatio = 0.35f;
        [SerializeField, Min(0f)] private float moveSpeed = 5f;
        [SerializeField, Min(1f)] private float sprintSpeedMultiplier = 1.5f;
        [SerializeField, Min(0f)] private float sprintHeatCoolingSuppressSeconds = 0.12f;
        [SerializeField, Min(0f)] private float actionHeatCoolingSuppressSeconds = 0.5f;

        [Header("왼손 권총")]
        [SerializeField] private PlayerProjectile projectilePrefab;
        [SerializeField, Min(0f)] private float attackDamage = 12f;
        [SerializeField, Min(0f)] private float attackHeat = 16f;
        [SerializeField, Min(0f)] private float attackRange = 7f;
        [SerializeField, Min(0f)] private float attackCooldown = 0.28f;
        [SerializeField, Min(0f)] private float projectileSpeed = 12f;
        [SerializeField, Min(0f)] private float projectileLifetime = 1f;
        [SerializeField, Min(0f)] private float projectileRadius = 0.08f;
        [SerializeField, Min(0.01f)] private float projectileTrailSeconds = 0.08f;
        [SerializeField, Min(0.1f)] private float projectileTrailWidthMultiplier = 2.8f;
        [SerializeField, Min(1f)] private float projectileGlowScale = 3f;
        [SerializeField, Min(0f)] private float combatEffectSeconds = 0.08f;
        [SerializeField, Min(0f)] private float gunAimHoldSeconds = 0.5f;
        [SerializeField] private Color attackEffectColor = new Color(1f, 0.35f, 0.12f, 0.55f);

        [Header("오른손 패링 권총")]
        [SerializeField, Range(1f, 360f)] private float parryAimAngleDegrees = 65f;
        [SerializeField, Min(0f)] private float parryShotCooldown = 0.18f;
        [SerializeField] private Color parryEffectColor = new Color(0.2f, 0.65f, 1f, 0.45f);
        [SerializeField] private Color parrySparkColor = new Color(1f, 0.88f, 0.35f, 1f);
        [SerializeField] private Color parryRingColor = new Color(0.45f, 0.9f, 1f, 0.75f);
        [SerializeField] private Color parryRingGlitterColor = new Color(1f, 0.96f, 0.68f, 1f);
        [SerializeField, FormerlySerializedAs("parryVfxSeconds"), Min(0f)] private float parrySparkSeconds = 0.22f;
        [SerializeField, Min(0f)] private float parryRingSeconds = 0.32f;
        [SerializeField, Min(0f)] private float parryRingGlitterSeconds = 0.2f;
        [SerializeField, Min(0)] private int parrySparkCount = 34;
        [SerializeField, Min(0)] private int parryRingGlitterCount = 18;
        [SerializeField, Min(0f)] private float hitHeatToPlayer = 10f;

        [Header("락온")]
        [SerializeField, Min(0f)] private float lockOnSearchRadius = 2f;
        [SerializeField, Min(0f)] private float lockOnBreakDistance = 9f;

        [Header("처형")]
        [SerializeField, Min(0f)] private float executionRange = 1.2f;
        [SerializeField, Min(0f)] private float executionHeatRecovery = 45f;
        [SerializeField] private bool destroyTargetOnExecute = true;
        [SerializeField, Min(0f)] private float executionStandOffDistance = 0.65f;
        [SerializeField, Min(0f)] private float executionAimSeconds = 0.18f;
        [SerializeField, Min(0f)] private float executionShotDelaySeconds = 0.12f;
        [SerializeField, Min(0f)] private float executionKillDelaySeconds = 0.08f;
        [SerializeField, Min(0f)] private float executionFinishSeconds = 0.12f;
        [SerializeField] private Color executionShotColor = Color.white;
        [SerializeField] private Color executionImpactColor = new Color(0.9f, 0.02f, 0.04f, 1f);
        [SerializeField] private Color executionAbsorbColor = new Color(0.35f, 0.85f, 1f, 1f);
        [SerializeField, Min(0f)] private float executionImpactParticleSeconds = 0.55f;
        [SerializeField, Min(0)] private int executionImpactParticleCount = 28;
        [SerializeField, Min(0)] private int executionAbsorbParticleCount = 24;

        public float MaxDurability => maxDurability;
        public float MaxHeat => maxHeat;
        public float HeatCoolingPerSecond => heatCoolingPerSecond;
        public float OverheatSeconds => overheatSeconds;
        public float HeatAfterOverheatRatio => heatAfterOverheatRatio;
        public float MoveSpeed => moveSpeed;
        public float SprintSpeedMultiplier => sprintSpeedMultiplier;
        public float SprintHeatCoolingSuppressSeconds => sprintHeatCoolingSuppressSeconds;
        public float ActionHeatCoolingSuppressSeconds => actionHeatCoolingSuppressSeconds;
        public PlayerProjectile ProjectilePrefab => projectilePrefab;
        public float AttackDamage => attackDamage;
        public float AttackHeat => attackHeat;
        public float AttackRange => attackRange;
        public float AttackCooldown => attackCooldown;
        public float ProjectileSpeed => projectileSpeed;
        public float ProjectileLifetime => projectileLifetime;
        public float ProjectileRadius => projectileRadius;
        public float ProjectileTrailSeconds => projectileTrailSeconds;
        public float ProjectileTrailWidthMultiplier => projectileTrailWidthMultiplier;
        public float ProjectileGlowScale => projectileGlowScale;
        public float CombatEffectSeconds => combatEffectSeconds;
        public float GunAimHoldSeconds => gunAimHoldSeconds;
        public Color AttackEffectColor => attackEffectColor;
        public float ParryAimAngleDegrees => parryAimAngleDegrees;
        public float ParryShotCooldown => parryShotCooldown;
        public Color ParryEffectColor => parryEffectColor;
        public Color ParrySparkColor => parrySparkColor;
        public Color ParryRingColor => parryRingColor;
        public Color ParryRingGlitterColor => parryRingGlitterColor;
        public float ParrySparkSeconds => parrySparkSeconds;
        public float ParryRingSeconds => parryRingSeconds;
        public float ParryRingGlitterSeconds => parryRingGlitterSeconds;
        public int ParrySparkCount => parrySparkCount;
        public int ParryRingGlitterCount => parryRingGlitterCount;
        public float HitHeatToPlayer => hitHeatToPlayer;
        public float LockOnSearchRadius => lockOnSearchRadius;
        public float LockOnBreakDistance => lockOnBreakDistance;
        public float ExecutionRange => executionRange;
        public float ExecutionHeatRecovery => executionHeatRecovery;
        public bool DestroyTargetOnExecute => destroyTargetOnExecute;
        public float ExecutionStandOffDistance => executionStandOffDistance;
        public float ExecutionAimSeconds => executionAimSeconds;
        public float ExecutionShotDelaySeconds => executionShotDelaySeconds;
        public float ExecutionKillDelaySeconds => executionKillDelaySeconds;
        public float ExecutionFinishSeconds => executionFinishSeconds;
        public Color ExecutionShotColor => executionShotColor;
        public Color ExecutionImpactColor => executionImpactColor;
        public Color ExecutionAbsorbColor => executionAbsorbColor;
        public float ExecutionImpactParticleSeconds => executionImpactParticleSeconds;
        public int ExecutionImpactParticleCount => executionImpactParticleCount;
        public int ExecutionAbsorbParticleCount => executionAbsorbParticleCount;
    }
}
