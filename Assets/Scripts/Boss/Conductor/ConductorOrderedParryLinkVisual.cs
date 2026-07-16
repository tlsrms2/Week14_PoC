using System.Collections.Generic;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    [AddComponentMenu("Week14/Boss/Conductor Ordered Parry Link Visual")]
    public sealed class ConductorOrderedParryLinkVisual : MonoBehaviour
    {
        public readonly struct ProjectilePair
        {
            public ProjectilePair(EnemyProjectile from, EnemyProjectile to)
            {
                From = from;
                To = to;
            }

            public EnemyProjectile From { get; }
            public EnemyProjectile To { get; }
        }

        private const string LinkLineName = "ConductorOrderedParryTieLine";

        private static Material linkMaterial;

        private readonly List<LinkLine> lines = new();

        private Color linkColor = new(0.62f, 0.92f, 1f, 0.78f);
        private float linkWidth = 0.04f;
        private float arcHeight = 0.32f;
        private int segments = 8;
        private int sortingOrder = 68;

        public void Configure(Color color, float width, float arc, int segmentCount, int order)
        {
            linkColor = color;
            linkWidth = Mathf.Max(0.001f, width);
            arcHeight = Mathf.Max(0f, arc);
            segments = Mathf.Clamp(segmentCount, 3, 24);
            sortingOrder = order;
        }

        public void SetPairs(IReadOnlyList<ProjectilePair> pairs)
        {
            int requiredCount = pairs != null ? pairs.Count : 0;
            for (int i = 0; i < requiredCount; i++)
            {
                LinkLine line = EnsureLine(i);
                line.From = pairs[i].From;
                line.To = pairs[i].To;
                line.Renderer.enabled = line.From != null && line.To != null;
            }

            for (int i = requiredCount; i < lines.Count; i++)
            {
                lines[i].Clear();
            }
        }

        public void ClearAndDestroy()
        {
            for (int i = 0; i < lines.Count; i++)
            {
                lines[i].Clear();
            }

            Destroy(gameObject);
        }

        private void LateUpdate()
        {
            for (int i = 0; i < lines.Count; i++)
            {
                UpdateLine(lines[i]);
            }
        }

        private LinkLine EnsureLine(int index)
        {
            while (lines.Count <= index)
            {
                GameObject lineObject = new($"{LinkLineName}_{lines.Count:00}");
                lineObject.transform.SetParent(transform, false);
                LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();
                lineRenderer.useWorldSpace = true;
                lineRenderer.loop = false;
                lineRenderer.numCapVertices = 3;
                lineRenderer.numCornerVertices = 3;
                lineRenderer.material = GetLinkMaterial();
                lines.Add(new LinkLine(lineRenderer));
            }

            return lines[index];
        }

        private void UpdateLine(LinkLine line)
        {
            if (line == null || line.Renderer == null || line.From == null || line.To == null)
            {
                if (line?.Renderer != null)
                {
                    line.Renderer.enabled = false;
                }

                return;
            }

            Vector2 start = line.From.transform.position;
            Vector2 end = line.To.transform.position;
            Vector2 delta = end - start;
            if (delta.sqrMagnitude <= 0.0001f)
            {
                line.Renderer.enabled = false;
                return;
            }

            Vector2 direction = delta.normalized;
            Vector2 normal = new(-direction.y, direction.x);
            if (normal.y < 0f)
            {
                normal = -normal;
            }

            Vector2 control = (start + end) * 0.5f + normal * arcHeight;
            line.Renderer.enabled = true;
            line.Renderer.positionCount = segments;
            line.Renderer.startWidth = linkWidth;
            line.Renderer.endWidth = linkWidth;
            line.Renderer.startColor = linkColor;
            line.Renderer.endColor = linkColor;
            BossSorting.Apply(line.Renderer);
            line.Renderer.sortingOrder = sortingOrder;

            for (int i = 0; i < segments; i++)
            {
                float t = segments <= 1 ? 1f : i / (segments - 1f);
                Vector2 point = EvaluateQuadratic(start, control, end, t);
                line.Renderer.SetPosition(i, new Vector3(point.x, point.y, 0f));
            }
        }

        private static Vector2 EvaluateQuadratic(Vector2 start, Vector2 control, Vector2 end, float t)
        {
            float inverse = 1f - t;
            return inverse * inverse * start + 2f * inverse * t * control + t * t * end;
        }

        private static Material GetLinkMaterial()
        {
            if (linkMaterial != null)
            {
                return linkMaterial;
            }

            Shader shader = Shader.Find("Sprites/Default");
            linkMaterial = shader != null ? new Material(shader) : null;
            return linkMaterial;
        }

        private sealed class LinkLine
        {
            public LinkLine(LineRenderer renderer)
            {
                Renderer = renderer;
            }

            public LineRenderer Renderer { get; }
            public EnemyProjectile From { get; set; }
            public EnemyProjectile To { get; set; }

            public void Clear()
            {
                From = null;
                To = null;
                if (Renderer != null)
                {
                    Renderer.enabled = false;
                }
            }
        }
    }
}
