using System.Collections.Generic;
using UnityEngine;

namespace Week14.Enemy
{
    [AddComponentMenu("Week14/Boss/Conductor Conducting Pattern Visual")]
    public sealed class ConductorConductingPatternVisual : MonoBehaviour
    {
        private const string StrokeLineName = "ConductorConductingStroke";
        private const string StrokeOutlineName = "ConductorConductingStrokeOutline";

        private static Material lineMaterial;

        private readonly List<LineRenderer> renderers = new();
        private readonly List<LineRenderer> outlineRenderers = new();
        private readonly List<Vector2> strokePoints = new();
        private readonly List<Vector3> renderPoints = new();
        private ConductorConductingPattern pattern;
        private ConductorConductingCueSettings settings;
        private float alphaMultiplier = 1f;
        private bool completedStyle;
        private bool flashStyle;

        public void Configure(ConductorConductingPattern nextPattern, ConductorConductingCueSettings nextSettings)
        {
            pattern = nextPattern;
            settings = nextSettings;
            alphaMultiplier = 1f;
            completedStyle = false;
            flashStyle = false;

            int strokeCount = pattern?.Strokes?.Count ?? 0;
            for (int i = 0; i < strokeCount; i++)
            {
                EnsureRenderer(i).enabled = false;
                EnsureOutlineRenderer(i).enabled = false;
            }

            for (int i = strokeCount; i < renderers.Count; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].enabled = false;
                }
            }

            for (int i = strokeCount; i < outlineRenderers.Count; i++)
            {
                if (outlineRenderers[i] != null)
                {
                    outlineRenderers[i].enabled = false;
                }
            }
        }

        public void SetAlpha(float alpha)
        {
            alphaMultiplier = Mathf.Clamp01(alpha);
            for (int i = 0; i < renderers.Count; i++)
            {
                if (renderers[i] != null)
                {
                    ApplyRendererStyle(renderers[i]);
                }
            }

            for (int i = 0; i < outlineRenderers.Count; i++)
            {
                if (outlineRenderers[i] != null)
                {
                    ApplyOutlineRendererStyle(outlineRenderers[i]);
                }
            }
        }

        public void SetStrokeProgress(int strokeIndex, float progress)
        {
            if (pattern == null
                || strokeIndex < 0
                || pattern.Strokes == null
                || strokeIndex >= pattern.Strokes.Count)
            {
                return;
            }

            ConductorConductingStroke stroke = pattern.Strokes[strokeIndex];
            LineRenderer line = EnsureRenderer(strokeIndex);
            LineRenderer outline = EnsureOutlineRenderer(strokeIndex);
            if (stroke == null || !stroke.HasDrawablePoints || alphaMultiplier <= 0f)
            {
                line.enabled = false;
                outline.enabled = false;
                return;
            }

            stroke.BuildRenderPoints(strokePoints);
            BuildStrokePoints(strokePoints, settings.Scale, Mathf.Clamp01(progress), renderPoints);
            line.enabled = renderPoints.Count >= 2;
            if (!line.enabled)
            {
                outline.enabled = false;
                return;
            }

            line.positionCount = renderPoints.Count;
            ApplyRendererStyle(line);
            outline.enabled = (completedStyle || flashStyle)
                && settings.DrawCompletedOutline
                && settings.CompletedOutlineWidth > 0f;
            if (outline.enabled)
            {
                outline.positionCount = renderPoints.Count;
                ApplyOutlineRendererStyle(outline);
            }

            for (int i = 0; i < renderPoints.Count; i++)
            {
                Vector3 point = renderPoints[i];
                line.SetPosition(i, point);
                if (outline.enabled)
                {
                    outline.SetPosition(i, point);
                }
            }
        }

        public void ApplyCompletedStyle()
        {
            flashStyle = false;
            completedStyle = true;
            int strokeCount = pattern?.Strokes?.Count ?? 0;
            for (int i = 0; i < strokeCount; i++)
            {
                SetStrokeProgress(i, 1f);
            }
        }

        public void ApplyFlashStyle()
        {
            completedStyle = false;
            flashStyle = true;
            int strokeCount = pattern?.Strokes?.Count ?? 0;
            for (int i = 0; i < strokeCount; i++)
            {
                SetStrokeProgress(i, 1f);
            }
        }

        public void ClearAndDestroy()
        {
            for (int i = 0; i < renderers.Count; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].enabled = false;
                }
            }

            for (int i = 0; i < outlineRenderers.Count; i++)
            {
                if (outlineRenderers[i] != null)
                {
                    outlineRenderers[i].enabled = false;
                }
            }

            Destroy(gameObject);
        }

        private LineRenderer EnsureRenderer(int index)
        {
            while (renderers.Count <= index)
            {
                GameObject lineObject = new($"{StrokeLineName}_{renderers.Count:00}");
                lineObject.transform.SetParent(transform, false);
                LineRenderer line = lineObject.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.loop = false;
                line.positionCount = 2;
                line.numCapVertices = 3;
                line.numCornerVertices = 3;
                line.material = GetLineMaterial();
                renderers.Add(line);
            }

            return renderers[index];
        }

        private LineRenderer EnsureOutlineRenderer(int index)
        {
            while (outlineRenderers.Count <= index)
            {
                GameObject lineObject = new($"{StrokeOutlineName}_{outlineRenderers.Count:00}");
                lineObject.transform.SetParent(transform, false);
                LineRenderer line = lineObject.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.loop = false;
                line.positionCount = 2;
                line.numCapVertices = 3;
                line.numCornerVertices = 3;
                line.material = GetLineMaterial();
                line.enabled = false;
                outlineRenderers.Add(line);
            }

            return outlineRenderers[index];
        }

        private void ApplyRendererStyle(LineRenderer line)
        {
            if (line == null || pattern == null)
            {
                return;
            }

            Color color = flashStyle
                ? settings.CompletedFlashColor
                : completedStyle
                    ? settings.CompletedColor
                    : settings.Color;
            color.a *= alphaMultiplier;
            line.startColor = color;
            line.endColor = color;
            line.startWidth = settings.LineWidth;
            line.endWidth = settings.LineWidth;
            line.sortingOrder = settings.SortingOrder;
        }

        private void ApplyOutlineRendererStyle(LineRenderer line)
        {
            if (line == null || pattern == null)
            {
                return;
            }

            Color color = flashStyle ? settings.CompletedFlashColor : settings.CompletedOutlineColor;
            color.a *= alphaMultiplier;
            line.startColor = color;
            line.endColor = color;
            line.startWidth = settings.LineWidth + settings.CompletedOutlineWidth;
            line.endWidth = settings.LineWidth + settings.CompletedOutlineWidth;
            line.sortingOrder = settings.SortingOrder - 1;
        }

        private static void BuildStrokePoints(
            IReadOnlyList<Vector2> sourcePoints,
            float scale,
            float progress,
            List<Vector3> results)
        {
            results.Clear();
            if (sourcePoints == null || sourcePoints.Count < 2 || progress <= 0f)
            {
                return;
            }

            float totalLength = GetPathLength(sourcePoints);
            if (totalLength <= 0.0001f)
            {
                return;
            }

            float visibleLength = totalLength * Mathf.Clamp01(progress);
            float remainingLength = visibleLength;
            results.Add(sourcePoints[0] * scale);

            for (int i = 0; i < sourcePoints.Count - 1; i++)
            {
                Vector2 start = sourcePoints[i];
                Vector2 end = sourcePoints[i + 1];
                float segmentLength = Vector2.Distance(start, end);
                if (segmentLength <= 0.0001f)
                {
                    continue;
                }

                if (remainingLength >= segmentLength)
                {
                    results.Add(end * scale);
                    remainingLength -= segmentLength;
                    continue;
                }

                Vector2 point = Vector2.Lerp(start, end, remainingLength / segmentLength);
                results.Add(point * scale);
                return;
            }
        }

        private static float GetPathLength(IReadOnlyList<Vector2> points)
        {
            float length = 0f;
            for (int i = 0; i < points.Count - 1; i++)
            {
                length += Vector2.Distance(points[i], points[i + 1]);
            }

            return length;
        }

        private static Material GetLineMaterial()
        {
            if (lineMaterial != null)
            {
                return lineMaterial;
            }

            Shader shader = Shader.Find("Sprites/Default");
            lineMaterial = shader != null ? new Material(shader) : null;
            return lineMaterial;
        }
    }
}
