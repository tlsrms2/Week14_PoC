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
            Sprite[] baseSprites = new Sprite[renderers.Length];
            Vector3[] baseLocalPositions = new Vector3[renderers.Length];
            Quaternion[] baseLocalRotations = new Quaternion[renderers.Length];
            Vector3[] baseLocalScales = new Vector3[renderers.Length];
            bool[] baseFlipX = new bool[renderers.Length];
            bool[] baseFlipY = new bool[renderers.Length];

            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                baseColors[i] = renderer.color;
                baseSprites[i] = renderer.sprite;
                baseLocalPositions[i] = targetRoot.InverseTransformPoint(renderer.transform.position);
                baseLocalRotations[i] = Quaternion.Inverse(targetRoot.rotation) * renderer.transform.rotation;
                baseLocalScales[i] = renderer.transform.localScale;
                baseFlipX[i] = renderer.flipX;
                baseFlipY[i] = renderer.flipY;
            }

            context.BodyRenderers = renderers;
            context.BodyBaseColors = baseColors;
            context.BodyBaseSprites = baseSprites;
            context.BodyBaseLocalPositions = baseLocalPositions;
            context.BodyBaseLocalRotations = baseLocalRotations;
            context.BodyBaseLocalScales = baseLocalScales;
            context.BodyBaseFlipX = baseFlipX;
            context.BodyBaseFlipY = baseFlipY;
        }

        internal Transform GetLeftFireOrigin()
        {
            return context.LeftGunFireOrigin != null ? context.LeftGunFireOrigin : context.LeftGunOrigin;
        }

        internal Transform GetRightFireOrigin()
        {
            if (context.RightGunFireOrigin != null)
            {
                return context.RightGunFireOrigin;
            }

            return GetLeftFireOrigin() != null ? GetLeftFireOrigin() : context.PlayerTransform;
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
