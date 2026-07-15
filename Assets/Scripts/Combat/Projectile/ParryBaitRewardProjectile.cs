using System;
using System.Collections.Generic;
using UnityEngine;
using Week14.Enemy;

namespace Week14.Combat
{
    [AddComponentMenu("Week14/Combat/Parry Bait Reward Projectile")]
    public sealed class ParryBaitRewardProjectile : EnemyProjectile
    {
        [Header("보상 투사체")]
        [SerializeField] private BossProjectileSettings rewardProjectile = new();
        [SerializeField, Min(1)] private int rewardBulletCount = 8;
        [SerializeField, Min(0.01f)] private float rewardCircleRadius = 1.5f;
        [SerializeField, Min(0.01f)] private float rewardLifetime = 2f;

        [Header("남은 시간 게이지")]
        [SerializeField] private SpriteRenderer chargeGaugeRenderer;

        [Header("Hacker 파괴 연출")]
        [SerializeField, Min(0.01f)] private float hackerShatterSeconds = 0.12f;
        [SerializeField, Min(0.01f)] private float hackerShardDistance = 0.35f;
        [SerializeField, Min(0f)] private float hackerReassembledHoldSeconds = 0.08f;

        private static readonly int FillAmountId = Shader.PropertyToID("_FillAmount");

        private int? rewardCountOverride;
        private float? rewardRadiusOverride;
        private float? rewardLifetimeOverride;
        private MaterialPropertyBlock chargeGaugePropertyBlock;
        private readonly List<ShardState> hackerShards = new();
        private readonly List<RendererState> hackerSourceRenderers = new();
        private bool hackerResilientMode;
        private bool hackerWasParried;
        private bool hackerReassemblyStarted;
        private bool hackerReassemblyCompleted;
        private float hackerParryDuration;
        private float hackerParryEndsAt;
        private float hackerAttackAt;
        private float hackerReassembleStartsAt;
        private float hackerReassembleSeconds;
        private float hackerDestroyAt;
        private float hackerParriedAt;
        private Transform hackerFollowTarget;
        private Vector3 hackerFollowWorldOffset;

        internal event Action HackerParried;

        protected override void OnProjectileInitialized()
        {
            CleanupHackerShards(true);
            hackerResilientMode = false;
            hackerWasParried = false;
            hackerReassemblyStarted = false;
            hackerReassemblyCompleted = false;
            hackerFollowTarget = null;
            rewardCountOverride = null;
            rewardRadiusOverride = null;
            rewardLifetimeOverride = null;
            ConfigureParryLockOnIndicatorColor(null);
            ConfigurePlayerCollisionIgnored(true);
            SetChargeGaugeVisible(true);
        }

        internal void ConfigureBaitDuration(float lifetimeSeconds)
        {
            OverrideProjectileLifetime(lifetimeSeconds);
        }

        internal void ConfigureHackerResilientMode(
            float parrySeconds,
            float attackDelaySeconds,
            float reassembleSeconds,
            Transform followTarget,
            Vector3 followWorldOffset)
        {
            hackerResilientMode = true;
            hackerWasParried = false;
            hackerReassemblyStarted = false;
            hackerReassemblyCompleted = false;
            hackerParryDuration = Mathf.Max(0.01f, parrySeconds);
            hackerParryEndsAt = Time.time + hackerParryDuration;
            hackerAttackAt = Time.time + Mathf.Max(hackerParryDuration, attackDelaySeconds);
            hackerReassembleSeconds = Mathf.Max(0.01f, reassembleSeconds);
            hackerDestroyAt = hackerAttackAt
                + hackerReassembleSeconds
                + Mathf.Max(0f, hackerReassembledHoldSeconds);
            hackerFollowTarget = followTarget;
            hackerFollowWorldOffset = followWorldOffset;

            ConfigureExternalMotionDriven(true);
            ConfigurePlayerCollisionIgnored(true);
            ConfigureInterceptable(true);
            ConfigurePathIndicatorSuppressed(true);
            OverrideProjectileLifetime(hackerDestroyAt - Time.time + 0.1f);
            FollowHackerAnchor();
            SetChargeGaugeVisible(true);
            SetChargeGaugeFill(1f);
        }

        internal void ConfigureRewardOverrides(int? countOverride, float? radiusOverride, float? lifetimeOverride)
        {
            rewardCountOverride = countOverride;
            rewardRadiusOverride = radiusOverride;
            rewardLifetimeOverride = lifetimeOverride;
        }

        public override bool TryDestroyByInterceptShot(out bool parried)
        {
            if (!hackerResilientMode)
            {
                return base.TryDestroyByInterceptShot(out parried);
            }

            if (!CanReceiveInterceptShot())
            {
                parried = false;
                return false;
            }

            parried = true;
            hackerWasParried = true;
            hackerParriedAt = Time.time;
            hackerReassembleStartsAt = Mathf.Max(
                hackerParriedAt + Mathf.Max(0.01f, hackerShatterSeconds),
                hackerAttackAt - hackerReassembleSeconds);
            hackerDestroyAt = hackerReassembleStartsAt
                + hackerReassembleSeconds
                + Mathf.Max(0f, hackerReassembledHoldSeconds);
            OverrideProjectileLifetime(hackerDestroyAt - Time.time + 0.1f);
            CompletePartialIntercept();
            ConfigureInterceptable(false);
            SetChargeGaugeVisible(false);
            CreateHackerShards();
            HackerParried?.Invoke();
            return true;
        }

        protected override void OnProjectileTick()
        {
            if (IsDestroying)
            {
                return;
            }

            if (hackerResilientMode)
            {
                TickHackerResilientMode();
                return;
            }

            float remainingRatio = ProjectileLifetime > 0f
                ? Mathf.Clamp01((DestroyAt - Time.time) / ProjectileLifetime)
                : 0f;
            SetChargeGaugeFill(remainingRatio);
        }

        protected override void OnProjectileDestroying(EnemyProjectileDestroyReason reason, Vector3 position)
        {
            if (!hackerResilientMode && reason == EnemyProjectileDestroyReason.Intercepted)
            {
                FireRewardCircle(position);
            }

            CleanupHackerShards(true);
            HackerParried = null;
        }

        protected override void OnProjectileReturnedToPool()
        {
            CleanupHackerShards(true);
            HackerParried = null;
            base.OnProjectileReturnedToPool();
        }

        protected override void OnDestroy()
        {
            CleanupHackerShards(true);
            base.OnDestroy();
        }

        protected override void ExtendSpecialTimers(float pausedSeconds)
        {
            base.ExtendSpecialTimers(pausedSeconds);
            if (!hackerResilientMode || pausedSeconds <= 0f)
            {
                return;
            }

            hackerParryEndsAt += pausedSeconds;
            hackerAttackAt += pausedSeconds;
            hackerReassembleStartsAt += pausedSeconds;
            hackerDestroyAt += pausedSeconds;
            if (hackerWasParried)
            {
                hackerParriedAt += pausedSeconds;
            }
        }

        private void TickHackerResilientMode()
        {
            FollowHackerAnchor();
            if (!hackerWasParried)
            {
                float remainingRatio = Mathf.Clamp01(
                    (hackerParryEndsAt - Time.time) / hackerParryDuration);
                SetChargeGaugeFill(remainingRatio);
                if (Time.time >= hackerParryEndsAt)
                {
                    DestroyFromOwner();
                }

                return;
            }

            if (Time.time < hackerReassembleStartsAt)
            {
                float shatterProgress = Mathf.Clamp01(
                    (Time.time - hackerParriedAt) / Mathf.Max(0.01f, hackerShatterSeconds));
                ApplyHackerShardProgress(shatterProgress, false);
                return;
            }

            if (!hackerReassemblyStarted)
            {
                hackerReassemblyStarted = true;
                for (int i = 0; i < hackerShards.Count; i++)
                {
                    hackerShards[i].ReassembleStartLocalPosition = hackerShards[i].Transform.localPosition;
                    hackerShards[i].ReassembleStartLocalRotation = hackerShards[i].Transform.localRotation;
                }
            }

            float reassembleProgress = Mathf.Clamp01(
                (Time.time - hackerReassembleStartsAt) / hackerReassembleSeconds);
            ApplyHackerShardProgress(reassembleProgress, true);
            if (!hackerReassemblyCompleted && reassembleProgress >= 1f)
            {
                hackerReassemblyCompleted = true;
                CleanupHackerShards(true);
            }

            if (Time.time >= hackerDestroyAt)
            {
                DestroyFromOwner();
            }
        }

        private void FollowHackerAnchor()
        {
            if (hackerFollowTarget != null)
            {
                transform.position = hackerFollowTarget.position + hackerFollowWorldOffset;
            }
        }

        private void SetChargeGaugeVisible(bool visible)
        {
            if (chargeGaugeRenderer != null)
            {
                chargeGaugeRenderer.enabled = visible;
            }
        }

        private void SetChargeGaugeFill(float remainingRatio)
        {
            if (chargeGaugeRenderer == null)
            {
                return;
            }

            chargeGaugePropertyBlock ??= new MaterialPropertyBlock();
            chargeGaugeRenderer.GetPropertyBlock(chargeGaugePropertyBlock);
            chargeGaugePropertyBlock.SetFloat(FillAmountId, remainingRatio);
            chargeGaugeRenderer.SetPropertyBlock(chargeGaugePropertyBlock);
        }

        private void CreateHackerShards()
        {
            CleanupHackerShards(true);
            SpriteRenderer source = FindHackerSourceRenderer();
            if (source == null || source.sprite == null)
            {
                return;
            }

            CaptureAndHideHackerSourceRenderers();
            const int columns = 3;
            const int rows = 2;
            Rect textureRect = source.sprite.textureRect;
            float shardWidth = textureRect.width / columns;
            float shardHeight = textureRect.height / rows;
            Vector2 sourcePivot = source.sprite.pivot;
            float pixelsPerUnit = source.sprite.pixelsPerUnit;

            try
            {
                for (int row = 0; row < rows; row++)
                {
                    for (int column = 0; column < columns; column++)
                    {
                        Rect shardRect = new(
                            textureRect.x + shardWidth * column,
                            textureRect.y + shardHeight * row,
                            shardWidth,
                            shardHeight);
                        Sprite shardSprite = Sprite.Create(
                            source.sprite.texture,
                            shardRect,
                            new Vector2(0.5f, 0.5f),
                            pixelsPerUnit);
                        Vector2 assembledOffset = new(
                            ((column + 0.5f) * shardWidth - sourcePivot.x) / pixelsPerUnit,
                            ((row + 0.5f) * shardHeight - sourcePivot.y) / pixelsPerUnit);
                        if (source.flipX)
                        {
                            assembledOffset.x *= -1f;
                        }
                        if (source.flipY)
                        {
                            assembledOffset.y *= -1f;
                        }

                        CreateHackerShard(source, shardSprite, assembledOffset, true, row * columns + column);
                    }
                }
            }
            catch (UnityException)
            {
                CleanupHackerShards(true);
                CaptureAndHideHackerSourceRenderers();
                CreateFallbackHackerShards(source);
            }
            catch (ArgumentException)
            {
                CleanupHackerShards(true);
                CaptureAndHideHackerSourceRenderers();
                CreateFallbackHackerShards(source);
            }
        }

        private SpriteRenderer FindHackerSourceRenderer()
        {
            Transform authoredVisual = transform.Find("BulletVisual");
            SpriteRenderer authoredRenderer = authoredVisual != null
                ? authoredVisual.GetComponent<SpriteRenderer>()
                : null;
            if (IsHackerBodyRenderer(authoredRenderer))
            {
                return authoredRenderer;
            }

            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (IsHackerBodyRenderer(renderers[i]))
                {
                    return renderers[i];
                }
            }

            return null;
        }

        private bool IsHackerBodyRenderer(SpriteRenderer renderer)
        {
            return renderer != null
                && renderer.sprite != null
                && renderer != chargeGaugeRenderer
                && !renderer.gameObject.name.StartsWith("HackerParryShard_", StringComparison.Ordinal)
                && !IsParryLockOnIndicatorRenderer(renderer);
        }

        private void CaptureAndHideHackerSourceRenderers()
        {
            hackerSourceRenderers.Clear();
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (!IsHackerBodyRenderer(renderer))
                {
                    continue;
                }

                hackerSourceRenderers.Add(new RendererState(renderer, renderer.enabled));
                renderer.enabled = false;
            }
        }

        private void CreateFallbackHackerShards(SpriteRenderer source)
        {
            for (int i = 0; i < 6; i++)
            {
                float angle = i * 60f * Mathf.Deg2Rad;
                Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.08f;
                CreateHackerShard(source, source.sprite, offset, false, i, 0.42f);
            }
        }

        private void CreateHackerShard(
            SpriteRenderer source,
            Sprite sprite,
            Vector2 assembledOffset,
            bool ownsSprite,
            int index,
            float scale = 1f)
        {
            GameObject shardObject = new($"HackerParryShard_{index}");
            shardObject.layer = gameObject.layer;
            shardObject.transform.SetParent(source.transform, false);
            shardObject.transform.localPosition = assembledOffset;
            shardObject.transform.localRotation = Quaternion.identity;
            shardObject.transform.localScale = Vector3.one * scale;

            SpriteRenderer shardRenderer = shardObject.AddComponent<SpriteRenderer>();
            shardRenderer.sprite = sprite;
            shardRenderer.color = source.color;
            shardRenderer.flipX = source.flipX;
            shardRenderer.flipY = source.flipY;
            shardRenderer.sharedMaterial = source.sharedMaterial;
            shardRenderer.sortingLayerID = source.sortingLayerID;
            shardRenderer.sortingOrder = source.sortingOrder + 1;

            Vector2 direction = assembledOffset.sqrMagnitude > 0.0001f
                ? assembledOffset.normalized
                : new Vector2(Mathf.Cos(index * 60f * Mathf.Deg2Rad), Mathf.Sin(index * 60f * Mathf.Deg2Rad));
            float distance = Mathf.Max(0.01f, hackerShardDistance) * (0.8f + index * 0.06f);
            float rotation = (index % 2 == 0 ? 1f : -1f) * (24f + index * 7f);
            hackerShards.Add(new ShardState(
                shardObject.transform,
                shardRenderer,
                sprite,
                ownsSprite,
                assembledOffset,
                assembledOffset + direction * distance,
                Quaternion.Euler(0f, 0f, rotation)));
        }

        private void ApplyHackerShardProgress(float progress, bool reassembling)
        {
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            for (int i = 0; i < hackerShards.Count; i++)
            {
                ShardState shard = hackerShards[i];
                if (shard.Transform == null)
                {
                    continue;
                }

                if (reassembling)
                {
                    shard.Transform.localPosition = Vector3.Lerp(
                        shard.ReassembleStartLocalPosition,
                        shard.AssembledLocalPosition,
                        eased);
                    shard.Transform.localRotation = Quaternion.Slerp(
                        shard.ReassembleStartLocalRotation,
                        Quaternion.identity,
                        eased);
                }
                else
                {
                    shard.Transform.localPosition = Vector3.Lerp(
                        shard.AssembledLocalPosition,
                        shard.ScatteredLocalPosition,
                        eased);
                    shard.Transform.localRotation = Quaternion.Slerp(
                        Quaternion.identity,
                        shard.ScatteredLocalRotation,
                        eased);
                }
            }
        }

        private void CleanupHackerShards(bool restoreSourceRenderers)
        {
            for (int i = 0; i < hackerShards.Count; i++)
            {
                ShardState shard = hackerShards[i];
                if (shard.Renderer != null)
                {
                    shard.Renderer.enabled = false;
                }
                if (shard.Transform != null)
                {
                    shard.Transform.SetParent(null, true);
                    shard.Transform.gameObject.SetActive(false);
                    Destroy(shard.Transform.gameObject);
                }
                if (shard.OwnsSprite && shard.Sprite != null)
                {
                    Destroy(shard.Sprite);
                }
            }
            hackerShards.Clear();

            if (restoreSourceRenderers)
            {
                for (int i = 0; i < hackerSourceRenderers.Count; i++)
                {
                    RendererState state = hackerSourceRenderers[i];
                    if (state.Renderer != null)
                    {
                        state.Renderer.enabled = state.WasEnabled;
                    }
                }
            }
            hackerSourceRenderers.Clear();
        }

        private void FireRewardCircle(Vector3 center)
        {
            EnemyProjectile prefab = rewardProjectile?.Prefab;
            if (prefab == null)
            {
                return;
            }

            int count = Mathf.Max(1, rewardCountOverride ?? rewardBulletCount);
            float radius = Mathf.Max(0.01f, rewardRadiusOverride ?? rewardCircleRadius);
            float lifetime = Mathf.Max(0.01f, rewardLifetimeOverride ?? rewardLifetime);
            float step = 360f / count;

            for (int i = 0; i < count; i++)
            {
                Vector2 direction = AngleToDirection(step * i);
                Vector3 spawnPosition = center + (Vector3)(direction * radius);
                EnemyProjectile reward = Spawn(
                    prefab,
                    OwnerBullets,
                    spawnPosition,
                    direction,
                    rewardProjectile.BulletDamage,
                    0f,
                    rewardProjectile.Speed,
                    lifetime,
                    rewardProjectile.Radius,
                    ProjectileColor,
                    rewardProjectile.TrailSeconds,
                    rewardProjectile.TrailWidthMultiplier,
                    false,
                    0f,
                    0f);
                reward?.ConfigurePlayerCollisionIgnored(true);
            }
        }

        private static Vector2 AngleToDirection(float angleDegrees)
        {
            float radians = angleDegrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        private sealed class ShardState
        {
            internal ShardState(
                Transform transform,
                SpriteRenderer renderer,
                Sprite sprite,
                bool ownsSprite,
                Vector3 assembledLocalPosition,
                Vector3 scatteredLocalPosition,
                Quaternion scatteredLocalRotation)
            {
                Transform = transform;
                Renderer = renderer;
                Sprite = sprite;
                OwnsSprite = ownsSprite;
                AssembledLocalPosition = assembledLocalPosition;
                ScatteredLocalPosition = scatteredLocalPosition;
                ScatteredLocalRotation = scatteredLocalRotation;
            }

            internal Transform Transform { get; }
            internal SpriteRenderer Renderer { get; }
            internal Sprite Sprite { get; }
            internal bool OwnsSprite { get; }
            internal Vector3 AssembledLocalPosition { get; }
            internal Vector3 ScatteredLocalPosition { get; }
            internal Quaternion ScatteredLocalRotation { get; }
            internal Vector3 ReassembleStartLocalPosition { get; set; }
            internal Quaternion ReassembleStartLocalRotation { get; set; }
        }

        private readonly struct RendererState
        {
            internal RendererState(SpriteRenderer renderer, bool wasEnabled)
            {
                Renderer = renderer;
                WasEnabled = wasEnabled;
            }

            internal SpriteRenderer Renderer { get; }
            internal bool WasEnabled { get; }
        }
    }
}
