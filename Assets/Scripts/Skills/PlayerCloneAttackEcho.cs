using System;
using System.Collections;
using UnityEngine;
using Week14.Combat;
using Week14.Enemy;
using Week14.Weapons;

namespace Week14.Skills
{
    public sealed class PlayerCloneAttackEcho : MonoBehaviour
    {
        private const int CloneSortingOffset = -1;

        private PlayerCombatController owner;
        private GameObject cloneVisualRoot;
        private Coroutine activeRoutine;
        private Action onEnded;
        private Transform cloneSourceRoot;
        private Vector3 cloneVisualBaseScale = Vector3.one;
        private CloneRendererBinding[] rendererBindings = Array.Empty<CloneRendererBinding>();
        private float damageMultiplier = 0.5f;
        private Color cloneTint = new Color(0.45f, 0.9f, 1f, 0.55f);
        private float activeStartedAt;
        private float activeDurationSeconds = 0.1f;

        private enum CloneFacing
        {
            Front,
            Side,
            Back
        }

        private enum RendererFacingGroup
        {
            Any,
            Front,
            Side,
            Back
        }

        private struct CloneRendererBinding
        {
            public SpriteRenderer Source;
            public SpriteRenderer Clone;
            public RendererFacingGroup FacingGroup;
            public Color BaseColor;
            public bool FollowAttackMotion;
            public Sprite BaseSprite;
            public Vector3 BaseLocalPosition;
            public Quaternion BaseLocalRotation;
            public Vector3 BaseLocalScale;
            public bool BaseFlipX;
            public bool BaseFlipY;
        }

        public void Activate(
            PlayerCombatController controller,
            float durationSeconds,
            float nextDamageMultiplier,
            Color nextCloneTint,
            Action nextOnEnded)
        {
            ClearActive();
            owner = controller;
            damageMultiplier = Mathf.Clamp01(nextDamageMultiplier);
            cloneTint = nextCloneTint;
            onEnded = nextOnEnded;
            activeStartedAt = Time.time;
            activeDurationSeconds = Mathf.Max(0.1f, durationSeconds);

            if (owner == null)
            {
                onEnded?.Invoke();
                return;
            }

            SpawnCloneVisual();
            owner.PlayerAttackPerformed += HandlePlayerAttackPerformed;
            activeRoutine = StartCoroutine(DurationRoutine(durationSeconds));
        }

        private void LateUpdate()
        {
            UpdateCloneVisual();
        }

        private void HandlePlayerAttackPerformed(PlayerCombatController.PlayerAttackEchoInfo attackInfo)
        {
            if (owner == null || cloneVisualRoot == null || attackInfo.Damage <= 0)
            {
                return;
            }

            PlayerCombatConfig config = owner.Config;
            if (config == null || config.ProjectilePrefab == null)
            {
                return;
            }

            BossAI boss = ResolveTargetBoss();
            if (!TryGetDirectionToBoss(boss, cloneVisualRoot.transform.position, out Vector2 direction))
            {
                return;
            }

            Vector2 origin = cloneVisualRoot.transform.position;
            int cloneDamage = Mathf.Max(1, Mathf.CeilToInt(attackInfo.Damage * damageMultiplier));
            BaseWeaponSO weapon = attackInfo.Weapon;

            if (weapon is ShotgunWeaponSO shotgun)
            {
                FireCloneSpread(config, weapon, origin, direction, cloneDamage, shotgun.PelletCount, shotgun.SpreadAngle);
                return;
            }

            if (weapon is RailgunWeaponSO railgun)
            {
                FireCloneLaser(origin, direction, cloneDamage, railgun, attackInfo.AmmoSpent);
                return;
            }

            if (weapon is BayonetWeaponSO bayonet)
            {
                float range = attackInfo.Range > 0f ? attackInfo.Range : bayonet.AttackRange;
                SwingCloneBayonet(origin, direction, range, cloneDamage, bayonet.RangeFlashColor, bayonet.RangeFlashSeconds);
                return;
            }

            if (weapon is BaseballBatWeaponSO baseballBat)
            {
                float range = attackInfo.Range > 0f ? attackInfo.Range : baseballBat.MaxAttackRange;
                float speed = attackInfo.ReflectedProjectileSpeed > 0f
                    ? attackInfo.ReflectedProjectileSpeed
                    : baseballBat.ReflectedProjectileSpeed;
                SwingCloneBaseballBat(
                    origin,
                    direction,
                    range,
                    cloneDamage,
                    speed,
                    baseballBat.VfxSettings,
                    baseballBat.GetCharge01FromRange(range));
                return;
            }

            FireCloneSpread(config, weapon, origin, direction, cloneDamage, 1, 0f);
        }

        private void FireCloneSpread(
            PlayerCombatConfig config,
            BaseWeaponSO weapon,
            Vector2 origin,
            Vector2 direction,
            int damage,
            int pelletCount,
            float spreadAngle)
        {
            if (config == null || damage <= 0 || pelletCount <= 0)
            {
                return;
            }

            PlayerProjectile projectilePrefab = weapon != null && weapon.ProjectilePrefab != null
                ? weapon.ProjectilePrefab
                : config.ProjectilePrefab;
            if (projectilePrefab == null)
            {
                return;
            }

            float startAngle = -spreadAngle * 0.5f;
            float angleStep = pelletCount > 1 ? spreadAngle / (pelletCount - 1) : 0f;
            for (int i = 0; i < pelletCount; i++)
            {
                float angle = startAngle + angleStep * i;
                Vector2 pelletDirection = Quaternion.Euler(0f, 0f, angle) * direction;
                PlayerProjectile.Spawn(
                    projectilePrefab,
                    origin,
                    pelletDirection,
                    owner,
                    config.ProjectileSpeed,
                    config.ProjectileLifetime,
                    config.ProjectileRadius,
                    damage,
                    cloneTint,
                    true,
                    isSkillShot: true);
            }

            ProjectileVfx.PlayPrefab(
                config.PlayerMuzzleFlashVfxPrefab,
                origin,
                direction,
                cloneVisualRoot.transform,
                0.8f);
        }

        private void FireCloneLaser(
            Vector2 origin,
            Vector2 direction,
            int damage,
            RailgunWeaponSO railgun,
            int spentAmmo)
        {
            if (railgun == null || damage <= 0)
            {
                return;
            }

            float beamLength = Mathf.Max(0.1f, railgun.LaserSpeed * railgun.LaserLifetimeSeconds);
            DamageEnemiesAlongLine(origin, direction, beamLength, railgun.BeamWidth, damage);
            Vector3 beamEnd = origin + direction * beamLength;
            RailgunVfxSettings vfxSettings = railgun.VfxSettings;
            GameObject beamPrefab = vfxSettings.ResolveBeamPrefab(Mathf.Max(1, spentAmmo));
            if (beamPrefab != null)
            {
                ProjectileVfx.PlayAnchoredBeamPrefab(
                    beamPrefab,
                    cloneVisualRoot.transform,
                    origin,
                    direction,
                    beamLength,
                    vfxSettings.BeamLengthAxis == RailgunBeamLengthAxis.LocalY,
                    vfxSettings.MuzzleOffset,
                    vfxSettings.RotationOffsetDegrees,
                    vfxSettings.PlaybackSpeed,
                    vfxSettings.SortingOrder);
            }
            else
            {
                ProjectileVfx.PlayShotLine(origin, beamEnd, cloneTint, railgun.BeamVisualSeconds, railgun.BeamWidth);
            }

            GameObject railgunMuzzleFlashPrefab = vfxSettings.ResolveMuzzleFlashPrefab(spentAmmo);
            GameObject muzzleFlashPrefab = railgunMuzzleFlashPrefab != null
                ? railgunMuzzleFlashPrefab
                : owner.Config.PlayerMuzzleFlashVfxPrefab;
            ProjectileVfx.PlayPrefab(
                muzzleFlashPrefab,
                origin,
                direction,
                cloneVisualRoot.transform);
        }

        private void SwingCloneBayonet(Vector2 origin, Vector2 direction, float range, int damage, Color flashColor, float flashSeconds)
        {
            if (range <= 0f)
            {
                return;
            }

            ClearProjectilesInSemicircle(origin, direction, range);
            DamageEnemiesInSemicircle(origin, direction, range, damage);
            ProjectileVfx.PlaySemicircleFlash(origin, direction, range, flashColor, flashSeconds);
        }

        private void SwingCloneBaseballBat(
            Vector2 origin,
            Vector2 direction,
            float range,
            int damage,
            float reflectedSpeed,
            BaseballBatVfxSettings vfxSettings,
            float charge01)
        {
            if (range <= 0f)
            {
                return;
            }

            if (vfxSettings != null)
            {
                // PlayAnchoredPrefab은 spawnParent(cloneVisualRoot)의 lossyScale을 그대로 곱해서 적용합니다.
                // cloneVisualRoot는 플레이어 몸통 스케일을 복제해서 쓰기 때문에 스케일이 1이 아니고,
                // 실제 배트 스윙이 붙는 BaseballBatVfxAnchor도 (그 조상 오브젝트 스케일 때문에) 1이 아닙니다.
                // 두 앵커의 실제 런타임 lossyScale 비율만큼 미리 보정해야 실제 스윙과 같은 월드 크기로 나옵니다.
                Transform realAnchor = owner != null ? owner.Context.BaseballBatVfxAnchor : null;
                Vector3 realAnchorScale = realAnchor != null ? realAnchor.lossyScale : Vector3.one;
                Vector3 cloneParentScale = cloneVisualRoot.transform.lossyScale;
                Vector3 scaleRatio = new Vector3(
                    GetSafeScaleRatio(realAnchorScale.x, cloneParentScale.x),
                    GetSafeScaleRatio(realAnchorScale.y, cloneParentScale.y),
                    GetSafeScaleRatio(realAnchorScale.z, cloneParentScale.z));

                Vector2 rawOffset = vfxSettings.GetRightFacingLocalOffset(charge01);
                Vector2 compensatedOffset = new Vector2(
                    rawOffset.x * scaleRatio.x,
                    rawOffset.y * scaleRatio.y);

                Vector3 rawScale = vfxSettings.GetLocalScale(range);
                Vector3 compensatedScale = Vector3.Scale(rawScale, scaleRatio);

                ProjectileVfx.PlayAnchoredPrefab(
                    vfxSettings.ResolveSwingVfxPrefab(charge01),
                    cloneVisualRoot.transform,
                    direction,
                    compensatedOffset,
                    vfxSettings.RotationOffsetDegrees,
                    compensatedScale,
                    vfxSettings.PlaybackSpeed,
                    vfxSettings.SortingOrder);
                ProjectileVfx.PlaySemicircleFlash(
                    origin,
                    direction,
                    range,
                    vfxSettings.RangeIndicatorColor,
                    vfxSettings.RangeIndicatorSeconds);
            }

            float hitDelaySeconds = vfxSettings != null ? vfxSettings.AttackHitDelaySeconds : 0f;
            float activeSeconds = vfxSettings != null ? vfxSettings.AttackActiveSeconds : 0.01f;
            StartCoroutine(ResolveCloneBaseballBatHitsDuringWindow(
                hitDelaySeconds,
                activeSeconds,
                origin,
                direction,
                range,
                damage,
                reflectedSpeed));
        }

        private IEnumerator ResolveCloneBaseballBatHitsDuringWindow(
            float delaySeconds,
            float activeSeconds,
            Vector2 fallbackOrigin,
            Vector2 direction,
            float range,
            int damage,
            float reflectedSpeed)
        {
            if (delaySeconds > 0f)
            {
                yield return new WaitForSeconds(delaySeconds);
            }

            float activeEndsAt = Time.time + Mathf.Max(0.01f, activeSeconds);
            do
            {
                Vector2 origin = cloneVisualRoot != null
                    ? cloneVisualRoot.transform.position
                    : fallbackOrigin;
                DestroyDeployedConductorTurretsInSemicircle(origin, direction, range);
                ReflectProjectilesInSemicircle(origin, direction, range, damage, reflectedSpeed);
                yield return null;
            }
            while (Time.time < activeEndsAt);
        }

        // targetScale/parentScale 둘 다 부호(좌우 반전)는 무시하고 크기 비율만 계산합니다.
        // 반전에 따른 회전 보정은 PlayAnchoredPrefab의 InverseTransformDirection이 이미 처리합니다.
        private static float GetSafeScaleRatio(float targetScale, float parentScale)
        {
            float safeParentScale = Mathf.Max(0.0001f, Mathf.Abs(parentScale));
            return Mathf.Abs(targetScale) / safeParentScale;
        }

        private BossAI ResolveTargetBoss()
        {
            BossAI[] bosses = FindObjectsByType<BossAI>(FindObjectsSortMode.None);
            for (int i = 0; i < bosses.Length; i++)
            {
                BossAI boss = bosses[i];
                if (boss != null && boss.IsCombatStarted && boss.Health != null && !boss.Health.IsDead)
                {
                    return boss;
                }
            }

            for (int i = 0; i < bosses.Length; i++)
            {
                BossAI boss = bosses[i];
                if (boss != null && boss.Health != null && !boss.Health.IsDead)
                {
                    return boss;
                }
            }

            return null;
        }

        private void SpawnCloneVisual()
        {
            SpriteRenderer[] sourceRenderers = owner.Context.BodyRenderers;
            if (sourceRenderers == null || sourceRenderers.Length == 0)
            {
                cloneVisualRoot = new GameObject("PlayerCloneEcho");
                cloneVisualRoot.transform.position = owner.Context.CombatCenterOrigin.position;
                rendererBindings = Array.Empty<CloneRendererBinding>();
                return;
            }

            cloneVisualRoot = new GameObject("PlayerCloneEcho");
            cloneVisualRoot.transform.position = owner.Context.CombatCenterOrigin.position;
            cloneSourceRoot = owner.Context.BodyRoot != null ? owner.Context.BodyRoot : owner.transform;
            cloneVisualBaseScale = GetAbsoluteLossyScale(cloneSourceRoot);
            cloneVisualRoot.transform.localScale = cloneVisualBaseScale;

            Color[] baseColors = owner.Context.BodyBaseColors;
            Sprite[] baseSprites = owner.Context.BodyBaseSprites;
            Vector3[] baseLocalPositions = owner.Context.BodyBaseLocalPositions;
            Quaternion[] baseLocalRotations = owner.Context.BodyBaseLocalRotations;
            Vector3[] baseLocalScales = owner.Context.BodyBaseLocalScales;
            bool[] baseFlipX = owner.Context.BodyBaseFlipX;
            bool[] baseFlipY = owner.Context.BodyBaseFlipY;
            BossAI boss = ResolveTargetBoss();
            TryGetDirectionToBoss(boss, cloneVisualRoot.transform.position, out Vector2 aimDirection);
            bool hasRenderer = false;
            CloneRendererBinding[] bindings = new CloneRendererBinding[sourceRenderers.Length];
            int bindingCount = 0;
            for (int i = 0; i < sourceRenderers.Length; i++)
            {
                SpriteRenderer source = sourceRenderers[i];
                if (source == null)
                {
                    continue;
                }

                GameObject cloneObject = new GameObject(source.name) { layer = source.gameObject.layer };
                cloneObject.transform.SetParent(cloneVisualRoot.transform, false);
                bool followAttackMotion = IsAttackMotionRenderer(source.transform);
                Vector3 baseLocalPosition = baseLocalPositions != null && i < baseLocalPositions.Length
                    ? baseLocalPositions[i]
                    : cloneSourceRoot.InverseTransformPoint(source.transform.position);
                Quaternion baseLocalRotation = baseLocalRotations != null && i < baseLocalRotations.Length
                    ? baseLocalRotations[i]
                    : source.transform.localRotation;
                Vector3 baseLocalScale = baseLocalScales != null && i < baseLocalScales.Length
                    ? baseLocalScales[i]
                    : source.transform.localScale;
                ApplyCloneRendererTransform(
                    source.transform,
                    cloneObject.transform,
                    followAttackMotion,
                    baseLocalPosition,
                    baseLocalRotation,
                    baseLocalScale,
                    aimDirection);

                SpriteRenderer clone = cloneObject.AddComponent<SpriteRenderer>();
                Sprite baseSprite = baseSprites != null && i < baseSprites.Length ? baseSprites[i] : source.sprite;
                bool sourceBaseFlipX = baseFlipX != null && i < baseFlipX.Length ? baseFlipX[i] : source.flipX;
                bool sourceBaseFlipY = baseFlipY != null && i < baseFlipY.Length ? baseFlipY[i] : source.flipY;
                clone.sprite = followAttackMotion ? source.sprite : baseSprite;
                clone.flipX = followAttackMotion ? source.flipX : sourceBaseFlipX;
                clone.flipY = followAttackMotion ? source.flipY : sourceBaseFlipY;
                clone.material = source.sharedMaterial;
                clone.sortingLayerID = source.sortingLayerID;
                clone.sortingOrder = source.sortingOrder + CloneSortingOffset;
                clone.maskInteraction = source.maskInteraction;
                Color baseColor = baseColors != null && i < baseColors.Length ? baseColors[i] : source.color;
                clone.color = MultiplyColor(baseColor, cloneTint);

                bindings[bindingCount++] = new CloneRendererBinding
                {
                    Source = source,
                    Clone = clone,
                    FacingGroup = ResolveFacingGroup(source.transform),
                    BaseColor = baseColor,
                    FollowAttackMotion = followAttackMotion,
                    BaseSprite = baseSprite,
                    BaseLocalPosition = baseLocalPosition,
                    BaseLocalRotation = baseLocalRotation,
                    BaseLocalScale = baseLocalScale,
                    BaseFlipX = sourceBaseFlipX,
                    BaseFlipY = sourceBaseFlipY
                };
                hasRenderer = true;
            }

            rendererBindings = new CloneRendererBinding[bindingCount];
            Array.Copy(bindings, rendererBindings, bindingCount);

            if (!hasRenderer)
            {
                Destroy(cloneVisualRoot);
                cloneVisualRoot = new GameObject("PlayerCloneEcho");
                cloneVisualRoot.transform.position = owner.Context.CombatCenterOrigin.position;
                rendererBindings = Array.Empty<CloneRendererBinding>();
            }

            UpdateCloneVisual();
        }

        private IEnumerator DurationRoutine(float durationSeconds)
        {
            yield return new WaitForSeconds(activeDurationSeconds);
            Action ended = onEnded;
            ClearActive();
            ended?.Invoke();
        }

        private void ClearActive()
        {
            if (activeRoutine != null)
            {
                StopCoroutine(activeRoutine);
                activeRoutine = null;
            }

            if (owner != null)
            {
                owner.PlayerAttackPerformed -= HandlePlayerAttackPerformed;
            }

            if (cloneVisualRoot != null)
            {
                Destroy(cloneVisualRoot);
                cloneVisualRoot = null;
            }

            cloneSourceRoot = null;
            cloneVisualBaseScale = Vector3.one;
            rendererBindings = Array.Empty<CloneRendererBinding>();
            activeStartedAt = 0f;
            activeDurationSeconds = 0.1f;
            owner = null;
            onEnded = null;
        }

        private void OnDisable()
        {
            ClearActive();
        }

        private static Color MultiplyColor(Color source, Color tint)
        {
            return new Color(
                source.r * tint.r,
                source.g * tint.g,
                source.b * tint.b,
                source.a * tint.a);
        }

        private void UpdateCloneVisual()
        {
            if (owner == null || cloneVisualRoot == null)
            {
                return;
            }

            BossAI boss = ResolveTargetBoss();
            TryGetDirectionToBoss(boss, cloneVisualRoot.transform.position, out Vector2 direction);
            CloneFacing facing = GetFacing(direction);
            ApplyCloneFlip(direction);
            float blinkAlphaMultiplier = GetExpirationBlinkAlphaMultiplier();

            for (int i = 0; i < rendererBindings.Length; i++)
            {
                CloneRendererBinding binding = rendererBindings[i];
                if (binding.Source == null || binding.Clone == null)
                {
                    continue;
                }

                if (binding.FollowAttackMotion)
                {
                    ApplyCloneRendererTransform(
                        binding.Source.transform,
                        binding.Clone.transform,
                        true,
                        binding.BaseLocalPosition,
                        binding.BaseLocalRotation,
                        binding.BaseLocalScale,
                        direction);
                    binding.Clone.sprite = binding.Source.sprite;
                    binding.Clone.flipX = binding.Source.flipX;
                    binding.Clone.flipY = binding.Source.flipY;
                    binding.Clone.enabled = binding.Source.enabled && binding.Source.sprite != null;
                }
                else
                {
                    ApplyCloneRendererTransform(
                        binding.Source.transform,
                        binding.Clone.transform,
                        false,
                        binding.BaseLocalPosition,
                        binding.BaseLocalRotation,
                        binding.BaseLocalScale,
                        direction);
                    binding.Clone.sprite = binding.BaseSprite;
                    binding.Clone.flipX = binding.BaseFlipX;
                    binding.Clone.flipY = binding.BaseFlipY;
                    binding.Clone.enabled = binding.BaseSprite != null;
                }

                Color color = MultiplyColor(binding.BaseColor, cloneTint);
                color.a = ShouldShowFacingGroup(binding.FacingGroup, facing) ? color.a : 0f;
                color.a *= blinkAlphaMultiplier;
                binding.Clone.color = color;
            }
        }

        private float GetExpirationBlinkAlphaMultiplier()
        {
            if (activeDurationSeconds <= 0.1f)
            {
                return 1f;
            }

            float elapsed = Time.time - activeStartedAt;
            float blinkStart = activeDurationSeconds * 0.8f;
            if (elapsed < blinkStart)
            {
                return 1f;
            }

            float warningProgress = Mathf.InverseLerp(blinkStart, activeDurationSeconds, elapsed);
            float blinkSpeed = Mathf.Lerp(8f, 16f, warningProgress);
            float blink = Mathf.PingPong(Time.time * blinkSpeed, 1f);
            return Mathf.Lerp(0.25f, 1f, blink);
        }

        private void ApplyCloneRendererTransform(
            Transform source,
            Transform clone,
            bool followSourceMotion,
            Vector3 baseLocalPosition,
            Quaternion baseLocalRotation,
            Vector3 baseLocalScale,
            Vector2 aimDirection)
        {
            if (source == null || clone == null)
            {
                return;
            }

            if (!followSourceMotion)
            {
                clone.localPosition = baseLocalPosition;
                clone.localRotation = baseLocalRotation;
                clone.localScale = baseLocalScale;
                return;
            }

            if (cloneSourceRoot == null)
            {
                clone.localPosition = baseLocalPosition + Vector3.up * (source.localPosition.y - baseLocalPosition.y);
                clone.localRotation = GetCloneAimRotation(aimDirection) * baseLocalRotation;
                clone.localScale = baseLocalScale;
                return;
            }

            Vector3 sourceLocalPosition = cloneSourceRoot.InverseTransformPoint(source.position);
            clone.localPosition = baseLocalPosition + Vector3.up * (sourceLocalPosition.y - baseLocalPosition.y);
            clone.localRotation = GetCloneAimRotation(aimDirection) * baseLocalRotation;
            clone.localScale = baseLocalScale;
        }

        private Quaternion GetCloneAimRotation(Vector2 aimDirection)
        {
            if (aimDirection.sqrMagnitude <= 0.0001f)
            {
                return Quaternion.identity;
            }

            Vector2 localDirection = cloneVisualRoot != null
                ? cloneVisualRoot.transform.InverseTransformVector(aimDirection)
                : aimDirection;
            if (localDirection.sqrMagnitude <= 0.0001f)
            {
                localDirection = Vector2.right;
            }

            float angle = Mathf.Atan2(localDirection.y, localDirection.x) * Mathf.Rad2Deg;
            return Quaternion.Euler(0f, 0f, angle);
        }

        private void ApplyCloneFlip(Vector2 direction)
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Vector3 scale = cloneVisualRoot.transform.localScale;
            float xMagnitude = Mathf.Max(0.001f, Mathf.Abs(cloneVisualBaseScale.x));
            scale.x = direction.x < 0f ? -xMagnitude : xMagnitude;
            scale.y = cloneVisualBaseScale.y;
            scale.z = cloneVisualBaseScale.z;
            cloneVisualRoot.transform.localScale = scale;
        }

        private static Vector3 GetAbsoluteLossyScale(Transform source)
        {
            if (source == null)
            {
                return Vector3.one;
            }

            Vector3 scale = source.lossyScale;
            return new Vector3(
                Mathf.Max(0.001f, Mathf.Abs(scale.x)),
                Mathf.Max(0.001f, Mathf.Abs(scale.y)),
                Mathf.Max(0.001f, Mathf.Abs(scale.z)));
        }

        private static bool TryGetDirectionToBoss(BossAI boss, Vector2 origin, out Vector2 direction)
        {
            Transform target = boss != null ? (boss.BodyRoot != null ? boss.BodyRoot : boss.transform) : null;
            direction = target != null ? (Vector2)target.position - origin : Vector2.right;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = Vector2.right;
            }

            direction.Normalize();
            return target != null;
        }

        private static CloneFacing GetFacing(Vector2 direction)
        {
            float angleFromDown = Vector2.Angle(Vector2.down, direction);
            if (angleFromDown <= 50f)
            {
                return CloneFacing.Front;
            }

            if (angleFromDown >= 130f)
            {
                return CloneFacing.Back;
            }

            return CloneFacing.Side;
        }

        private static bool ShouldShowFacingGroup(RendererFacingGroup group, CloneFacing facing)
        {
            return group == RendererFacingGroup.Any
                || (group == RendererFacingGroup.Front && facing == CloneFacing.Front)
                || (group == RendererFacingGroup.Side && facing == CloneFacing.Side)
                || (group == RendererFacingGroup.Back && facing == CloneFacing.Back);
        }

        private static RendererFacingGroup ResolveFacingGroup(Transform source)
        {
            for (Transform current = source; current != null; current = current.parent)
            {
                if (current.name.Contains("Front"))
                {
                    return RendererFacingGroup.Front;
                }

                if (current.name.Contains("Side"))
                {
                    return RendererFacingGroup.Side;
                }

                if (current.name.Contains("Back"))
                {
                    return RendererFacingGroup.Back;
                }
            }

            return RendererFacingGroup.Any;
        }

        private static bool IsAttackMotionRenderer(Transform source)
        {
            for (Transform current = source; current != null; current = current.parent)
            {
                string name = current.name;
                if (name.Contains("Arm_L") || name.Contains("Weapon"))
                {
                    return true;
                }
            }

            return false;
        }

        private void DamageEnemiesAlongLine(Vector2 origin, Vector2 direction, float length, float beamRadius, int damage)
        {
            if (damage <= 0 || length <= 0f)
            {
                return;
            }

            RaycastHit2D[] hits = Physics2D.CircleCastAll(origin, Mathf.Max(0.01f, beamRadius), direction, length);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            Health[] hitTargets = new Health[hits.Length];
            int hitCount = 0;
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D collider = hits[i].collider;
                if (collider == null)
                {
                    continue;
                }

                Health targetHealth = collider.GetComponentInParent<Health>();
                if (!IsValidAreaDamageTarget(targetHealth) || ContainsHealth(hitTargets, hitCount, targetHealth))
                {
                    continue;
                }

                hitTargets[hitCount++] = targetHealth;
                PlayerProjectile.TryApplyDamageToHealth(
                    targetHealth,
                    damage,
                    true,
                    targetHealth.transform.position,
                    direction,
                    cloneTint,
                    owner?.Config?.EnemyHitVfxPrefab);
            }
        }

        private void ClearProjectilesInSemicircle(Vector2 origin, Vector2 direction, float range)
        {
            var activeProjectiles = EnemyProjectile.ActiveProjectiles;
            for (int i = activeProjectiles.Count - 1; i >= 0; i--)
            {
                EnemyProjectile projectile = activeProjectiles[i];
                if (projectile == null || !projectile.CanBeIntercepted || !OverlapsSemicircle(projectile, origin, direction, range))
                {
                    continue;
                }

                PlayerDashVfx.PlayProjectileAbsorb(
                    owner,
                    projectile,
                    origin,
                    0.16f,
                    new Color(cloneTint.r, cloneTint.g, cloneTint.b, 0.85f));
                projectile.TryDestroyByInterceptShot(out _);
            }
        }

        private void ReflectProjectilesInSemicircle(Vector2 origin, Vector2 direction, float range, int damage, float reflectedSpeed)
        {
            var activeProjectiles = EnemyProjectile.ActiveProjectiles;
            for (int i = activeProjectiles.Count - 1; i >= 0; i--)
            {
                EnemyProjectile projectile = activeProjectiles[i];
                if (projectile == null || !projectile.CanBeReflected || !OverlapsSemicircle(projectile, origin, direction, range))
                {
                    continue;
                }

                if (projectile.TryReflectTowardOwnerBoss(reflectedSpeed, damage, out _))
                {
                    PlayerDashVfx.PlayProjectileAbsorb(
                        owner,
                        projectile,
                        origin,
                        0.12f,
                        new Color(1f, 0.65f, 0.25f, 0.85f));
                }
            }
        }

        private static void DestroyDeployedConductorTurretsInSemicircle(
            Vector2 origin,
            Vector2 direction,
            float range)
        {
            var activeProjectiles = EnemyProjectile.ActiveProjectiles;
            for (int i = activeProjectiles.Count - 1; i >= 0; i--)
            {
                if (activeProjectiles[i] is not ConductorTurretProjectile turret
                    || !turret.IsPlayerTargetable
                    || !OverlapsSemicircle(turret, origin, direction, range))
                {
                    continue;
                }

                turret.TryDestroyByBaseballBat(turret.transform.position, direction);
            }
        }

        private void DamageEnemiesInSemicircle(Vector2 origin, Vector2 direction, float range, int damage)
        {
            if (damage <= 0)
            {
                return;
            }

            Health[] allHealth = FindObjectsByType<Health>(FindObjectsSortMode.None);
            for (int i = 0; i < allHealth.Length; i++)
            {
                Health targetHealth = allHealth[i];
                if (!IsValidAreaDamageTarget(targetHealth) || !OverlapsSemicircle(targetHealth, origin, direction, range))
                {
                    continue;
                }

                PlayerProjectile.TryApplyDamageToHealth(
                    targetHealth,
                    damage,
                    true,
                    targetHealth.transform.position,
                    direction,
                    cloneTint,
                    owner?.Config?.EnemyHitVfxPrefab);
            }
        }

        private bool IsValidAreaDamageTarget(Health targetHealth)
        {
            return targetHealth != null
                && owner != null
                && targetHealth != owner.Health
                && !targetHealth.IsDead
                && (targetHealth.GetComponent<BossAI>() != null
                    || targetHealth.GetComponentInParent<BossAI>() != null
                    || targetHealth.GetComponent<Minion>() != null
                    || targetHealth.GetComponentInParent<Minion>() != null);
        }

        private static bool OverlapsSemicircle(Component target, Vector2 origin, Vector2 direction, float range)
        {
            float sqrRange = range * range;
            if (TryGetColliderBounds(target.gameObject, out Bounds bounds))
            {
                return BoundsOverlapsSemicircle(bounds, origin, direction, sqrRange);
            }

            return IsPointInSemicircle(target.transform.position, origin, direction, sqrRange);
        }

        private static bool TryGetColliderBounds(GameObject target, out Bounds bounds)
        {
            Collider2D[] colliders = target.GetComponentsInChildren<Collider2D>();
            bounds = default;
            bool hasBounds = false;

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D collider = colliders[i];
                if (collider == null || !collider.enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = collider.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }

            return hasBounds;
        }

        private static bool BoundsOverlapsSemicircle(Bounds bounds, Vector2 origin, Vector2 direction, float sqrRange)
        {
            Vector3 origin3 = new Vector3(origin.x, origin.y, bounds.center.z);
            if (bounds.Contains(origin3))
            {
                return true;
            }

            if (IsPointInSemicircle(bounds.ClosestPoint(origin3), origin, direction, sqrRange))
            {
                return true;
            }

            Vector2 min = bounds.min;
            Vector2 max = bounds.max;
            return IsPointInSemicircle(new Vector2(min.x, min.y), origin, direction, sqrRange)
                || IsPointInSemicircle(new Vector2(min.x, max.y), origin, direction, sqrRange)
                || IsPointInSemicircle(new Vector2(max.x, min.y), origin, direction, sqrRange)
                || IsPointInSemicircle(new Vector2(max.x, max.y), origin, direction, sqrRange);
        }

        private static bool IsPointInSemicircle(Vector2 point, Vector2 origin, Vector2 direction, float sqrRange)
        {
            Vector2 toPoint = point - origin;
            return toPoint.sqrMagnitude <= sqrRange && Vector2.Dot(direction, toPoint) >= 0f;
        }

        private static bool ContainsHealth(Health[] values, int count, Health target)
        {
            for (int i = 0; i < count; i++)
            {
                if (values[i] == target)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
