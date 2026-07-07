using System.Collections.Generic;
using UnityEngine;

namespace Week14.Enemy
{
    [AddComponentMenu("Week14/Boss/Conductor Score Lane Rush Indicator Visual")]
    public sealed class ConductorScoreLaneRushIndicatorVisual : MonoBehaviour
    {
        private const string LaneLineName = "ConductorScoreLaneRushIndicatorLine";

        private static Material laneMaterial;

        private readonly List<LaneLine> lines = new();

        private Color laneColor = new(0.62f, 0.92f, 1f, 0.66f);
        private float laneWidth = 0.035f;
        private int sortingOrder = 66;
        private float alphaMultiplier = 1f;

        public int LaneCount => lines.Count;

        public void Configure(Color color, float width, int order)
        {
            laneColor = color;
            laneWidth = Mathf.Max(0.001f, width);
            sortingOrder = order;
        }

        public void SetAlpha(float alpha)
        {
            alphaMultiplier = Mathf.Clamp01(alpha);
            for (int i = 0; i < lines.Count; i++)
            {
                RefreshLine(lines[i]);
            }
        }

        public void SetLane(int index, Vector2 start, Vector2 end)
        {
            LaneLine line = EnsureLine(index);
            line.Start = start;
            line.End = end;
            SetProgress(index, 0f);
        }

        public void SetProgress(int index, float progress)
        {
            if (index < 0 || index >= lines.Count)
            {
                return;
            }

            LaneLine line = lines[index];
            line.Progress = Mathf.Clamp01(progress);
            RefreshLine(line);
        }

        private void RefreshLine(LaneLine line)
        {
            if (line == null || line.Renderer == null)
            {
                return;
            }

            float t = line.Progress;
            line.Renderer.enabled = t > 0f && alphaMultiplier > 0f;
            line.Renderer.positionCount = 2;
            line.Renderer.startWidth = laneWidth;
            line.Renderer.endWidth = laneWidth;
            Color color = laneColor;
            color.a *= alphaMultiplier;
            line.Renderer.startColor = color;
            line.Renderer.endColor = color;
            line.Renderer.sortingOrder = sortingOrder;
            line.Renderer.SetPosition(0, line.Start);
            line.Renderer.SetPosition(1, Vector2.Lerp(line.Start, line.End, t));
        }

        public void ClearAndDestroy()
        {
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Renderer != null)
                {
                    lines[i].Renderer.enabled = false;
                }
            }

            Destroy(gameObject);
        }

        private LaneLine EnsureLine(int index)
        {
            while (lines.Count <= index)
            {
                GameObject lineObject = new($"{LaneLineName}_{lines.Count:00}");
                lineObject.transform.SetParent(transform, false);
                LineRenderer renderer = lineObject.AddComponent<LineRenderer>();
                renderer.useWorldSpace = true;
                renderer.loop = false;
                renderer.positionCount = 2;
                renderer.numCapVertices = 3;
                renderer.numCornerVertices = 2;
                renderer.material = GetLaneMaterial();
                lines.Add(new LaneLine(renderer));
            }

            return lines[index];
        }

        private static Material GetLaneMaterial()
        {
            if (laneMaterial != null)
            {
                return laneMaterial;
            }

            Shader shader = Shader.Find("Sprites/Default");
            laneMaterial = shader != null ? new Material(shader) : null;
            return laneMaterial;
        }

        private sealed class LaneLine
        {
            public LaneLine(LineRenderer renderer)
            {
                Renderer = renderer;
            }

            public LineRenderer Renderer { get; }
            public Vector2 Start { get; set; }
            public Vector2 End { get; set; }
            public float Progress { get; set; }
        }
    }
}
