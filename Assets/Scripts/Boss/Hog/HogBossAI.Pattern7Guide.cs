using System.Collections.Generic;
using UnityEngine;

namespace Week14.Enemy
{
    public sealed partial class HogBossAI
    {
        private void UpdatePattern7GuideLines(Vector3 origin, Vector2 baseDirection)
        {
            ProjectileSettings projectileSettings = GetProjectile(pattern7.NormalProjectileIndex);
            if (projectileSettings == null)
            {
                pattern7GuideView.Hide();
                return;
            }

            pattern7GuideView.Show(
                this,
                origin,
                baseDirection,
                pattern7.FanAngleDegrees,
                projectileSettings.Speed,
                projectileSettings.Lifetime,
                projectileSettings.Radius,
                projectileSettings.ChargingColor);
        }

        private void HidePattern7GuideLines()
        {
            pattern7GuideView.Hide();
        }

        private sealed class Pattern7GuideView
        {
            private const string WallLayerName = "Wall";
            private const int MaxDashCountPerLine = 160;
            private const float DashLength = 0.2f;
            private const float DashGap = 0.14f;

            private static Material material;

            private readonly List<LineRenderer> guideLines = new();
            private Component owner;

            public void Show(
                Component nextOwner,
                Vector3 origin,
                Vector2 baseDirection,
                float fanAngleDegrees,
                float projectileSpeed,
                float projectileLifetime,
                float projectileRadius,
                Color chargingColor)
            {
                owner = nextOwner;
                if (owner == null || baseDirection.sqrMagnitude <= 0.0001f)
                {
                    Hide();
                    return;
                }

                float baseAngle = Mathf.Atan2(baseDirection.y, baseDirection.x) * Mathf.Rad2Deg;
                float halfFanAngle = fanAngleDegrees * 0.5f;
                float maxLength = Mathf.Max(0f, projectileSpeed * projectileLifetime);
                float width = Mathf.Max(0.013f, projectileRadius * 0.14f);
                Color color = chargingColor;
                color.a = 0.58f;
                int visibleDashCount = 0;

                for (int i = 0; i < 3; i++)
                {
                    float t = i - 1f;
                    Vector2 direction = AngleToDirection(baseAngle + halfFanAngle * t);
                    float length = GetGuideLength(origin, direction, maxLength, projectileRadius);
                    visibleDashCount = DrawDashes(visibleDashCount, origin, direction, length, color, width);
                }

                HideFrom(visibleDashCount);
            }

            public void Hide()
            {
                HideFrom(0);
            }

            private int DrawDashes(int startDashIndex, Vector3 start, Vector2 direction, float length, Color color, float width)
            {
                if (length <= 0.01f)
                {
                    return startDashIndex;
                }

                Vector2 normalized = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.left;
                int dashCount = Mathf.Min(MaxDashCountPerLine, Mathf.CeilToInt(length / (DashLength + DashGap)));
                int nextDashIndex = startDashIndex;

                for (int i = 0; i < dashCount; i++)
                {
                    float segmentStart = i * (DashLength + DashGap);
                    float segmentEnd = Mathf.Min(segmentStart + DashLength, length);
                    if (segmentEnd <= 0f)
                    {
                        continue;
                    }

                    LineRenderer dash = EnsureGuideLine(nextDashIndex);
                    if (dash == null)
                    {
                        continue;
                    }

                    dash.enabled = true;
                    dash.startColor = color;
                    dash.endColor = color;
                    dash.startWidth = width;
                    dash.endWidth = width;
                    dash.SetPosition(0, start + (Vector3)(normalized * segmentStart));
                    dash.SetPosition(1, start + (Vector3)(normalized * segmentEnd));
                    nextDashIndex++;
                }

                return nextDashIndex;
            }

            private static float GetGuideLength(Vector2 start, Vector2 direction, float maxLength, float projectileRadius)
            {
                if (maxLength <= 0.01f)
                {
                    return 0f;
                }

                int wallMask = GetWallMask();
                if (wallMask == 0 || direction.sqrMagnitude <= 0.0001f)
                {
                    return maxLength;
                }

                float castRadius = Mathf.Max(0.001f, projectileRadius);
                RaycastHit2D hit = Physics2D.CircleCast(start, castRadius, direction.normalized, maxLength, wallMask);
                return hit.collider != null ? Mathf.Max(0f, hit.distance) : maxLength;
            }

            private static int GetWallMask()
            {
                int wallLayer = LayerMask.NameToLayer(WallLayerName);
                return wallLayer >= 0 ? 1 << wallLayer : 0;
            }

            private LineRenderer EnsureGuideLine(int index)
            {
                if (owner == null)
                {
                    return null;
                }

                while (guideLines.Count <= index)
                {
                    GameObject lineObject = new($"Pattern7GuideLine_{guideLines.Count:00}");
                    lineObject.transform.SetParent(owner.transform, false);
                    LineRenderer line = lineObject.AddComponent<LineRenderer>();
                    line.material = GetMaterial();
                    line.useWorldSpace = true;
                    line.loop = false;
                    line.positionCount = 2;
                    line.numCornerVertices = 0;
                    line.numCapVertices = 1;
                    line.sortingOrder = 17;
                    guideLines.Add(line);
                }

                LineRenderer guideLine = guideLines[index];
                guideLine.material = GetMaterial();
                return guideLine;
            }

            private void HideFrom(int startIndex)
            {
                for (int i = Mathf.Max(0, startIndex); i < guideLines.Count; i++)
                {
                    if (guideLines[i] != null)
                    {
                        guideLines[i].enabled = false;
                    }
                }
            }

            private static Material GetMaterial()
            {
                if (material != null)
                {
                    return material;
                }

                Shader shader = Shader.Find("Sprites/Default");
                material = shader != null ? new Material(shader) : null;
                return material;
            }

            private static Vector2 AngleToDirection(float angleDegrees)
            {
                float radians = angleDegrees * Mathf.Deg2Rad;
                return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            }
        }
    }
}
