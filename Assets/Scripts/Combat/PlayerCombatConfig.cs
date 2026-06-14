using UnityEngine;
using UnityEngine.Serialization;

namespace Week14.Combat
{
    [CreateAssetMenu(menuName = "Week14/Combat/Player Combat Config", fileName = "PlayerCombatConfig")]
    public sealed class PlayerCombatConfig : ScriptableObject
    {
        [Header("상태")]
        [Tooltip("플레이어의 최대 내구도입니다.")]
        [SerializeField, FormerlySerializedAs("maxHealth"), Min(1f)] private float maxDurability = 100f;
        [Tooltip("플레이어가 과열되기까지 필요한 최대 열기입니다.")]
        [SerializeField, Min(1f)] private float maxHeat = 100f;
        [Tooltip("열기 감소가 막혀 있지 않을 때 초당 감소하는 열기량입니다.")]
        [SerializeField, Min(0f)] private float heatCoolingPerSecond = 12f;
        [Tooltip("과열 상태가 유지되는 시간입니다. 이 시간 동안 플레이어는 행동할 수 없습니다.")]
        [SerializeField, Min(0f)] private float overheatSeconds = 1.4f;
        [Tooltip("기본 이동 속도입니다.")]
        [SerializeField, Min(0f)] private float moveSpeed = 5f;
        [Tooltip("질주할 때 기본 이동 속도에 곱해지는 배율입니다.")]
        [SerializeField, Min(1f)] private float sprintSpeedMultiplier = 1.5f;
        [Tooltip("질주 중 열기 자연 감소를 멈추는 시간입니다.")]
        [SerializeField, Min(0f)] private float sprintHeatCoolingSuppressSeconds = 0.12f;
        [Tooltip("공격, 패링탄, 방어 같은 행동 후 열기 자연 감소를 멈추는 시간입니다.")]
        [SerializeField, Min(0f)] private float actionHeatCoolingSuppressSeconds = 0.5f;

        [Header("왼손 권총")]
        [Tooltip("플레이어가 발사할 탄 프리팹입니다.")]
        [SerializeField] private PlayerProjectile projectilePrefab;
        [Tooltip("플레이어 공격, 패링, 피격, 처형 등에 사용할 공통 이펙트 데이터입니다.")]
        [SerializeField] private CombatEffectData effectData;
        [Tooltip("왼손 권총의 데미지입니다.")]
        [SerializeField, Min(0f)] private float attackDamage = 12f;
        [Tooltip("유효 거리 안에서 맞췄을 때 적에게 추가되는 열기량입니다.")]
        [SerializeField, Min(0f)] private float attackHeat = 16f;
        [Tooltip("왼손 권총을 다시 쏠 수 있을 때까지의 쿨다운입니다.")]
        [SerializeField, Min(0f)] private float attackCooldown = 0.28f;
        [Tooltip("이 거리 안에서 쏜 탄에 맞아야 적 열기 증가, 냉각 지연, 공격 끊김, 일반 피격 이펙트가 발생합니다.")]
        [SerializeField, FormerlySerializedAs("damageFalloffDistance"), Min(0f)] private float heatEffectiveRange = 5f;
        [Tooltip("유효 거리 안에서 맞은 적의 열기 자연 감소를 멈추는 시간입니다.")]
        [SerializeField, Min(0f)] private float targetHeatCoolingSuppressSeconds = 0.35f;
        [Tooltip("플레이어 탄의 이동 속도입니다.")]
        [SerializeField, Min(0f)] private float projectileSpeed = 12f;
        [Tooltip("플레이어 탄이 자동으로 사라지기까지의 시간입니다.")]
        [SerializeField, Min(0f)] private float projectileLifetime = 1f;
        [Tooltip("플레이어 탄의 충돌 반지름입니다.")]
        [SerializeField, Min(0f)] private float projectileRadius = 0.08f;
        [Tooltip("총을 조준 방향으로 고정해서 보여주는 시간입니다.")]
        [SerializeField, Min(0f)] private float gunAimHoldSeconds = 0.5f;

        [Header("오른손 패링 권총")]
        [Tooltip("오른손 패링탄으로 적 탄을 노릴 수 있는 최대 거리입니다.")]
        [SerializeField, FormerlySerializedAs("attackRange"), Min(0f)] private float parryRange = 7f;
        [Tooltip("우클릭 방어가 자동으로 적 탄을 요격할 수 있는 거리입니다.")]
        [SerializeField, FormerlySerializedAs("closeParryFailDistance"), Min(0f)] private float defenseRange = 1.2f;
        [Tooltip("방어 성공 시 플레이어에게 추가되는 열기량입니다.")]
        [SerializeField, FormerlySerializedAs("failedParryHeat"), Min(0f)] private float defenseHeat = 4f;
        [Tooltip("패링 성공 시 플레이어에게 추가되는 열기량입니다. 이 열기만으로는 과열되지 않습니다.")]
        [SerializeField, Min(0f)] private float parryHeat = 2f;
        [Tooltip("패링탄과 방어가 바라보는 방향 기준으로 허용하는 각도입니다.")]
        [SerializeField, Range(1f, 360f)] private float parryAimAngleDegrees = 65f;
        [Tooltip("오른손 패링탄을 다시 쏠 수 있을 때까지의 쿨다운입니다.")]
        [SerializeField, Min(0f)] private float parryShotCooldown = 0.18f;
        [Tooltip("플레이어가 적 공격에 맞았을 때 증가하는 열기량입니다.")]
        [SerializeField, Min(0f)] private float hitHeatToPlayer = 10f;

        [Header("락온")]
        [Tooltip("마우스 위치 주변에서 락온 대상을 찾는 반경입니다.")]
        [SerializeField, Min(0f)] private float lockOnSearchRadius = 2f;
        [Tooltip("락온 대상이 이 거리보다 멀어지면 락온이 해제됩니다.")]
        [SerializeField, Min(0f)] private float lockOnBreakDistance = 9f;

        [Header("처형")]
        [Tooltip("처형을 시작할 수 있는 대상과의 최대 거리입니다.")]
        [SerializeField, Min(0f)] private float executionRange = 1.2f;
        [Tooltip("처형 성공 시 플레이어 열기를 회복시키는 양입니다.")]
        [SerializeField, Min(0f)] private float executionHeatRecovery = 45f;
        [Tooltip("처형 완료 후 대상 오브젝트를 파괴할지 여부입니다.")]
        [SerializeField] private bool destroyTargetOnExecute = true;
        [Tooltip("처형 연출 중 플레이어가 대상과 유지하는 거리입니다.")]
        [SerializeField, Min(0f)] private float executionStandOffDistance = 0.65f;
        [Tooltip("처형 시작 후 조준 자세를 유지하는 시간입니다.")]
        [SerializeField, Min(0f)] private float executionAimSeconds = 0.18f;
        [Tooltip("처형 조준 후 실제 발사까지의 지연 시간입니다.")]
        [SerializeField, Min(0f)] private float executionShotDelaySeconds = 0.12f;
        [Tooltip("처형 발사 후 대상이 사망 처리되기까지의 지연 시간입니다.")]
        [SerializeField, Min(0f)] private float executionKillDelaySeconds = 0.08f;
        [Tooltip("처형 마무리 연출이 유지되는 시간입니다.")]
        [SerializeField, Min(0f)] private float executionFinishSeconds = 0.12f;
        [Tooltip("처형 중 카메라가 플레이어와 적 사이의 중심점을 따라가는 강도입니다. 1에 가까울수록 둘의 중간이 화면 중앙에 옵니다.")]
        [SerializeField, Range(0f, 1f)] private float executionCameraFocusWeight = 1f;
        [Tooltip("처형 중 카메라 줌 배율입니다. 낮을수록 더 가까이 확대됩니다.")]
        [SerializeField, Range(0.35f, 1f)] private float executionCameraZoomMultiplier = 0.62f;
        [Tooltip("처형 사격 전에 왼손 총 반동 타겟이 천천히 밀려나는 시간입니다.")]
        [SerializeField, Min(0.01f)] private float executionGunKickSeconds = 0.45f;
        [Tooltip("처형 사격 순간 왼손 총 반동 타겟이 원래 위치로 돌아오는 시간입니다.")]
        [SerializeField, Min(0.01f)] private float executionGunReturnSeconds = 0.045f;
        [Tooltip("처형 사격 순간 화면이 어두워졌다가 사라지는 시간입니다.")]
        [SerializeField, Min(0f)] private float executionShotDimSeconds = 0.065f;
        [Tooltip("처형 사격 순간 화면을 어둡게 덮는 최대 투명도입니다.")]
        [SerializeField, Range(0f, 1f)] private float executionShotDimAlpha = 0.72f;

        public float MaxDurability => maxDurability;
        public float MaxHeat => maxHeat;
        public float HeatCoolingPerSecond => heatCoolingPerSecond;
        public float OverheatSeconds => overheatSeconds;
        public float MoveSpeed => moveSpeed;
        public float SprintSpeedMultiplier => sprintSpeedMultiplier;
        public float SprintHeatCoolingSuppressSeconds => sprintHeatCoolingSuppressSeconds;
        public float ActionHeatCoolingSuppressSeconds => actionHeatCoolingSuppressSeconds;
        public PlayerProjectile ProjectilePrefab => projectilePrefab;
        public float AttackDamage => attackDamage;
        public float AttackHeat => attackHeat;
        public float AttackCooldown => attackCooldown;
        public float HeatEffectiveRange => heatEffectiveRange;
        public float TargetHeatCoolingSuppressSeconds => targetHeatCoolingSuppressSeconds;
        public float ProjectileSpeed => projectileSpeed;
        public float ProjectileLifetime => projectileLifetime;
        public float ProjectileRadius => projectileRadius;
        public float ProjectileTrailSeconds => effectData != null ? effectData.PlayerProjectileTrailSeconds : 0.08f;
        public float ProjectileTrailWidthMultiplier => effectData != null ? effectData.PlayerProjectileTrailWidthMultiplier : 2.8f;
        public float GunAimHoldSeconds => gunAimHoldSeconds;
        public Color AttackEffectColor => effectData != null ? effectData.AttackEffectColor : new Color(1f, 0.35f, 0.12f, 0.55f);
        public float ParryRange => parryRange;
        public float DefenseRange => defenseRange;
        public float DefenseHeat => defenseHeat;
        public float ParryHeat => parryHeat;
        public float ParryAimAngleDegrees => parryAimAngleDegrees;
        public float ParryShotCooldown => parryShotCooldown;
        public Color ParryEffectColor => effectData != null ? effectData.ParryEffectColor : new Color(0.2f, 0.65f, 1f, 0.45f);
        public Color EnemyProjectileColor => effectData != null ? effectData.EnemyProjectileColor : new Color(1f, 0.95f, 0.25f, 1f);
        public Color ParrySparkColor => effectData != null ? effectData.ParrySparkColor : new Color(1f, 0.88f, 0.35f, 1f);
        public Color ParryRingColor => effectData != null ? effectData.ParryRingColor : new Color(0.45f, 0.9f, 1f, 0.75f);
        public Color ParryRingGlitterColor => effectData != null ? effectData.ParryRingGlitterColor : new Color(1f, 0.96f, 0.68f, 1f);
        public float ParrySparkSeconds => effectData != null ? effectData.ParrySparkSeconds : 0.22f;
        public float ParryRingSeconds => effectData != null ? effectData.ParryRingSeconds : 0.32f;
        public float ParryRingGlitterSeconds => effectData != null ? effectData.ParryRingGlitterSeconds : 0.2f;
        public int ParrySparkCount => effectData != null ? effectData.ParrySparkCount : 34;
        public int ParryRingGlitterCount => effectData != null ? effectData.ParryRingGlitterCount : 18;
        public int ParryFlameCount => effectData != null ? effectData.ParryFlameCount : 20;
        public float ParryEffectScale => effectData != null ? effectData.ParryEffectScale : 1f;
        public int DefenseSparkCount => effectData != null ? effectData.DefenseSparkCount : 18;
        public float DefenseEffectScale => effectData != null ? effectData.DefenseEffectScale : 1f;
        public int PlayerHitSparkCount => effectData != null ? effectData.PlayerHitSparkCount : 12;
        public int PlayerHitBackSparkCount => effectData != null ? effectData.PlayerHitBackSparkCount : 5;
        public int PlayerHitFlameCount => effectData != null ? effectData.PlayerHitFlameCount : 6;
        public float PlayerHitEffectScale => effectData != null ? effectData.PlayerHitEffectScale : 0.55f;
        public Color PlayerBodyOverheatedColor => effectData != null ? effectData.PlayerBodyOverheatedColor : new Color(1f, 0.2f, 0.12f, 1f);
        public Color PlayerBodyHitColor => effectData != null ? effectData.PlayerBodyHitColor : new Color(1f, 0.85f, 0.25f, 1f);
        public float BodyHitColorSeconds => effectData != null ? effectData.BodyHitColorSeconds : 0.08f;
        public Color HeatParryOutlineColor => effectData != null ? effectData.HeatParryOutlineColor : Color.white;
        public Color HeatDefenseOutlineColor => effectData != null ? effectData.HeatDefenseOutlineColor : new Color(0.55f, 0.55f, 0.55f, 1f);
        public Color HeatHitOutlineColor => effectData != null ? effectData.HeatHitOutlineColor : Color.yellow;
        public Color HeatOverheatBlinkOutlineColor => effectData != null ? effectData.HeatOverheatBlinkOutlineColor : Color.yellow;
        public Color HeatOverheatBlinkFillColor => effectData != null ? effectData.HeatOverheatBlinkFillColor : Color.yellow;
        public float HeatOverheatBlinkFrequency => effectData != null ? effectData.HeatOverheatBlinkFrequency : 5f;
        public float HeatOverheatBlinkMinAlpha => effectData != null ? effectData.HeatOverheatBlinkMinAlpha : 0.25f;
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
        public float ExecutionCameraFocusWeight => executionCameraFocusWeight;
        public float ExecutionCameraZoomMultiplier => executionCameraZoomMultiplier;
        public float ExecutionGunKickSeconds => executionGunKickSeconds;
        public float ExecutionGunReturnSeconds => executionGunReturnSeconds;
        public float ExecutionShotDimSeconds => executionShotDimSeconds;
        public float ExecutionShotDimAlpha => executionShotDimAlpha;
        public Color ExecutionShotColor => effectData != null ? effectData.ExecutionShotColor : Color.white;
        public Color ExecutionImpactColor => effectData != null ? effectData.ExecutionImpactColor : new Color(0.9f, 0.02f, 0.04f, 1f);
        public Color ExecutionAbsorbColor => effectData != null ? effectData.ExecutionAbsorbColor : new Color(0.35f, 0.85f, 1f, 1f);
        public float ExecutionImpactParticleSeconds => effectData != null ? effectData.ExecutionImpactParticleSeconds : 0.55f;
        public int ExecutionImpactParticleCount => effectData != null ? effectData.ExecutionImpactParticleCount : 28;
        public int ExecutionAbsorbParticleCount => effectData != null ? effectData.ExecutionAbsorbParticleCount : 24;
    }
}
