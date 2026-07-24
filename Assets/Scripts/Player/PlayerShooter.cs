using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Week14.Audio;
using Week14.Enemy;
using Week14.Weapons;

namespace Week14.Combat
{
    public sealed class PlayerShooter
    {
        private readonly PlayerCombatController.PlayerCombatContext context;
        private readonly PlayerAimController aimController;

        private float chargeTime;
        private bool isCharging;
        private bool hasShownChargeLaser;
        private bool hasPlayedBaseballBatChargingSfx;
        private float nextBayonetAttackTime;
        private BaseballBatRangePreviewVfx baseballBatRangePreview;
        private BaseballBatDisplayVfx baseballBatDisplayVfx;
        private float baseballBatCurrentRotationDegrees;
        private Vector3 baseballBatCurrentScale = Vector3.one;
        private Color baseballBatCurrentColor = Color.white;
        private bool baseballBatWindingUp;
        private float baseballBatWindUpElapsed;
        private float baseballBatWindUpDuration;
        private bool baseballBatSwinging;
        private float baseballBatSwingElapsed;
        private float baseballBatSwingDuration;
        private float baseballBatSwingStartDegrees;
        private Vector3 baseballBatSwingStartScale;
        private Color baseballBatSwingColor = Color.white;
        private bool baseballBatHolding;
        private float baseballBatHoldElapsed;
        private bool baseballBatReturning;
        private float baseballBatReturnElapsed;
        private Coroutine baseballBatHitRoutine;
        private SoundManager.SfxPlaybackHandle baseballBatChargingSfxHandle;
        private SoundManager.SfxPlaybackHandle sniperChargeSfxHandle;

        internal PlayerShooter(
            PlayerCombatController.PlayerCombatContext context,
            PlayerAimController aimController)
        {
            this.context = context;
            this.aimController = aimController;
        }

        public int CurrentBullets => context.Bullets != null ? context.Bullets.CurrentBullets : 0;
        public bool IsCharging => isCharging;
        public bool HasShownChargeLaser => hasShownChargeLaser;

        internal void BeginAttack()
        {
            StopBaseballBatChargingSfx();
            StopSniperChargeSfx();
            chargeTime = 0f;
            isCharging = true;
            hasShownChargeLaser = false;
            hasPlayedBaseballBatChargingSfx = false;
            context.PlayerHpView?.FreezeNewestBullet(true);
            WeaponLoadoutManager.Instance?.CurrentWeapon?.BeginAttack(this);
        }

        internal void HoldAttack(float dt)
        {
            if (!isCharging) return;

            chargeTime += dt;
            WeaponLoadoutManager.Instance?.CurrentWeapon?.HoldAttack(this, chargeTime);
        }

        internal void ReleaseAttack()
        {
            if (!isCharging) return;
            context.PlayerHpView?.FreezeNewestBullet(false);
            StopBaseballBatChargingSfx();
            StopSniperChargeSfx();
            WeaponLoadoutManager.Instance?.CurrentWeapon?.ReleaseAttack(this, chargeTime);
            context.SniperChargeLaserEffect?.EndCharge();
            HideBaseballBatRangePreview();
            isCharging = false;
            chargeTime = 0f;
            hasShownChargeLaser = false;
            hasPlayedBaseballBatChargingSfx = false;
        }

        // 홀드가 일정 시간(첫 등장 딜레이) 이상 지속됐을 때 딱 한 번만 레이저 연출을 켭니다.
        public void ShowSniperChargeLaser(float laserLength, float spreadAngleDegrees)
        {
            hasShownChargeLaser = true;
            context.SniperChargeLaserEffect?.BeginCharge(laserLength, spreadAngleDegrees);
        }

        // 차지가 threshold(초)에 도달하는 진행도(0~1)를 레이저 연출에 매 홀드 프레임마다 밀어 넣습니다.
        public void UpdateSniperChargeProgress(float chargeTime, float thresholdSeconds)
        {
            float progress = thresholdSeconds > 0f ? Mathf.Clamp01(chargeTime / thresholdSeconds) : 1f;
            context.SniperChargeLaserEffect?.SetProgress(progress);
        }

        public void PlaySniperChargeSfx(string sfxId)
        {
            StopSniperChargeSfx();
            if (!string.IsNullOrWhiteSpace(sfxId))
            {
                sniperChargeSfxHandle = SoundManager.PlayTrackedSfx(sfxId);
            }
        }

        private void StopSniperChargeSfx()
        {
            SoundManager.StopSfx(sniperChargeSfxHandle);
            sniperChargeSfxHandle = null;
        }

        public void EndCharge()
        {
            StopBaseballBatChargingSfx();
            StopSniperChargeSfx();
            context.PlayerHpView?.FreezeNewestBullet(false);
            context.SniperChargeLaserEffect?.EndCharge();
            HideBaseballBatRangePreview();
            isCharging = false;
            chargeTime = 0f;
            hasShownChargeLaser = false;
            hasPlayedBaseballBatChargingSfx = false;
        }

        public void PlayBaseballBatChargingSfxOnce(string sfxId)
        {
            if (hasPlayedBaseballBatChargingSfx || string.IsNullOrEmpty(sfxId))
            {
                return;
            }

            hasPlayedBaseballBatChargingSfx = true;
            baseballBatChargingSfxHandle = SoundManager.PlayTrackedSfx(sfxId);
        }

        private void StopBaseballBatChargingSfx()
        {
            SoundManager.StopSfx(baseballBatChargingSfxHandle);
            baseballBatChargingSfxHandle = null;
        }

        public void PreviewBaseballBatRange(float range, Color color)
        {
            if (range <= 0f)
            {
                HideBaseballBatRangePreview();
                return;
            }

            if (baseballBatRangePreview == null)
            {
                GameObject previewObject = new GameObject("BaseballBatRangePreviewVfx");
                baseballBatRangePreview = previewObject.AddComponent<BaseballBatRangePreviewVfx>();
                baseballBatRangePreview.Initialize();
            }

            Vector2 origin = context.CombatCenterOrigin.position;
            Vector2 direction = aimController.GetAimDirection(context.CombatCenterOrigin);
            baseballBatRangePreview.UpdatePreview(origin, direction, range, color);
        }

        public void HideBaseballBatRangePreview()
        {
            if (baseballBatRangePreview == null)
            {
                return;
            }

            Object.Destroy(baseballBatRangePreview.gameObject);
            baseballBatRangePreview = null;
        }

        // 차징이 시작되는 순간(BeginAttack) 호출됩니다. 현재 각도(idle 0도)에서 스냅 목표 각도까지
        // WindUpSnapSeconds 동안 빠르게 회전하는 1단계 트윈을 시작합니다. 실제 진행은
        // UpdateBaseballBatCharging이 매 홀드 프레임마다 처리합니다.
        public void BeginBaseballBatWindUp(BaseballBatVfxSettings vfxSettings)
        {
            if (vfxSettings == null)
            {
                return;
            }

            baseballBatSwinging = false;
            baseballBatHolding = false;
            baseballBatReturning = false;
            baseballBatWindingUp = true;
            baseballBatWindUpElapsed = 0f;
            baseballBatWindUpDuration = Mathf.Max(0.01f, vfxSettings.WindUpSnapSeconds);
        }

        // 차징 중(HoldAttack)에 매 프레임 호출됩니다. 1단계(스냅)가 진행 중이면 0도에서 스냅 목표
        // 각도까지 빠르게 회전시키고, 끝난 뒤에는 2단계로 넘어가 스냅 목표 각도에서 -N도까지
        // 차징 진행도(0~1)에 비례해서 마저 회전합니다.
        public void UpdateBaseballBatCharging(float charge01, BaseballBatVfxSettings vfxSettings, Sprite sprite)
        {
            if (vfxSettings == null)
            {
                return;
            }

            float clampedCharge01 = Mathf.Clamp01(charge01);
            float fullTargetDegrees = -vfxSettings.DisplayWindUpDegrees;
            float snapTargetDegrees = Mathf.Lerp(0f, fullTargetDegrees, vfxSettings.WindUpSnapRatio);

            if (baseballBatWindingUp)
            {
                baseballBatWindUpElapsed += Time.deltaTime;
                float snapT = Mathf.Clamp01(baseballBatWindUpElapsed / baseballBatWindUpDuration);
                baseballBatCurrentRotationDegrees = Mathf.Lerp(0f, snapTargetDegrees, snapT);
                if (snapT >= 1f)
                {
                    baseballBatWindingUp = false;
                }
            }
            else
            {
                baseballBatCurrentRotationDegrees = Mathf.Lerp(snapTargetDegrees, fullTargetDegrees, clampedCharge01);
                if (clampedCharge01 >= 1f && vfxSettings.FullChargeShakeDegrees > 0f)
                {
                    float noise = Mathf.PerlinNoise(Time.time * vfxSettings.FullChargeShakeSpeed, 0.37f) - 0.5f;
                    baseballBatCurrentRotationDegrees += noise * 2f * vfxSettings.FullChargeShakeDegrees;
                }
            }

            Vector3 scale = Vector3.Lerp(vfxSettings.DisplayScale, vfxSettings.MaxChargeScale, clampedCharge01);
            Color color = vfxSettings.ResolveDisplayColor(charge01);
            ApplyBaseballBatDisplayPose(vfxSettings, sprite, baseballBatCurrentRotationDegrees, scale, color);
        }

        // 공격이 실제로 나가는 순간(ReleaseAttack) 호출됩니다. 현재 각도(보통 -N)에서 +N까지
        // durationSeconds 동안 빠르게 스윙하고, 동시에 크기를 기본 Display Scale로 되돌립니다.
        // 실제 진행은 매 프레임 UpdateBaseballBatDisplay에서 처리되며, 스윙이 끝나면 곧바로
        // 원래 각도로 돌아가지 않고 SwingHoldSeconds 동안 유지한 뒤 SwingReturnSeconds에 걸쳐 서서히 복귀합니다.
        public void StartBaseballBatSwingThrough(BaseballBatVfxSettings vfxSettings, float durationSeconds)
        {
            if (vfxSettings == null)
            {
                return;
            }

            baseballBatWindingUp = false;
            baseballBatSwinging = true;
            baseballBatHolding = false;
            baseballBatReturning = false;
            baseballBatSwingElapsed = 0f;
            baseballBatSwingDuration = Mathf.Max(0.01f, durationSeconds);
            baseballBatSwingStartDegrees = baseballBatCurrentRotationDegrees;
            baseballBatSwingStartScale = baseballBatCurrentScale;
            baseballBatSwingColor = baseballBatCurrentColor;
        }

        // 매 프레임(차징 여부와 무관하게) 호출됩니다. 차징 중일 때는 UpdateBaseballBatCharging이 이미
        // 포즈를 갱신하므로 여기서는 건드리지 않고, 스윙 스루 진행 또는 조준 방향을 향하는 대기 포즈만 처리합니다.
        public void UpdateBaseballBatDisplay()
        {
            BaseballBatWeaponSO bat = WeaponLoadoutManager.Instance?.CurrentWeapon as BaseballBatWeaponSO;
            if (bat == null)
            {
                HideBaseballBatDisplay();
                return;
            }

            if (isCharging)
            {
                return;
            }

            BaseballBatVfxSettings vfxSettings = bat.VfxSettings;
            if (baseballBatSwinging)
            {
                baseballBatSwingElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(baseballBatSwingElapsed / baseballBatSwingDuration);
                float rotation = Mathf.Lerp(baseballBatSwingStartDegrees, vfxSettings.DisplayWindUpDegrees, t);
                Vector3 scale = Vector3.Lerp(baseballBatSwingStartScale, vfxSettings.DisplayScale, t);
                ApplyBaseballBatDisplayPose(vfxSettings, bat.InGameSprite, rotation, scale, baseballBatSwingColor);
                if (t >= 1f)
                {
                    baseballBatSwinging = false;
                    baseballBatHolding = true;
                    baseballBatHoldElapsed = 0f;
                }

                return;
            }

            if (baseballBatHolding)
            {
                baseballBatHoldElapsed += Time.deltaTime;
                ApplyBaseballBatDisplayPose(vfxSettings, bat.InGameSprite, vfxSettings.DisplayWindUpDegrees, vfxSettings.DisplayScale, baseballBatSwingColor);
                if (baseballBatHoldElapsed >= vfxSettings.SwingHoldSeconds)
                {
                    baseballBatHolding = false;
                    baseballBatReturning = true;
                    baseballBatReturnElapsed = 0f;
                }

                return;
            }

            if (baseballBatReturning)
            {
                baseballBatReturnElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(baseballBatReturnElapsed / vfxSettings.SwingReturnSeconds);
                float rotation = Mathf.Lerp(vfxSettings.DisplayWindUpDegrees, 0f, t);
                Color color = Color.Lerp(baseballBatSwingColor, vfxSettings.ResolveDisplayColor(0f), t);
                ApplyBaseballBatDisplayPose(vfxSettings, bat.InGameSprite, rotation, vfxSettings.DisplayScale, color);
                if (t >= 1f)
                {
                    baseballBatReturning = false;
                }

                return;
            }

            ApplyBaseballBatDisplayPose(vfxSettings, bat.InGameSprite, 0f, vfxSettings.DisplayScale, vfxSettings.ResolveDisplayColor(0f));
        }

        private void ApplyBaseballBatDisplayPose(BaseballBatVfxSettings vfxSettings, Sprite sprite, float rotationDegrees, Vector3 scale, Color color)
        {
            if (vfxSettings == null || context.BaseballBatVfxAnchor == null)
            {
                return;
            }

            if (baseballBatDisplayVfx == null)
            {
                GameObject displayObject = new GameObject("BaseballBatDisplayVfx");
                displayObject.transform.SetParent(context.BaseballBatVfxAnchor, false);
                baseballBatDisplayVfx = displayObject.AddComponent<BaseballBatDisplayVfx>();
                baseballBatDisplayVfx.Initialize(sprite, vfxSettings.DisplaySortingOrder, scale, color);
            }
            else
            {
                baseballBatDisplayVfx.SetSprite(sprite);
                baseballBatDisplayVfx.SetScale(scale);
                baseballBatDisplayVfx.SetColor(color);
            }

            baseballBatCurrentRotationDegrees = rotationDegrees;
            baseballBatCurrentScale = scale;
            baseballBatCurrentColor = color;

            Vector3 origin = context.BaseballBatVfxAnchor.position;
            Vector2 direction = aimController.GetAimDirection(context.BaseballBatVfxAnchor);
            baseballBatDisplayVfx.SetPose(
                origin,
                direction,
                vfxSettings.DisplayOffsetDistance,
                rotationDegrees,
                vfxSettings.DisplaySpriteRotationOffsetDegrees);
        }

        private void HideBaseballBatDisplay()
        {
            baseballBatWindingUp = false;
            baseballBatSwinging = false;
            baseballBatHolding = false;
            baseballBatReturning = false;
            baseballBatCurrentRotationDegrees = 0f;
            if (baseballBatDisplayVfx == null)
            {
                return;
            }

            Object.Destroy(baseballBatDisplayVfx.gameObject);
            baseballBatDisplayVfx = null;
        }

        public bool TrySpendOneBullet()
        {
            BulletGauge bullets = context.Bullets;
            PlayerCombatConfig config = context.Config;
            if (bullets == null || config == null) return false;
            return bullets.TrySpend(config.LeftAttackBulletCost, BulletChangeSource.Attack);
        }

        public bool TrySpendAllBullets()
        {
            BulletGauge bullets = context.Bullets;
            if (bullets == null || bullets.CurrentBullets <= 0) return false;
            return bullets.TrySpend(bullets.CurrentBullets, BulletChangeSource.Attack);
        }

        // 관통(레일건) 전용 발사. 물리 투사체를 날리는 게 아니라 실제 사거리만큼 즉시 CircleCastAll로
        // 스윕해서 일직선상의 모든 대상에게 damage를 그대로(분할 없이) 적용합니다. 프리팹 이펙트는
        // 같은 사거리까지 자동으로 늘어나며, 프리팹이 없을 때만 ShotLine을 대체 연출로 사용합니다.
        public void FireLaser(
            int damage,
            int spentAmmo,
            float speed,
            float lifetime,
            float beamVisualSeconds,
            float beamWidth,
            Color beamColor,
            RailgunVfxSettings vfxSettings,
            string fireSfxId)
        {
            PlayerCombatConfig config = context.Config;
            if (config == null) return;

            Transform fireOrigin = GetLeftFireOrigin();
            Vector2 direction = aimController.AimGunAndGetDirection(
                context.LeftGunOrigin,
                aimController.GetAimDirection(context.LeftGunOrigin));
            aimController.LockLeftGunAim(direction);

            int finalDamage = ApplyNextAttackDamageMultiplier(damage);
            float beamLength = Mathf.Max(0.1f, speed * lifetime);
            Vector2 origin = fireOrigin.position;

            DamageEnemiesAlongLine(origin, direction, beamLength, config.ProjectileRadius, finalDamage);
            context.Owner.NotifyPlayerAttackPerformed(finalDamage, ammoSpent: spentAmmo);

            Vector3 beamEnd = fireOrigin.position + (Vector3)(direction * beamLength);
            GameObject beamPrefab = vfxSettings?.ResolveBeamPrefab(spentAmmo);
            if (beamPrefab != null)
            {
                ProjectileVfx.PlayAnchoredBeamPrefab(
                    beamPrefab,
                    fireOrigin,
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
                ProjectileVfx.PlayShotLine(fireOrigin.position, beamEnd, beamColor, beamVisualSeconds, beamWidth);
            }

            GameObject muzzleFlashPrefab = vfxSettings?.ResolveMuzzleFlashPrefab(spentAmmo);
            float muzzleFlashScale = 1f;
            if (muzzleFlashPrefab == null)
            {
                muzzleFlashPrefab = config.PlayerMuzzleFlashVfxPrefab;
                muzzleFlashScale = 1.2f;
            }

            ProjectileVfx.PlayPrefab(
                muzzleFlashPrefab,
                fireOrigin.position,
                direction,
                fireOrigin,
                muzzleFlashScale);
            context.Visual?.PlayShot();
            if (!string.IsNullOrEmpty(fireSfxId))
            {
                SoundManager.PlaySfx(fireSfxId);
            }
            SoundManager.PlaySfx("BulletLoss");
        }

        private void DamageEnemiesAlongLine(Vector2 origin, Vector2 direction, float length, float beamRadius, int damage)
        {
            if (damage <= 0 || length <= 0f)
            {
                return;
            }

            RaycastHit2D[] hits = Physics2D.CircleCastAll(origin, Mathf.Max(0.01f, beamRadius), direction, length);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            HashSet<Health> hitTargets = new HashSet<Health>();
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D collider = hits[i].collider;
                if (collider == null)
                {
                    continue;
                }

                Health targetHealth = collider.GetComponentInParent<Health>();
                if (!IsValidAreaDamageTarget(targetHealth) || !hitTargets.Add(targetHealth))
                {
                    continue;
                }

                PlayerProjectile.TryApplyDamageToHealth(
                    targetHealth,
                    damage,
                    false,
                    targetHealth.transform.position,
                    direction,
                    Color.white,
                    context.Config?.EnemyHitVfxPrefab);
            }
        }

        // 총검 쿨타임을 소모 시도합니다. 이 상태를 PlayerShooter(씬마다 새로 만들어지는 플레이어 런타임 객체)에
        // 두는 이유: BayonetWeaponSO에 두면 씬 재로드/Time.time 리셋과 무관하게 게임 전체에서 공유되는
        // ScriptableObject 인스턴스에 타임스탬프가 남아서, 에디터에서 Play를 반복하면(도메인 리로드 꺼짐 등)
        // 이전 세션의 큰 Time.time 값이 그대로 남아 새 세션에서 한동안 공격이 전혀 안 나가는 버그가 생깁니다.
        public bool TryConsumeBayonetCooldown(float cooldownSeconds)
        {
            if (Time.time < nextBayonetAttackTime)
            {
                return false;
            }

            nextBayonetAttackTime = Time.time + Mathf.Max(0f, cooldownSeconds);
            return true;
        }

        public bool IsBayonetCooldownReady()
        {
            return Time.time >= nextBayonetAttackTime;
        }

        public void ResetChargeTime()
        {
            StopBaseballBatChargingSfx();
            chargeTime = 0f;
            hasPlayedBaseballBatChargingSfx = false;
        }

        // 근접 반원 공격(총검): 조준 방향(락온 중이면 GetAimDirection이 알아서 보스 방향을 반환) 기준
        // 앞쪽 반원(반지름 range) 안의 적탄을 즉시 제거하고, 같은 범위 안의 보스/미니언에게 damage를 적용합니다.
        // 탄환은 전혀 소모하지 않습니다.
        public void SwingBayonet(int damage, float range, Color rangeFlashColor, float rangeFlashSeconds, string slashSfxId)
        {
            if (range <= 0f)
            {
                return;
            }

            Vector2 origin = context.CombatCenterOrigin.position;
            Vector2 direction = aimController.GetAimDirection(context.CombatCenterOrigin);

            ClearProjectilesInSemicircle(origin, direction, range);
            DamageEnemiesInSemicircle(origin, direction, range, damage);
            context.Owner.NotifyPlayerAttackPerformed(damage, range);
            ProjectileVfx.PlaySemicircleFlash(origin, direction, range, rangeFlashColor, rangeFlashSeconds);

            if (!string.IsNullOrEmpty(slashSfxId))
            {
                SoundManager.PlaySfx(slashSfxId);
            }
        }

        public void SwingBaseballBat(
            int reflectedDamage,
            float range,
            float reflectedSpeed,
            BaseballBatVfxSettings vfxSettings,
            float charge01,
            string reflectionSuccessSfxId,
            float reflectionSfxBasePitch,
            float reflectionSfxPitchStep,
            float reflectionSfxMaxPitch)
        {
            if (range <= 0f)
            {
                return;
            }

            Vector2 direction = aimController.GetAimDirection(context.CombatCenterOrigin);
            if (vfxSettings != null)
            {
                ProjectileVfx.PlayAnchoredPrefab(
                    vfxSettings.ResolveSwingVfxPrefab(charge01),
                    context.BaseballBatVfxAnchor,
                    direction,
                    vfxSettings.GetRightFacingLocalOffset(charge01),
                    vfxSettings.RotationOffsetDegrees,
                    vfxSettings.GetLocalScale(range),
                    vfxSettings.PlaybackSpeed,
                    vfxSettings.SortingOrder);

                Vector2 indicatorOrigin = context.CombatCenterOrigin.position;
                ProjectileVfx.PlaySemicircleFlash(
                    indicatorOrigin,
                    direction,
                    range,
                    vfxSettings.RangeIndicatorColor,
                    vfxSettings.RangeIndicatorSeconds);
            }

            float hitDelaySeconds = vfxSettings != null ? vfxSettings.AttackHitDelaySeconds : 0f;
            float activeSeconds = vfxSettings != null ? vfxSettings.AttackActiveSeconds : 0.01f;
            if (baseballBatHitRoutine != null)
            {
                context.CoroutineHost.StopCoroutine(baseballBatHitRoutine);
            }

            baseballBatHitRoutine = context.CoroutineHost.StartCoroutine(
                ResolveBaseballBatHitsDuringWindow(
                    hitDelaySeconds,
                    activeSeconds,
                    direction,
                    range,
                    reflectedDamage,
                    reflectedSpeed,
                    reflectionSuccessSfxId,
                    reflectionSfxBasePitch,
                    reflectionSfxPitchStep,
                    reflectionSfxMaxPitch));
        }

        private IEnumerator ResolveBaseballBatHitsDuringWindow(
            float delaySeconds,
            float activeSeconds,
            Vector2 direction,
            float range,
            int reflectedDamage,
            float reflectedSpeed,
            string reflectionSuccessSfxId,
            float reflectionSfxBasePitch,
            float reflectionSfxPitchStep,
            float reflectionSfxMaxPitch)
        {
            if (delaySeconds > 0f)
            {
                yield return new WaitForSeconds(delaySeconds);
            }

            context.Owner.NotifyPlayerAttackPerformed(reflectedDamage, range, reflectedSpeed);

            float activeEndsAt = Time.time + Mathf.Max(0.01f, activeSeconds);
            float safeBasePitch = Mathf.Clamp(reflectionSfxBasePitch, 0.1f, 3f);
            float safePitchStep = Mathf.Max(0f, reflectionSfxPitchStep);
            float safeMaxPitch = Mathf.Clamp(reflectionSfxMaxPitch, safeBasePitch, 3f);
            int reflectedProjectileCount = 0;
            do
            {
                int newlyReflectedCount = ResolveBaseballBatHit(
                    direction,
                    range,
                    reflectedDamage,
                    reflectedSpeed);
                for (int i = 0; i < newlyReflectedCount; i++)
                {
                    float pitch = Mathf.Min(
                        safeMaxPitch,
                        safeBasePitch + safePitchStep * reflectedProjectileCount);
                    if (!string.IsNullOrEmpty(reflectionSuccessSfxId))
                    {
                        SoundManager.PlaySfx(reflectionSuccessSfxId, pitch);
                    }

                    reflectedProjectileCount++;
                }

                yield return null;
            }
            while (Time.time < activeEndsAt);

            baseballBatHitRoutine = null;
        }

        private int ResolveBaseballBatHit(
            Vector2 direction,
            float range,
            int reflectedDamage,
            float reflectedSpeed)
        {
            Vector2 origin = context.CombatCenterOrigin.position;
            DestroyDeployedConductorTurretsInSemicircle(
                origin,
                direction,
                range);
            int reflectedCount = ReflectProjectilesInSemicircle(
                origin,
                direction,
                range,
                reflectedDamage,
                reflectedSpeed);
            InterceptNonReflectableProjectilesInSemicircle(origin, direction, range);
            return reflectedCount;
        }

        // 반사는 안 되지만 요격은 되는 투사체(패링 미끼 등)를 처리합니다. 반사 가능한 투사체를 먼저 처리했으므로
        // 여기 남아 있는 CanBeIntercepted 대상은 반사되지 않은 투사체뿐입니다. 마우스 패링과 같은 통로를 사용해
        // 패링 성공 이벤트, 챌린지 집계와 보스 패턴 억제가 동일하게 동작하도록 합니다.
        private void InterceptNonReflectableProjectilesInSemicircle(Vector2 origin, Vector2 direction, float range)
        {
            IReadOnlyList<EnemyProjectile> activeProjectiles = EnemyProjectile.ActiveProjectiles;

            for (int i = activeProjectiles.Count - 1; i >= 0; i--)
            {
                EnemyProjectile projectile = activeProjectiles[i];
                if (projectile == null || !projectile.CanBeIntercepted)
                {
                    continue;
                }

                if (!OverlapsSemicircle(projectile, origin, direction, range))
                {
                    continue;
                }

                PlayerDashVfx.PlayProjectileAbsorb(
                    context.CoroutineHost,
                    projectile,
                    origin,
                    0.16f,
                    new Color(0.9f, 0.9f, 1f, 0.85f));
                context.Owner.TryParryProjectileForMelee(projectile);
            }
        }

        private static bool DestroyDeployedConductorTurretsInSemicircle(
            Vector2 origin,
            Vector2 direction,
            float range)
        {
            IReadOnlyList<EnemyProjectile> activeProjectiles = EnemyProjectile.ActiveProjectiles;
            bool destroyedAnyTurret = false;

            // 파괴 시 활성 투사체 목록에서 빠지므로 인덱스가 밀리지 않도록 뒤에서부터 순회합니다.
            for (int i = activeProjectiles.Count - 1; i >= 0; i--)
            {
                if (activeProjectiles[i] is not ConductorTurretProjectile turret
                    || !turret.IsPlayerTargetable
                    || !OverlapsSemicircle(turret, origin, direction, range))
                {
                    continue;
                }

                if (turret.TryDestroyByBaseballBat(turret.transform.position, direction))
                {
                    destroyedAnyTurret = true;
                }
            }

            return destroyedAnyTurret;
        }

        private void ClearProjectilesInSemicircle(Vector2 origin, Vector2 direction, float range)
        {
            IReadOnlyList<EnemyProjectile> activeProjectiles = EnemyProjectile.ActiveProjectiles;

            // 뒤에서부터 순회합니다: TryDestroyByInterceptShot이 이 리스트에서 즉시 self-remove하는데,
            // 앞에서부터 돌면 삭제된 자리로 뒤 원소들이 한 칸씩 당겨지면서 다음 인덱스를 건너뛰어
            // 범위 안에 있는데도 안 지워지는 총알이 생깁니다. 뒤에서부터 지우면 이미 지나온 인덱스만
            // 밀리므로 안전합니다.
            for (int i = activeProjectiles.Count - 1; i >= 0; i--)
            {
                EnemyProjectile projectile = activeProjectiles[i];
                if (projectile == null || !projectile.CanBeIntercepted)
                {
                    continue;
                }

                if (!OverlapsSemicircle(projectile, origin, direction, range))
                {
                    continue;
                }

                // 흡수 VFX는 스프라이트를 복제하는 방식이라, 파괴(비활성화)되기 전에 먼저 재생해야 합니다.
                PlayerDashVfx.PlayProjectileAbsorb(
                    context.CoroutineHost,
                    projectile,
                    origin,
                    0.16f,
                    new Color(0.9f, 0.9f, 1f, 0.85f));
                projectile.TryDestroyByInterceptShot(out _);
            }
        }

        private int ReflectProjectilesInSemicircle(
            Vector2 origin,
            Vector2 direction,
            float range,
            int reflectedDamage,
            float reflectedSpeed)
        {
            IReadOnlyList<EnemyProjectile> activeProjectiles = EnemyProjectile.ActiveProjectiles;
            int reflectedCount = 0;

            for (int i = activeProjectiles.Count - 1; i >= 0; i--)
            {
                EnemyProjectile projectile = activeProjectiles[i];
                if (projectile == null || !projectile.CanBeReflected)
                {
                    continue;
                }

                if (!OverlapsSemicircle(projectile, origin, direction, range))
                {
                    continue;
                }

                if (projectile.TryReflectTowardOwnerBoss(reflectedSpeed, reflectedDamage, out _))
                {
                    reflectedCount++;
                    PlayerDashVfx.PlayProjectileAbsorb(
                        context.CoroutineHost,
                        projectile,
                        origin,
                        0.12f,
                        new Color(1f, 0.65f, 0.25f, 0.85f));
                }
            }

            return reflectedCount;
        }

        private void DamageEnemiesInSemicircle(Vector2 origin, Vector2 direction, float range, int damage)
        {
            if (damage <= 0)
            {
                return;
            }

            Health[] allHealth = Object.FindObjectsByType<Health>(FindObjectsSortMode.None);

            for (int i = 0; i < allHealth.Length; i++)
            {
                Health targetHealth = allHealth[i];
                if (!IsValidAreaDamageTarget(targetHealth))
                {
                    continue;
                }

                if (!OverlapsSemicircle(targetHealth, origin, direction, range))
                {
                    continue;
                }

                PlayerProjectile.TryApplyDamageToHealth(
                    targetHealth,
                    damage,
                    false,
                    targetHealth.transform.position,
                    direction,
                    Color.white,
                    context.Config?.EnemyHitVfxPrefab);
            }
        }

        // target(투사체/보스/미니언)의 콜라이더 바운드가 반원(원점 기준 반지름 range, 조준 방향 앞쪽 180도)에
        // 조금이라도 겹치면 true입니다. 예전엔 transform.position 한 점만 봐서 피벗이 반원 밖이면 몸체 대부분이
        // 걸쳐 있어도 판정이 안 되는 문제가 있었습니다. 콜라이더가 없으면 점 판정으로 폴백합니다.
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

        // 바운드 안에 원점이 있거나, 바운드에서 원점과 가장 가까운 점이 반원 안이거나,
        // 네 모서리 중 하나라도 반원 안이면 겹치는 것으로 판정합니다(관대한 근사치).
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

        // 총검(반원)/레일건(빔) 둘 다 즉시 판정되는 범위 공격이라 유효 타겟 조건을 공유합니다.
        private bool IsValidAreaDamageTarget(Health targetHealth)
        {
            return targetHealth != null
                && targetHealth != context.Health
                && !targetHealth.IsDead
                && (targetHealth.GetComponent<BossAI>() != null
                    || targetHealth.GetComponentInParent<BossAI>() != null
                    || targetHealth.GetComponent<Minion>() != null
                    || targetHealth.GetComponentInParent<Minion>() != null);
        }

        public void FireSingle(int damage)
        {
            PlayerCombatConfig config = context.Config;
            if (config == null) return;

            PlayerProjectile projectilePrefab = ResolveProjectilePrefab(config);
            if (projectilePrefab == null) return;

            damage = ApplyNextAttackDamageMultiplier(damage);

            Transform fireOrigin = GetLeftFireOrigin();
            Vector2 direction = aimController.AimGunAndGetDirection(
                context.LeftGunOrigin,
                aimController.GetAimDirection(context.LeftGunOrigin));
            aimController.LockLeftGunAim(direction);

            PlayerProjectile projectile = PlayerProjectile.Spawn(
                projectilePrefab,
                fireOrigin.position,
                direction,
                context.Owner,
                config.ProjectileSpeed,
                config.ProjectileLifetime,
                config.ProjectileRadius,
                damage,
                config.AttackEffectColor,
                true);

            if (projectile == null) return;

            ProjectileVfx.PlayPrefab(config.PlayerMuzzleFlashVfxPrefab, fireOrigin.position, direction, fireOrigin, 0.9f);
            context.Visual?.PlayShot();
            SoundManager.PlaySfx("SniperFire");
            SoundManager.PlaySfx("BulletLoss");
            context.Owner.NotifyPlayerAttackPerformed(damage);
        }

        // damage는 펠릿 하나하나가 각각 그대로 받는 값입니다(무기 기본 데미지 그대로, 펠릿 수로 나누지 않음).
        // spreadAngle은 전체 퍼짐 각도(콘 너비)이고, 펠릿들은 그 안에 고르게 분포합니다.
        public void FireSpread(int damage, int pelletCount, float spreadAngle)
        {
            if (pelletCount <= 0) return;

            PlayerCombatConfig config = context.Config;
            if (config == null) return;

            PlayerProjectile projectilePrefab = ResolveProjectilePrefab(config);
            if (projectilePrefab == null) return;

            Transform fireOrigin = GetLeftFireOrigin();
            Vector2 baseDirection = aimController.AimGunAndGetDirection(
                context.LeftGunOrigin,
                aimController.GetAimDirection(context.LeftGunOrigin));
            aimController.LockLeftGunAim(baseDirection);

            int pelletDamage = ApplyNextAttackDamageMultiplier(damage);

            float startAngle = -spreadAngle * 0.5f;
            float angleStep = pelletCount > 1 ? spreadAngle / (pelletCount - 1) : 0f;

            for (int i = 0; i < pelletCount; i++)
            {
                float angle = startAngle + angleStep * i;
                Vector2 pelletDir = Quaternion.Euler(0f, 0f, angle) * baseDirection;
                PlayerProjectile.Spawn(
                    projectilePrefab,
                    fireOrigin.position,
                    pelletDir,
                    context.Owner,
                    config.ProjectileSpeed,
                    config.ProjectileLifetime,
                    config.ProjectileRadius,
                    pelletDamage,
                    config.AttackEffectColor,
                    true);
            }

            ProjectileVfx.PlayPrefab(config.PlayerMuzzleFlashVfxPrefab, fireOrigin.position, baseDirection, fireOrigin, 0.9f);
            context.Visual?.PlayShot();
            SoundManager.PlaySfx(pelletCount >= 2 ? "ShotgunFire" : "PlayerShot");
            SoundManager.PlaySfx("BulletLoss");
            context.Owner.NotifyPlayerAttackPerformed(pelletDamage);
        }

        private PlayerProjectile ResolveProjectilePrefab(PlayerCombatConfig config)
        {
            return config != null ? config.ProjectilePrefab : null;
        }

        public bool TryShootEnemy()
        {
            PlayerCombatConfig config = context.Config;
            BulletGauge bullets = context.Bullets;
            if (config == null)
            {
                return false;
            }

            PlayerProjectile projectilePrefab = ResolveProjectilePrefab(config);

            if (projectilePrefab == null)
            {
                Debug.LogWarning($"{nameof(PlayerCombatConfig)} requires {nameof(PlayerCombatConfig.ProjectilePrefab)}.", context.Owner);
                return false;
            }

            int firedBulletNumber = bullets != null ? bullets.CurrentBullets : 0;
            int dynamicDamage = CalculateAttackBulletDamage();

            if (bullets == null || !bullets.TrySpend(config.LeftAttackBulletCost, BulletChangeSource.Attack))
            {
                return false;
            }

            dynamicDamage = ApplyNextAttackDamageMultiplier(dynamicDamage);

            Transform fireOrigin = GetLeftFireOrigin();
            Vector2 direction = aimController.AimGunAndGetDirection(
                context.LeftGunOrigin,
                aimController.GetAimDirection(context.LeftGunOrigin));
            aimController.LockLeftGunAim(direction);

            PlayerProjectile projectile = PlayerProjectile.Spawn(
                projectilePrefab,
                fireOrigin.position,
                direction,
                context.Owner,
                config.ProjectileSpeed,
                config.ProjectileLifetime,
                config.ProjectileRadius,
                dynamicDamage,
                config.AttackEffectColor,
                true);

            if (projectile == null)
            {
                bullets.Restore(config.LeftAttackBulletCost, BulletChangeSource.Attack);
                return false;
            }

            ProjectileVfx.PlayPrefab(config.PlayerMuzzleFlashVfxPrefab, fireOrigin.position, direction, fireOrigin, 0.9f);
            context.Visual?.PlayShot();
            SoundManager.PlaySfx(firedBulletNumber >= 2 ? "PlayerShot" : "PlayerPowerShot");
            SoundManager.PlaySfx("BulletLoss");
            context.Owner.NotifyPlayerAttackPerformed(dynamicDamage);
            return true;
        }

        internal bool TryFireSkillProjectile(int damage, float sizeMultiplier, Color color)
        {
            PlayerCombatConfig config = context.Config;
            if (config == null)
            {
                return false;
            }

            if (config.ProjectilePrefab == null)
            {
                Debug.LogWarning($"{nameof(PlayerCombatConfig)} requires {nameof(PlayerCombatConfig.ProjectilePrefab)}.", context.Owner);
                return false;
            }

            Transform fireOrigin = GetLeftFireOrigin();
            Vector2 direction = aimController.AimGunAndGetDirection(
                context.LeftGunOrigin,
                aimController.GetAimDirection(context.LeftGunOrigin));
            aimController.LockLeftGunAim(direction);

            float radius = config.ProjectileRadius * Mathf.Max(0.1f, sizeMultiplier);

            PlayerProjectile projectile = PlayerProjectile.Spawn(
                config.ProjectilePrefab,
                fireOrigin.position,
                direction,
                context.Owner,
                config.ProjectileSpeed,
                config.ProjectileLifetime,
                radius,
                damage,
                color,
                true,
                isSkillShot: true);

            if (projectile == null)
            {
                return false;
            }

            ProjectileVfx.PlayPrefab(config.PlayerMuzzleFlashVfxPrefab, fireOrigin.position, direction, fireOrigin, 0.9f);
            context.Visual?.PlayShot();
            SoundManager.PlaySfx("PlayerPowerShot");
            return true;
        }

        private int ApplyNextAttackDamageMultiplier(int damage)
        {
            float multiplier = context.Owner.ConsumeNextAttackDamageMultiplier();
            return multiplier > 1f ? Mathf.Max(1, Mathf.RoundToInt(damage * multiplier)) : damage;
        }

        public int CalculateAttackBulletDamage()
        {
            BulletGauge bullets = context.Bullets;
            BaseWeaponSO weapon = WeaponLoadoutManager.Instance != null ? WeaponLoadoutManager.Instance.CurrentWeapon : null;
            if (weapon == null)
            {
                return 1;
            }

            int remainingAmmo = bullets != null ? bullets.CurrentBullets : 1;
            return weapon.GetDamageForAmmo(remainingAmmo);
        }

        private Transform GetLeftFireOrigin()
        {
            return context.LeftGunFireOrigin != null
                ? context.LeftGunFireOrigin
                : (context.LeftGunOrigin != null ? context.LeftGunOrigin : context.PlayerTransform);
        }
    }

    internal static class PlayerBulletAudio
    {
        private const float MinPitch = 1f;
        private const int PitchStepReferenceBulletCount = 5;

        internal static float GetBulletCountPitch(int currentBullets, int maxBullets, float maxPitch = 2f)
        {
            float pitchStepPerBullet = (maxPitch - MinPitch) / (PitchStepReferenceBulletCount - 1);
            int bulletDeficit = maxBullets - currentBullets;
            float pitch = maxPitch - pitchStepPerBullet * bulletDeficit;
            return Mathf.Clamp(pitch, MinPitch, maxPitch);
        }

        internal static void PlayBulletRestoreSfx(int currentBullets, int maxBullets)
        {
            SoundManager.PlaySfx("BulletRestore2", GetBulletCountPitch(currentBullets, maxBullets));
        }
    }
}
