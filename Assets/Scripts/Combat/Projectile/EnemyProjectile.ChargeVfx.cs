using UnityEngine;
using Week14.Enemy;

namespace Week14.Combat
{
    public partial class EnemyProjectile
    {
        private void UpdateChargeVfx()
        {
            if (!canBeIntercepted)
            {
                SetChargeVfxVisible(false);
                return;
            }

            LineRenderer line = EnsureChargeVfx();
            if (line == null)
            {
                return;
            }

            float t = projectileChargeSeconds <= 0f
                ? 1f
                : 1f - Mathf.Clamp01((chargeEndsAt - Time.time) / projectileChargeSeconds);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 24f);
            float radius = Mathf.Lerp(projectileRadius * 1.9f, projectileRadius * 3.1f, pulse);
            Color chargeColor = Color.Lerp(projectileColor, Color.white, 0.55f + 0.25f * pulse);
            chargeColor.a = Mathf.Lerp(0.35f, 0.95f, t);

            line.enabled = true;
            line.startColor = chargeColor;
            line.endColor = chargeColor;
            line.startWidth = Mathf.Max(0.018f, projectileRadius * 0.32f);
            line.endWidth = line.startWidth;
            line.positionCount = 5;
            line.SetPosition(0, new Vector3(0f, radius, 0f));
            line.SetPosition(1, new Vector3(radius, 0f, 0f));
            line.SetPosition(2, new Vector3(0f, -radius, 0f));
            line.SetPosition(3, new Vector3(-radius, 0f, 0f));
            line.SetPosition(4, new Vector3(0f, radius, 0f));
        }

        private LineRenderer EnsureChargeVfx()
        {
            if (chargeVfx != null)
            {
                return chargeVfx;
            }

            Transform existing = transform.Find(ChargeVfxName);
            if (existing == null)
            {
                return null;
            }

            chargeVfx = existing.GetComponent<LineRenderer>();
            if (chargeVfx == null)
            {
                return null;
            }

            chargeVfx.useWorldSpace = false;
            chargeVfx.loop = false;
            chargeVfx.numCornerVertices = 2;
            chargeVfx.numCapVertices = 2;
            BossSorting.Apply(chargeVfx);
            chargeVfx.sortingOrder = 24;
            chargeVfx.material = GetChargeVfxMaterial();
            return chargeVfx;
        }

        private static Material GetChargeVfxMaterial()
        {
            if (chargeVfxMaterial != null)
            {
                return chargeVfxMaterial;
            }

            Shader shader = Shader.Find("Sprites/Default");
            chargeVfxMaterial = shader != null ? new Material(shader) : null;
            return chargeVfxMaterial;
        }

        private void SetChargeVfxVisible(bool visible)
        {
            if (chargeVfx != null)
            {
                chargeVfx.enabled = visible;
            }
        }

    }
}
