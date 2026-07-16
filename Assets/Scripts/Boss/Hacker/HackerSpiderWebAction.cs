using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class HackerSpiderWebAction : BossAction, IBossActionDurationProvider
    {
        [SerializeField, BossGraphBossChildPath] private string webCenterPath;
        [SerializeField] private string animationTriggerName = "SpiderWeb";
        [SerializeField, Min(0f)] private float windupSeconds = 0.4f;
        [SerializeField, Min(3)] private int spokeCount = 8;
        [SerializeField, Min(0.5f)] private float webRadius = 6f;
        [SerializeField, Min(0.05f)] private float centerSafeRadius = 0.75f;
        [SerializeField, Min(1)] private int ringCount = 3;
        [SerializeField, Min(0.05f)] private float ringTelegraphSeconds = 0.4f;
        [SerializeField, Min(0.01f)] private float cellExplosionSeconds = 0.22f;
        [SerializeField, Min(0f)] private float ringBuildInterval = 0.2f;
        [SerializeField, Min(1)] private int explosionDamage = 1;
        [SerializeField, Min(0.1f)] private float activeSeconds = 10f;
        [SerializeField, Min(0.05f)] private float periodicExplosionInterval = 1f;
        [SerializeField, Min(0.05f)] private float periodicTelegraphSeconds = 0.35f;
        [SerializeField, Min(1)] private int periodicExplosionCount = 2;
        [SerializeField] private Color wireColor = new(0.5f, 0.8f, 1f, 0.8f);
        [SerializeField] private Color indicatorColor = new(1f, 0.3f, 0.08f, 0.32f);
        [SerializeField] private Color explosionColor = new(0.95f, 0.25f, 0.08f, 0.95f);
        [SerializeField] private int wireSortingOrder = -9;
        [SerializeField] private int indicatorSortingOrder = -9;
        [SerializeField, Min(0f)] private float recoverySeconds = 0.25f;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context?.Boss == null)
            {
                yield break;
            }

            context.PlayAnimationTrigger(animationTriggerName);
            yield return HackerMeleeAttackAction.Wait(context, windupSeconds);

            HackerSpiderWebHazard.Create(
                context.GetBossChildPosition(webCenterPath),
                spokeCount,
                webRadius,
                centerSafeRadius,
                ringCount,
                ringTelegraphSeconds,
                cellExplosionSeconds,
                ringBuildInterval,
                explosionDamage,
                activeSeconds,
                periodicExplosionInterval,
                periodicTelegraphSeconds,
                periodicExplosionCount,
                wireColor,
                indicatorColor,
                explosionColor,
                wireSortingOrder,
                indicatorSortingOrder);

            yield return HackerMeleeAttackAction.Wait(context, GetBuildSeconds());
            yield return HackerMeleeAttackAction.Wait(context, recoverySeconds);
        }

        public bool TryGetDurationSeconds(out float seconds)
        {
            seconds = Mathf.Max(0f, windupSeconds) + GetBuildSeconds() + Mathf.Max(0f, recoverySeconds);
            return true;
        }

        private float GetBuildSeconds()
        {
            int count = Mathf.Max(1, ringCount);
            return count * (Mathf.Max(0.05f, ringTelegraphSeconds) + Mathf.Max(0.01f, cellExplosionSeconds))
                + Mathf.Max(0, count - 1) * Mathf.Max(0f, ringBuildInterval);
        }
    }

    internal sealed class HackerSpiderWebHazard : MonoBehaviour
    {
        private readonly List<LineRenderer> wireLines = new();
        private readonly List<HackerSpiderWebCellIndicator> indicators = new();
        private readonly List<HackerSpiderWebCell> pendingCells = new();

        private int spokeCount;
        private int ringCount;
        private float webRadius;
        private float centerSafeRadius;
        private float ringTelegraphSeconds;
        private float cellExplosionSeconds;
        private float ringBuildInterval;
        private int explosionDamage;
        private float activeSeconds;
        private float periodicExplosionInterval;
        private float periodicTelegraphSeconds;
        private int periodicExplosionCount;
        private Color wireColor;
        private Color indicatorColor;
        private Color explosionColor;
        private int wireSortingOrder;
        private int indicatorSortingOrder;
        private int nextRingIndex;
        private int pendingRingIndex = -1;
        private float elapsed;
        private float nextRingTelegraphAt;
        private float pendingExplosionAt;
        private float pendingRingWireRadius;
        private float pendingRingWireRevealAt = -1f;
        private float nextPeriodicTelegraphAt;
        private bool hasPendingPeriodicExplosion;
        private bool isComplete;

        internal static void Create(
            Vector3 center,
            int nextSpokeCount,
            float nextWebRadius,
            float nextCenterSafeRadius,
            int nextRingCount,
            float nextRingTelegraphSeconds,
            float nextCellExplosionSeconds,
            float nextRingBuildInterval,
            int nextExplosionDamage,
            float nextActiveSeconds,
            float nextPeriodicExplosionInterval,
            float nextPeriodicTelegraphSeconds,
            int nextPeriodicExplosionCount,
            Color nextWireColor,
            Color nextIndicatorColor,
            Color nextExplosionColor,
            int nextWireSortingOrder,
            int nextIndicatorSortingOrder)
        {
            GameObject hazardObject = new("HackerSpiderWebHazard");
            hazardObject.transform.position = center;
            HackerSpiderWebHazard hazard = hazardObject.AddComponent<HackerSpiderWebHazard>();
            hazard.Initialize(
                nextSpokeCount,
                nextWebRadius,
                nextCenterSafeRadius,
                nextRingCount,
                nextRingTelegraphSeconds,
                nextCellExplosionSeconds,
                nextRingBuildInterval,
                nextExplosionDamage,
                nextActiveSeconds,
                nextPeriodicExplosionInterval,
                nextPeriodicTelegraphSeconds,
                nextPeriodicExplosionCount,
                nextWireColor,
                nextIndicatorColor,
                nextExplosionColor,
                nextWireSortingOrder,
                nextIndicatorSortingOrder);
        }

        private void Initialize(
            int nextSpokeCount,
            float nextWebRadius,
            float nextCenterSafeRadius,
            int nextRingCount,
            float nextRingTelegraphSeconds,
            float nextCellExplosionSeconds,
            float nextRingBuildInterval,
            int nextExplosionDamage,
            float nextActiveSeconds,
            float nextPeriodicExplosionInterval,
            float nextPeriodicTelegraphSeconds,
            int nextPeriodicExplosionCount,
            Color nextWireColor,
            Color nextIndicatorColor,
            Color nextExplosionColor,
            int nextWireSortingOrder,
            int nextIndicatorSortingOrder)
        {
            spokeCount = Mathf.Max(3, nextSpokeCount);
            webRadius = Mathf.Max(0.5f, nextWebRadius);
            centerSafeRadius = Mathf.Clamp(nextCenterSafeRadius, 0.05f, webRadius - 0.01f);
            ringCount = Mathf.Max(1, nextRingCount);
            ringTelegraphSeconds = Mathf.Max(0.05f, nextRingTelegraphSeconds);
            cellExplosionSeconds = Mathf.Max(0.01f, nextCellExplosionSeconds);
            ringBuildInterval = Mathf.Max(0f, nextRingBuildInterval);
            explosionDamage = Mathf.Max(1, nextExplosionDamage);
            periodicExplosionInterval = Mathf.Max(0.05f, nextPeriodicExplosionInterval);
            periodicTelegraphSeconds = Mathf.Max(0.05f, nextPeriodicTelegraphSeconds);
            periodicExplosionCount = Mathf.Max(1, nextPeriodicExplosionCount);
            wireColor = nextWireColor;
            indicatorColor = nextIndicatorColor;
            explosionColor = nextExplosionColor;
            wireSortingOrder = nextWireSortingOrder;
            indicatorSortingOrder = nextIndicatorSortingOrder;
            activeSeconds = Mathf.Max(GetBuildSeconds() + 0.1f, nextActiveSeconds);

            DrawSpokes();
            DrawRing(centerSafeRadius);
        }

        private void Update()
        {
            elapsed += EnemyTimeScale.DeltaTime;
            if (!isComplete)
            {
                TickBuild();
            }
            else
            {
                TickPeriodicExplosions();
            }

            if (elapsed >= activeSeconds)
            {
                Destroy(gameObject);
            }
        }

        private void TickBuild()
        {
            if (pendingRingWireRevealAt >= 0f)
            {
                if (elapsed >= pendingRingWireRevealAt)
                {
                    RevealRingWire();
                }

                return;
            }

            if (pendingRingIndex >= 0)
            {
                if (elapsed >= pendingExplosionAt)
                {
                    CompleteRing();
                }

                return;
            }

            if (nextRingIndex >= ringCount)
            {
                isComplete = true;
                nextPeriodicTelegraphAt = elapsed + periodicExplosionInterval;
                return;
            }

            if (elapsed >= nextRingTelegraphAt)
            {
                BeginRingTelegraph(nextRingIndex);
            }
        }

        private void BeginRingTelegraph(int ringIndex)
        {
            pendingCells.Clear();
            pendingCells.AddRange(GetRingCells(ringIndex));
            ShowIndicators(pendingCells);
            pendingRingIndex = ringIndex;
            pendingExplosionAt = elapsed + ringTelegraphSeconds;
        }

        private void CompleteRing()
        {
            TriggerCells(pendingCells);
            ClearIndicators();
            pendingRingWireRadius = GetRingOuterRadius(pendingRingIndex);
            pendingRingWireRevealAt = elapsed + cellExplosionSeconds;
        }

        private void RevealRingWire()
        {
            DrawRing(pendingRingWireRadius);
            pendingCells.Clear();
            pendingRingIndex = -1;
            pendingRingWireRevealAt = -1f;
            nextRingIndex++;
            nextRingTelegraphAt = elapsed + ringBuildInterval;
        }

        private void TickPeriodicExplosions()
        {
            if (hasPendingPeriodicExplosion)
            {
                if (elapsed >= pendingExplosionAt)
                {
                    TriggerCells(pendingCells);
                    ClearIndicators();
                    pendingCells.Clear();
                    hasPendingPeriodicExplosion = false;
                    nextPeriodicTelegraphAt = elapsed + periodicExplosionInterval;
                }

                return;
            }

            if (elapsed < nextPeriodicTelegraphAt)
            {
                return;
            }

            pendingCells.Clear();
            SelectRandomCells(pendingCells, periodicExplosionCount);
            ShowIndicators(pendingCells);
            hasPendingPeriodicExplosion = pendingCells.Count > 0;
            pendingExplosionAt = elapsed + periodicTelegraphSeconds;
        }

        private void TriggerCells(List<HackerSpiderWebCell> cells)
        {
            PlayerCombatController player = PlayerCombatController.Active;
            bool playerDamaged = false;
            for (int i = 0; i < cells.Count; i++)
            {
                HackerSpiderWebCell cell = cells[i];
                HackerSpiderWebCellExplosionVisual.Create(cell, explosionColor, cellExplosionSeconds, indicatorSortingOrder);
                if (playerDamaged || player == null || !cell.Contains(player.transform.position))
                {
                    continue;
                }

                Vector2 direction = ((Vector2)player.transform.position - (Vector2)cell.Center).normalized;
                player.ReceiveAttack(explosionDamage, player.transform.position, direction);
                playerDamaged = true;
            }
        }

        private void SelectRandomCells(List<HackerSpiderWebCell> selectedCells, int count)
        {
            List<HackerSpiderWebCell> candidates = new(spokeCount * ringCount);
            for (int ringIndex = 0; ringIndex < ringCount; ringIndex++)
            {
                candidates.AddRange(GetRingCells(ringIndex));
            }

            int selectionCount = Mathf.Min(Mathf.Max(1, count), candidates.Count);
            for (int i = 0; i < selectionCount; i++)
            {
                int index = UnityEngine.Random.Range(0, candidates.Count);
                selectedCells.Add(candidates[index]);
                candidates.RemoveAt(index);
            }
        }

        private List<HackerSpiderWebCell> GetRingCells(int ringIndex)
        {
            List<HackerSpiderWebCell> cells = new(spokeCount);
            float innerRadius = GetRingInnerRadius(ringIndex);
            float outerRadius = GetRingOuterRadius(ringIndex);
            for (int spokeIndex = 0; spokeIndex < spokeCount; spokeIndex++)
            {
                float startAngle = Mathf.PI * 2f * spokeIndex / spokeCount;
                float endAngle = Mathf.PI * 2f * (spokeIndex + 1) / spokeCount;
                cells.Add(new HackerSpiderWebCell(
                    GetPoint(startAngle, innerRadius),
                    GetPoint(endAngle, innerRadius),
                    GetPoint(endAngle, outerRadius),
                    GetPoint(startAngle, outerRadius)));
            }

            return cells;
        }

        private float GetRingInnerRadius(int ringIndex)
        {
            return Mathf.Lerp(centerSafeRadius, webRadius, Mathf.Clamp01((float)ringIndex / ringCount));
        }

        private float GetRingOuterRadius(int ringIndex)
        {
            return Mathf.Lerp(centerSafeRadius, webRadius, Mathf.Clamp01((float)(ringIndex + 1) / ringCount));
        }

        private void ShowIndicators(List<HackerSpiderWebCell> cells)
        {
            ClearIndicators();
            for (int i = 0; i < cells.Count; i++)
            {
                indicators.Add(HackerSpiderWebCellIndicator.Create(cells[i], indicatorColor, indicatorSortingOrder));
            }
        }

        private void ClearIndicators()
        {
            for (int i = 0; i < indicators.Count; i++)
            {
                HackerSpiderWebCellIndicator.Destroy(indicators[i]);
            }

            indicators.Clear();
        }

        private void DrawSpokes()
        {
            for (int i = 0; i < spokeCount; i++)
            {
                LineRenderer line = CreateWireLine();
                line.positionCount = 2;
                line.SetPosition(0, transform.position);
                line.SetPosition(1, GetPoint(Mathf.PI * 2f * i / spokeCount, webRadius));
            }
        }

        private void DrawRing(float radius)
        {
            LineRenderer ring = CreateWireLine();
            ring.loop = true;
            ring.positionCount = spokeCount;
            for (int i = 0; i < spokeCount; i++)
            {
                ring.SetPosition(i, GetPoint(Mathf.PI * 2f * i / spokeCount, radius));
            }
        }

        private LineRenderer CreateWireLine()
        {
            GameObject lineObject = new("HackerSpiderWebLine");
            lineObject.transform.SetParent(transform, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.startWidth = 0.035f;
            line.endWidth = 0.035f;
            line.startColor = wireColor;
            line.endColor = wireColor;
            BossSorting.Apply(line);
            line.sortingOrder = wireSortingOrder;
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                line.material = new Material(shader);
            }

            wireLines.Add(line);
            return line;
        }

        private Vector3 GetPoint(float angle, float radius)
        {
            return transform.position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        private float GetBuildSeconds()
        {
            return ringCount * (ringTelegraphSeconds + cellExplosionSeconds)
                + Mathf.Max(0, ringCount - 1) * ringBuildInterval;
        }

        private void OnDestroy()
        {
            ClearIndicators();
            for (int i = 0; i < wireLines.Count; i++)
            {
                LineRenderer line = wireLines[i];
                if (line != null && line.material != null)
                {
                    Destroy(line.material);
                }
            }
        }
    }

    internal readonly struct HackerSpiderWebCell
    {
        internal readonly Vector3 InnerStart;
        internal readonly Vector3 InnerEnd;
        internal readonly Vector3 OuterEnd;
        internal readonly Vector3 OuterStart;

        internal Vector3 Center => (InnerStart + InnerEnd + OuterEnd + OuterStart) * 0.25f;

        internal HackerSpiderWebCell(Vector3 innerStart, Vector3 innerEnd, Vector3 outerEnd, Vector3 outerStart)
        {
            InnerStart = innerStart;
            InnerEnd = innerEnd;
            OuterEnd = outerEnd;
            OuterStart = outerStart;
        }

        internal Vector3 GetCorner(int index)
        {
            return index switch
            {
                0 => InnerStart,
                1 => InnerEnd,
                2 => OuterEnd,
                _ => OuterStart
            };
        }

        internal bool Contains(Vector2 point)
        {
            bool hasPositive = false;
            bool hasNegative = false;
            for (int i = 0; i < 4; i++)
            {
                Vector2 from = GetCorner(i);
                Vector2 to = GetCorner((i + 1) % 4);
                float cross = (to.x - from.x) * (point.y - from.y) - (to.y - from.y) * (point.x - from.x);
                hasPositive |= cross > 0.0001f;
                hasNegative |= cross < -0.0001f;
                if (hasPositive && hasNegative)
                {
                    return false;
                }
            }

            return true;
        }
    }

    internal sealed class HackerSpiderWebCellIndicator : MonoBehaviour
    {
        private Material lineMaterial;
        private Material fillMaterial;
        private Mesh fillMesh;

        internal static HackerSpiderWebCellIndicator Create(HackerSpiderWebCell cell, Color color, int sortingOrder)
        {
            GameObject indicatorObject = new("HackerSpiderWebCellIndicator");
            HackerSpiderWebCellIndicator indicator = indicatorObject.AddComponent<HackerSpiderWebCellIndicator>();
            indicator.Initialize(cell, color, sortingOrder);
            return indicator;
        }

        internal static void Destroy(HackerSpiderWebCellIndicator indicator)
        {
            if (indicator != null)
            {
                UnityEngine.Object.Destroy(indicator.gameObject);
            }
        }

        private void Initialize(HackerSpiderWebCell cell, Color color, int sortingOrder)
        {
            LineRenderer line = gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 5;
            line.startWidth = 0.045f;
            line.endWidth = 0.045f;
            line.startColor = color;
            line.endColor = color;
            BossSorting.Apply(line);
            line.sortingOrder = sortingOrder;
            for (int i = 0; i < 5; i++)
            {
                line.SetPosition(i, cell.GetCorner(i % 4));
            }

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                return;
            }

            lineMaterial = new Material(shader);
            line.material = lineMaterial;
            MeshFilter meshFilter = gameObject.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
            fillMesh = new Mesh();
            fillMesh.vertices = new[]
            {
                cell.InnerStart,
                cell.InnerEnd,
                cell.OuterEnd,
                cell.OuterStart
            };
            fillMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            fillMesh.RecalculateBounds();
            meshFilter.sharedMesh = fillMesh;
            fillMaterial = new Material(shader) { color = color };
            meshRenderer.sharedMaterial = fillMaterial;
            BossSorting.Apply(meshRenderer);
            meshRenderer.sortingOrder = sortingOrder;
        }

        private void OnDestroy()
        {
            if (lineMaterial != null)
            {
                Destroy(lineMaterial);
            }

            if (fillMaterial != null)
            {
                Destroy(fillMaterial);
            }

            if (fillMesh != null)
            {
                Destroy(fillMesh);
            }
        }
    }

    internal sealed class HackerSpiderWebCellExplosionVisual : MonoBehaviour
    {
        private float duration;
        private float elapsed;
        private Material lineMaterial;

        internal static void Create(HackerSpiderWebCell cell, Color color, float durationSeconds, int sortingOrder)
        {
            GameObject visualObject = new("HackerSpiderWebCellExplosion");
            HackerSpiderWebCellExplosionVisual visual = visualObject.AddComponent<HackerSpiderWebCellExplosionVisual>();
            visual.duration = Mathf.Max(0.01f, durationSeconds);
            visual.CreateLine(cell, color, sortingOrder);
        }

        private void Update()
        {
            elapsed += EnemyTimeScale.DeltaTime;
            if (elapsed >= duration)
            {
                Destroy(gameObject);
            }
        }

        private void CreateLine(HackerSpiderWebCell cell, Color color, int sortingOrder)
        {
            LineRenderer line = gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 5;
            line.startWidth = 0.09f;
            line.endWidth = 0.09f;
            line.startColor = color;
            line.endColor = color;
            BossSorting.Apply(line);
            line.sortingOrder = sortingOrder;
            for (int i = 0; i < 5; i++)
            {
                line.SetPosition(i, cell.GetCorner(i % 4));
            }

            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                lineMaterial = new Material(shader);
                line.material = lineMaterial;
            }
        }

        private void OnDestroy()
        {
            if (lineMaterial != null)
            {
                Destroy(lineMaterial);
            }
        }
    }
}
