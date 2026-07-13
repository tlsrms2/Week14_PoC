using System;
using System.Collections;
using UnityEngine;
using Week14.Combat;
using Week14.Enemy;

namespace Week14.Skills
{
    public sealed class PlayerCloneAttackEcho : MonoBehaviour
    {
        private const int CloneSortingOffset = -1;

        private PlayerCombatController owner;
        private GameObject cloneVisualRoot;
        private Coroutine activeRoutine;
        private Action onEnded;
        private float damageMultiplier = 0.5f;
        private Color cloneTint = new Color(0.45f, 0.9f, 1f, 0.55f);

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

            if (owner == null)
            {
                onEnded?.Invoke();
                return;
            }

            SpawnCloneVisual();
            owner.PlayerAttackPerformed += HandlePlayerAttackPerformed;
            activeRoutine = StartCoroutine(DurationRoutine(durationSeconds));
        }

        private void HandlePlayerAttackPerformed(int playerDamage)
        {
            if (owner == null || cloneVisualRoot == null || playerDamage <= 0)
            {
                return;
            }

            PlayerCombatConfig config = owner.Config;
            if (config == null || config.ProjectilePrefab == null)
            {
                return;
            }

            BossAI boss = ResolveTargetBoss();
            Transform target = boss != null ? (boss.BodyRoot != null ? boss.BodyRoot : boss.transform) : null;
            if (target == null)
            {
                return;
            }

            Vector2 origin = cloneVisualRoot.transform.position;
            Vector2 direction = (Vector2)target.position - origin;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = Vector2.right;
            }

            int cloneDamage = Mathf.Max(1, Mathf.RoundToInt(playerDamage * damageMultiplier));
            PlayerProjectile projectile = PlayerProjectile.Spawn(
                config.ProjectilePrefab,
                origin,
                direction.normalized,
                owner,
                config.ProjectileSpeed,
                config.ProjectileLifetime,
                config.ProjectileRadius,
                cloneDamage,
                cloneTint,
                true,
                isSkillShot: true);

            if (projectile != null)
            {
                ProjectileVfx.PlayMuzzleFlash(origin, direction.normalized, cloneTint, 0.8f);
            }
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
                return;
            }

            cloneVisualRoot = new GameObject("PlayerCloneEcho");
            cloneVisualRoot.transform.position = owner.Context.CombatCenterOrigin.position;

            Color[] baseColors = owner.Context.BodyBaseColors;
            bool hasRenderer = false;
            for (int i = 0; i < sourceRenderers.Length; i++)
            {
                SpriteRenderer source = sourceRenderers[i];
                if (source == null || !source.enabled || source.sprite == null || source.color.a <= 0.01f)
                {
                    continue;
                }

                GameObject cloneObject = new GameObject(source.name) { layer = source.gameObject.layer };
                cloneObject.transform.SetParent(cloneVisualRoot.transform, true);
                cloneObject.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                cloneObject.transform.localScale = source.transform.lossyScale;

                SpriteRenderer clone = cloneObject.AddComponent<SpriteRenderer>();
                clone.sprite = source.sprite;
                clone.flipX = source.flipX;
                clone.flipY = source.flipY;
                clone.material = source.sharedMaterial;
                clone.sortingLayerID = source.sortingLayerID;
                clone.sortingOrder = source.sortingOrder + CloneSortingOffset;
                clone.maskInteraction = source.maskInteraction;
                Color baseColor = baseColors != null && i < baseColors.Length ? baseColors[i] : source.color;
                baseColor.a = source.color.a;
                clone.color = MultiplyColor(baseColor, cloneTint);
                hasRenderer = true;
            }

            if (!hasRenderer)
            {
                Destroy(cloneVisualRoot);
                cloneVisualRoot = new GameObject("PlayerCloneEcho");
                cloneVisualRoot.transform.position = owner.Context.CombatCenterOrigin.position;
            }
        }

        private IEnumerator DurationRoutine(float durationSeconds)
        {
            yield return new WaitForSeconds(Mathf.Max(0.1f, durationSeconds));
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
    }
}
