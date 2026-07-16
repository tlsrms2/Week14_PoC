using System.Collections.Generic;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    [AddComponentMenu("Week14/Boss/Conductor Score Lane Rush Indicator Visual")]
    public sealed class ConductorScoreLaneRushIndicatorVisual : MonoBehaviour
    {
        private const string LaneLineName = "ConductorScoreLaneRushIndicatorLine";
        private const string LaneDashName = "ConductorScoreLaneRushIndicatorDash";
        private const int MaxDashSegments = 128;

        private static Material laneMaterial;

        private readonly List<LaneLine> lines = new();

        private Color laneColor = new(0.62f, 0.92f, 1f, 0.66f);
        private float laneWidth = 0.035f;
        private int sortingOrder = 66;
        private float alphaMultiplier = 1f;
        private bool blocksPlayer;
        private float playerBlockingThickness = 0.12f;
        private bool clearOnExecutionCinematic;
        private bool isClearing;

        public int LaneCount => lines.Count;

        public void Configure(Color color, float width, int order)
        {
            laneColor = color;
            laneWidth = Mathf.Max(0.001f, width);
            sortingOrder = order;
        }

        public void ConfigureClearOnExecutionCinematic(bool enabled)
        {
            clearOnExecutionCinematic = enabled;
        }

        public void ConfigurePlayerBlocking(bool enabled, float thickness = 0.12f)
        {
            blocksPlayer = enabled;
            playerBlockingThickness = Mathf.Max(0.01f, thickness);
            for (int i = 0; i < lines.Count; i++)
            {
                RefreshLine(lines[i]);
            }
        }

        private void LateUpdate()
        {
            if (clearOnExecutionCinematic && PlayerCombatController.IsExecutionCinematicActive)
            {
                ClearAndDestroy();
            }
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
            line.IsDashed = false;
            line.TravelProgress = 0f;
            SetProgress(index, 0f);
        }

        public void SetDashedLane(int index, Vector2 start, Vector2 end, float dashLength, float dashGap)
        {
            LaneLine line = EnsureLine(index);
            line.Start = start;
            line.End = end;
            line.IsDashed = true;
            line.DashLength = Mathf.Max(0.01f, dashLength);
            line.DashGap = Mathf.Max(0.001f, dashGap);
            line.TravelProgress = 0f;
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

        public void SetTravelProgress(int index, float progress)
        {
            if (index < 0 || index >= lines.Count)
            {
                return;
            }

            LaneLine line = lines[index];
            line.TravelProgress = Mathf.Clamp01(progress);
            RefreshLine(line);
        }

        private void RefreshLine(LaneLine line)
        {
            if (line == null || line.Renderer == null)
            {
                return;
            }

            if (line.IsDashed)
            {
                RefreshDashedLine(line);
                return;
            }

            DisableDashRenderers(line, 0);
            float startT = line.TravelProgress;
            float endT = line.Progress;
            line.Renderer.enabled = endT > startT && alphaMultiplier > 0f;
            line.Renderer.positionCount = 2;
            line.Renderer.startWidth = laneWidth;
            line.Renderer.endWidth = laneWidth;
            Color color = laneColor;
            color.a *= alphaMultiplier;
            line.Renderer.startColor = color;
            line.Renderer.endColor = color;
            BossSorting.Apply(line.Renderer);
            line.Renderer.sortingOrder = sortingOrder;
            line.Renderer.SetPosition(0, Vector2.Lerp(line.Start, line.End, startT));
            line.Renderer.SetPosition(1, Vector2.Lerp(line.Start, line.End, endT));
            RefreshBarrier(line, startT, endT);
        }

        private void RefreshDashedLine(LaneLine line)
        {
            line.Renderer.enabled = false;

            Vector2 delta = line.End - line.Start;
            float length = delta.magnitude;
            if (length <= 0.01f || line.Progress <= line.TravelProgress || alphaMultiplier <= 0f)
            {
                DisableDashRenderers(line, 0);
                RefreshBarrier(line, 0f, 0f);
                return;
            }

            Vector2 direction = delta / length;
            float visibleStart = Mathf.Clamp01(line.TravelProgress) * length;
            float visibleEnd = Mathf.Clamp01(line.Progress) * length;
            float dashLength = Mathf.Max(0.01f, line.DashLength);
            float dashGap = Mathf.Max(0.001f, line.DashGap);
            float dashStep = dashLength + dashGap;
            int dashCount = Mathf.Min(MaxDashSegments, Mathf.CeilToInt(visibleEnd / dashStep));
            int visibleCount = 0;
            Color color = laneColor;
            color.a *= alphaMultiplier;

            for (int i = 0; i < dashCount; i++)
            {
                float segmentStart = i * dashStep;
                float segmentEnd = Mathf.Min(segmentStart + dashLength, length);
                if (segmentEnd <= visibleStart || segmentStart >= visibleEnd)
                {
                    continue;
                }

                segmentStart = Mathf.Max(segmentStart, visibleStart);
                segmentEnd = Mathf.Min(segmentEnd, visibleEnd);
                LineRenderer dash = EnsureDashRenderer(line, visibleCount++);
                dash.enabled = true;
                dash.positionCount = 2;
                dash.startWidth = laneWidth;
                dash.endWidth = laneWidth;
                dash.startColor = color;
                dash.endColor = color;
                BossSorting.Apply(dash);
                dash.sortingOrder = sortingOrder;
                dash.SetPosition(0, line.Start + direction * segmentStart);
                dash.SetPosition(1, line.Start + direction * segmentEnd);
            }

            DisableDashRenderers(line, visibleCount);
            RefreshBarrier(line, line.TravelProgress, line.Progress);
        }

        public void ClearAndDestroy()
        {
            if (isClearing)
            {
                return;
            }

            isClearing = true;
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Renderer != null)
                {
                    lines[i].Renderer.enabled = false;
                }

                if (lines[i].Barrier != null)
                {
                    lines[i].Barrier.enabled = false;
                }

                DisableDashRenderers(lines[i], 0);
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
                BoxCollider2D barrier = lineObject.AddComponent<BoxCollider2D>();
                barrier.isTrigger = true;
                barrier.enabled = false;
                lineObject.AddComponent<PlayerOnlyMovementBarrier>();

                lines.Add(new LaneLine(renderer, barrier));
            }

            return lines[index];
        }

        private LineRenderer EnsureDashRenderer(LaneLine line, int index)
        {
            while (line.DashRenderers.Count <= index)
            {
                GameObject dashObject = new($"{LaneDashName}_{line.DashRenderers.Count:00}");
                dashObject.transform.SetParent(line.Renderer.transform, false);
                LineRenderer renderer = dashObject.AddComponent<LineRenderer>();
                renderer.useWorldSpace = true;
                renderer.loop = false;
                renderer.positionCount = 2;
                renderer.numCapVertices = 1;
                renderer.numCornerVertices = 0;
                renderer.material = GetLaneMaterial();
                line.DashRenderers.Add(renderer);
            }

            return line.DashRenderers[index];
        }

        private static void DisableDashRenderers(LaneLine line, int startIndex)
        {
            if (line == null)
            {
                return;
            }

            for (int i = Mathf.Max(0, startIndex); i < line.DashRenderers.Count; i++)
            {
                if (line.DashRenderers[i] != null)
                {
                    line.DashRenderers[i].enabled = false;
                }
            }
        }

        private void RefreshBarrier(LaneLine line, float startT, float endT)
        {
            if (line?.Barrier == null)
            {
                return;
            }

            float visibleStart = Mathf.Clamp01(startT);
            float visibleEnd = Mathf.Clamp01(endT);
            Vector2 start = Vector2.Lerp(line.Start, line.End, visibleStart);
            Vector2 end = Vector2.Lerp(line.Start, line.End, visibleEnd);
            Vector2 delta = end - start;
            float length = delta.magnitude;
            bool active = blocksPlayer && alphaMultiplier > 0f && length > 0.01f;
            line.Barrier.enabled = active;
            if (!active)
            {
                return;
            }

            Transform barrierTransform = line.Barrier.transform;
            barrierTransform.position = (start + end) * 0.5f;
            barrierTransform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            line.Barrier.offset = Vector2.zero;
            line.Barrier.size = new Vector2(length, Mathf.Max(playerBlockingThickness, laneWidth));
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
            public LaneLine(LineRenderer renderer, BoxCollider2D barrier)
            {
                Renderer = renderer;
                Barrier = barrier;
            }

            public LineRenderer Renderer { get; }
            public BoxCollider2D Barrier { get; }
            public List<LineRenderer> DashRenderers { get; } = new();
            public Vector2 Start { get; set; }
            public Vector2 End { get; set; }
            public float Progress { get; set; }
            public float TravelProgress { get; set; }
            public bool IsDashed { get; set; }
            public float DashLength { get; set; } = 0.22f;
            public float DashGap { get; set; } = 0.16f;
        }
    }
}
