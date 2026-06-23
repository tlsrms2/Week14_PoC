using UnityEngine;

namespace Week14.Combat
{
    internal sealed class PlayerCombatRig
    {
        private const string BodyVisualName = "VisualRoot";
        private const string CombatCenterName = "Center Pivot";

        private readonly PlayerCombatController.PlayerCombatContext context;

        internal PlayerCombatRig(PlayerCombatController.PlayerCombatContext context)
        {
            this.context = context;
        }

        internal Transform CombatCenterOrigin => context.CombatCenter != null
            ? context.CombatCenter
            : (context.BodyRoot != null ? context.BodyRoot : context.PlayerTransform);

        internal void ResolveReferences()
        {
            if (context.BodyRoot == null)
            {
                context.BodyRoot = FindChildRecursive(context.PlayerTransform, BodyVisualName);
            }

            if (context.CombatCenter == null)
            {
                context.CombatCenter = FindChildRecursive(context.PlayerTransform, CombatCenterName);
            }

            context.LeftGunOrigin ??= context.PlayerTransform;
            context.LeftGunFireOrigin ??= context.LeftGunOrigin;
        }

        internal void ResolveMouseParryReticleReference()
        {
            if (context.MouseParryReticle != null || context.MouseParryReticleRenderer == null)
            {
                return;
            }

            MouseParryReticle reticle = context.MouseParryReticleRenderer.GetComponent<MouseParryReticle>();
            if (reticle == null)
            {
                reticle = context.MouseParryReticleRenderer.GetComponentInParent<MouseParryReticle>();
            }

            context.MouseParryReticle = reticle;
        }

        internal void CacheBodyRenderers()
        {
            Transform targetRoot = context.BodyRoot != null ? context.BodyRoot : context.PlayerTransform;
            SpriteRenderer[] renderers = targetRoot.GetComponentsInChildren<SpriteRenderer>(true);
            Color[] baseColors = new Color[renderers.Length];

            for (int i = 0; i < renderers.Length; i++)
            {
                baseColors[i] = renderers[i].color;
            }

            context.BodyRenderers = renderers;
            context.BodyBaseColors = baseColors;
        }

        internal Transform GetLeftFireOrigin()
        {
            return context.LeftGunFireOrigin != null ? context.LeftGunFireOrigin : context.LeftGunOrigin;
        }

        internal void StopBody()
        {
            if (context.Body != null)
            {
                context.Body.linearVelocity = Vector2.zero;
            }
        }

        private static Transform FindChildRecursive(Transform root, string childName)
        {
            if (root == null)
            {
                return null;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == childName)
                {
                    return child;
                }

                Transform nested = FindChildRecursive(child, childName);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }
    }
}
