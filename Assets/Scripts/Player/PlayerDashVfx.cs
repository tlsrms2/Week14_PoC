using System.Collections;
using UnityEngine;

namespace Week14.Combat
{
    internal static class PlayerDashVfx
    {
        private const int RollAfterimageSortingOffset = -1;
        private const int AbsorbSortingOrder = 74;

        internal static void SpawnRollAfterimage(MonoBehaviour host, SpriteRenderer[] sourceRenderers, float seconds, Color tint)
        {
            if (host == null || sourceRenderers == null || sourceRenderers.Length == 0 || seconds <= 0f)
            {
                return;
            }

            GameObject root = new GameObject("RollAfterimageVfx");
            SpriteRenderer[] clones = new SpriteRenderer[sourceRenderers.Length];
            Color[] startColors = new Color[sourceRenderers.Length];
            bool hasRenderer = false;

            for (int i = 0; i < sourceRenderers.Length; i++)
            {
                SpriteRenderer source = sourceRenderers[i];
                if (source == null || !source.enabled || source.sprite == null || source.color.a <= 0.01f)
                {
                    continue;
                }

                GameObject cloneObject = new GameObject(source.name);
                cloneObject.transform.SetParent(root.transform, true);
                cloneObject.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                cloneObject.transform.localScale = source.transform.lossyScale;

                SpriteRenderer clone = cloneObject.AddComponent<SpriteRenderer>();
                clone.sprite = source.sprite;
                clone.flipX = source.flipX;
                clone.flipY = source.flipY;
                clone.material = source.sharedMaterial;
                clone.sortingLayerID = source.sortingLayerID;
                clone.sortingOrder = source.sortingOrder + RollAfterimageSortingOffset;
                clone.maskInteraction = source.maskInteraction;
                clone.color = MultiplyColor(source.color, tint);
                clones[i] = clone;
                startColors[i] = clone.color;
                hasRenderer = true;
            }

            if (!hasRenderer)
            {
                Object.Destroy(root);
                return;
            }

            host.StartCoroutine(FadeAndDestroyRoutine(root, clones, startColors, seconds));
        }

        internal static void PlayProjectileAbsorb(MonoBehaviour host, EnemyProjectile projectile, Vector3 targetPosition, float seconds, Color tint)
        {
            if (host == null || projectile == null || seconds <= 0f)
            {
                return;
            }

            SpriteRenderer source = projectile.GetComponentInChildren<SpriteRenderer>();
            if (source == null || source.sprite == null)
            {
                return;
            }

            GameObject absorbObject = new GameObject("DashProjectileAbsorbVfx");
            absorbObject.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            absorbObject.transform.localScale = source.transform.lossyScale;

            SpriteRenderer renderer = absorbObject.AddComponent<SpriteRenderer>();
            renderer.sprite = source.sprite;
            renderer.flipX = source.flipX;
            renderer.flipY = source.flipY;
            renderer.material = source.sharedMaterial;
            renderer.sortingLayerID = source.sortingLayerID;
            renderer.sortingOrder = Mathf.Max(source.sortingOrder + 2, AbsorbSortingOrder);
            renderer.color = MultiplyColor(source.color, tint);

            host.StartCoroutine(AbsorbAndDestroyRoutine(absorbObject, renderer, targetPosition, seconds));
        }

        private static Color MultiplyColor(Color source, Color tint)
        {
            return new Color(
                source.r * tint.r,
                source.g * tint.g,
                source.b * tint.b,
                source.a * tint.a);
        }

        private static IEnumerator FadeAndDestroyRoutine(
            GameObject root,
            SpriteRenderer[] renderers,
            Color[] startColors,
            float seconds)
        {
            float duration = Mathf.Max(0.01f, seconds);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                float alphaScale = 1f - t;

                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] == null)
                    {
                        continue;
                    }

                    Color color = startColors[i];
                    color.a *= alphaScale;
                    renderers[i].color = color;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            Object.Destroy(root);
        }

        private static IEnumerator AbsorbAndDestroyRoutine(
            GameObject absorbObject,
            SpriteRenderer spriteRenderer,
            Vector3 targetPosition,
            float seconds)
        {
            if (absorbObject == null)
            {
                yield break;
            }

            Vector3 startPosition = absorbObject.transform.position;
            targetPosition.z = startPosition.z;
            Vector3 startScale = absorbObject.transform.localScale;
            Color startColor = spriteRenderer != null ? spriteRenderer.color : Color.white;
            float duration = Mathf.Max(0.01f, seconds);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);

                absorbObject.transform.position = Vector3.Lerp(startPosition, targetPosition, eased);
                absorbObject.transform.localScale = Vector3.Lerp(startScale, startScale * 0.15f, eased);
                if (spriteRenderer != null)
                {
                    Color color = startColor;
                    color.a *= 1f - t;
                    spriteRenderer.color = color;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            Object.Destroy(absorbObject);
        }
    }
}
