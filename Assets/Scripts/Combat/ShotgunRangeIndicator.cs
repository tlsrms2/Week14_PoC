using UnityEngine;
using Week14.Input;
using Week14.Weapons;

namespace Week14.Combat
{
    public sealed class ShotgunRangeIndicator : MonoBehaviour
    {
        [SerializeField] private LineRenderer lineRenderer;
        [SerializeField, Min(3)] private int arcSegments = 24;
        [SerializeField] private Color lineColor = new Color(1f, 0.7f, 0.2f, 0.8f);
        [SerializeField, Min(0f)] private float lineWidth = 0.05f;
        [SerializeField, Min(1f)] private float dashTiling = 6f;

        private float spreadAngle;
        private float maxRange;
        private Material lineMaterial;

        private void Awake()
        {
            SetupLineRenderer();

            if (WeaponLoadoutManager.Instance != null)
            {
                WeaponLoadoutManager.Instance.WeaponChanged += HandleWeaponChanged;
                HandleWeaponChanged(WeaponLoadoutManager.Instance.CurrentWeapon);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (WeaponLoadoutManager.Instance != null)
                WeaponLoadoutManager.Instance.WeaponChanged -= HandleWeaponChanged;
        }

        private void LateUpdate()
        {
            PlayerCombatController player = PlayerCombatController.Active;
            if (player == null || !player.IsReticleVisible)
            {
                if (lineRenderer != null) lineRenderer.enabled = false;
                return;
            }

            if (lineRenderer != null) lineRenderer.enabled = true;

            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 origin = player.LeftGunOrigin != null
                ? player.LeftGunOrigin.position
                : player.transform.position;
            origin.z = 0f;

            Vector2 mouseWorld = cam.ScreenToWorldPoint(GameInput.MouseScreenPosition);
            Vector2 aimDir = mouseWorld - (Vector2)origin;
            if (aimDir.sqrMagnitude < 0.0001f) aimDir = Vector2.right;
            aimDir.Normalize();

            DrawFan(origin, aimDir);
        }

        private void DrawFan(Vector3 origin, Vector2 aimDir)
        {
            if (lineRenderer == null) return;

            lineRenderer.startWidth = lineWidth;
            lineRenderer.endWidth = lineWidth;
            if (lineMaterial != null)
            {
                lineMaterial.color = lineColor;
                lineMaterial.mainTextureScale = new Vector2(dashTiling, 1f);
            }

            float aimAngleDeg = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg;
            float halfSpread = spreadAngle * 0.5f;

            lineRenderer.positionCount = arcSegments + 1;
            for (int i = 0; i <= arcSegments; i++)
            {
                float angleDeg = aimAngleDeg + halfSpread - i * spreadAngle / arcSegments;
                float angleRad = angleDeg * Mathf.Deg2Rad;
                Vector3 point = origin + new Vector3(Mathf.Cos(angleRad), Mathf.Sin(angleRad), 0f) * maxRange;
                lineRenderer.SetPosition(i, point);
            }
        }

        private void HandleWeaponChanged(BaseWeaponSO weapon)
        {
            if (weapon is ShotgunWeaponSO shotgun)
            {
                spreadAngle = (shotgun.MaxAmmo - 1) * shotgun.PelletStep;
                maxRange = shotgun.MaxRange;
                gameObject.SetActive(true);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private void SetupLineRenderer()
        {
            if (lineRenderer == null) return;
            lineRenderer.useWorldSpace = true;
            lineRenderer.loop = false;
            lineRenderer.textureMode = LineTextureMode.Tile;

            Texture2D dashTex = new(4, 1, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Point
            };
            dashTex.SetPixels32(new Color32[]
            {
                new(255, 255, 255, 255),
                new(255, 255, 255, 255),
                new(0, 0, 0, 0),
                new(0, 0, 0, 0)
            });
            dashTex.Apply();

            lineMaterial = new Material(Shader.Find("Unlit/Transparent")) { mainTexture = dashTex };
            lineRenderer.sharedMaterial = lineMaterial;
        }
    }
}
