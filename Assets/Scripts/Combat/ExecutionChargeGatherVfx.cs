using System.Collections.Generic;
using UnityEngine;

namespace Week14.Combat
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Week14/Combat/Execution Charge Gather VFX")]
    public sealed class ExecutionChargeGatherVfx : MonoBehaviour
    {
        private sealed class GatherLine
        {
            internal GameObject GameObject;
            internal LineRenderer Renderer;
            internal Vector2 StartOffset;
            internal float Length;
            internal float SpawnedAt;
            internal bool Active;
        }

        [Header("Appearance")]
        [SerializeField] private Material lineMaterial;
        [SerializeField] private Color gatheredColor = new(1f, 0.2f, 0.06f, 1f);
        [SerializeField, Min(0.01f)] private float lineWidth = 0.065f;
        [SerializeField] private string sortingLayerName = "Default";
        [SerializeField] private int sortingOrder = 75;
        [SerializeField] private bool sortAboveMuzzleOwner = true;
        [SerializeField, Min(1)] private int ownerSortingOrderOffset = 10;

        [Header("Muzzle Circle")]
        [SerializeField] private bool showMuzzleCircle = true;
        [SerializeField, Min(0.001f)] private float startCircleRadius = 0.025f;
        [SerializeField, Min(0.01f)] private float maxCircleRadius = 0.48f;
        [SerializeField, Min(0.01f)] private float circleLineWidth = 0.085f;
        [SerializeField, Range(8, 64)] private int circleSegments = 40;
        [SerializeField] private AnimationCurve circleGrowthCurve =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Emission")]
        [SerializeField, Range(1, 48)] private int lineCount = 18;
        [SerializeField, Min(0.05f)] private float emissionSeconds = 0.8f;
        [SerializeField, Min(0.05f)] private float travelSeconds = 0.48f;
        [SerializeField, Min(0.01f)] private float minSpawnDistance = 0.45f;
        [SerializeField, Min(0.01f)] private float maxSpawnDistance = 1.8f;
        [SerializeField, Min(0.01f)] private float minLineLength = 0.22f;
        [SerializeField, Min(0.01f)] private float maxLineLength = 0.68f;
        [SerializeField, Range(0.05f, 1f)] private float endLengthMultiplier = 0.35f;
        [SerializeField] private AnimationCurve movementCurve =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private readonly List<GatherLine> lines = new();
        private Transform muzzle;
        private Material runtimeMaterial;
        private GameObject muzzleCircleObject;
        private LineRenderer muzzleCircle;
        private int runtimeSortingLayerId;
        private int runtimeSortingOrder;
        private float startedAt;
        private int spawnedCount;
        private bool emitting;

        private void Update()
        {
            if (muzzle == null)
            {
                Stop(true);
                return;
            }

            float now = Time.unscaledTime;
            SpawnScheduledLines(now);
            UpdateMuzzleCircle(now);
            UpdateLines(now);
        }

        private void OnDisable()
        {
            Stop(true);
        }

        private void OnDestroy()
        {
            if (runtimeMaterial != null)
            {
                Destroy(runtimeMaterial);
                runtimeMaterial = null;
            }
        }

        public void Play(Transform targetMuzzle)
        {
            Stop(true);
            if (targetMuzzle == null)
            {
                return;
            }

            muzzle = targetMuzzle;
            ResolveRuntimeSorting(targetMuzzle);
            ApplyRuntimeSorting();
            startedAt = Time.unscaledTime;
            spawnedCount = 0;
            emitting = true;
            EnsureMuzzleCircle();
            SetMuzzleCircleActive(showMuzzleCircle);
            SpawnLine(startedAt);
        }

        public void Stop(bool clearImmediately)
        {
            emitting = false;
            muzzle = clearImmediately ? null : muzzle;
            if (!clearImmediately)
            {
                return;
            }

            for (int i = 0; i < lines.Count; i++)
            {
                SetLineActive(lines[i], false);
            }

            SetMuzzleCircleActive(false);
        }

        private void SpawnScheduledLines(float now)
        {
            if (!emitting)
            {
                return;
            }

            int count = Mathf.Max(1, lineCount);
            float interval = count <= 1
                ? 0f
                : Mathf.Max(0.05f, emissionSeconds) / (count - 1);
            while (spawnedCount < count
                && now >= startedAt + spawnedCount * interval)
            {
                SpawnLine(now);
            }

            if (spawnedCount >= count)
            {
                emitting = false;
            }
        }

        private void SpawnLine(float now)
        {
            GatherLine line = GetAvailableLine();
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float minDistance = Mathf.Min(minSpawnDistance, maxSpawnDistance);
            float maxDistance = Mathf.Max(minSpawnDistance, maxSpawnDistance);
            float minLength = Mathf.Min(minLineLength, maxLineLength);
            float maxLength = Mathf.Max(minLineLength, maxLineLength);
            line.StartOffset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle))
                * Random.Range(minDistance, maxDistance);
            line.Length = Random.Range(minLength, maxLength);
            line.SpawnedAt = now;
            SetLineActive(line, true);
            ApplyLinePose(line, 0f);
            spawnedCount++;
        }

        private void UpdateLines(float now)
        {
            bool hasActiveLine = false;
            for (int i = 0; i < lines.Count; i++)
            {
                GatherLine line = lines[i];
                if (!line.Active)
                {
                    continue;
                }

                float progress = Mathf.Clamp01(
                    (now - line.SpawnedAt) / Mathf.Max(0.05f, travelSeconds));
                ApplyLinePose(line, progress);
                if (progress >= 1f)
                {
                    SetLineActive(line, false);
                }
                else
                {
                    hasActiveLine = true;
                }
            }

            if (!emitting && !hasActiveLine)
            {
                SetMuzzleCircleActive(false);
                muzzle = null;
            }
        }

        private void UpdateMuzzleCircle(float now)
        {
            if (!showMuzzleCircle || muzzleCircle == null || muzzle == null)
            {
                return;
            }

            float progress = Mathf.Clamp01(
                (now - startedAt) / Mathf.Max(0.05f, emissionSeconds));
            float growth = circleGrowthCurve != null
                ? circleGrowthCurve.Evaluate(progress)
                : progress;
            float radius = Mathf.Lerp(
                startCircleRadius,
                Mathf.Max(startCircleRadius, maxCircleRadius),
                growth);
            muzzleCircleObject.transform.position = muzzle.position;
            int segmentCount = muzzleCircle.positionCount;
            for (int i = 0; i < segmentCount; i++)
            {
                float angle = Mathf.PI * 2f * i / segmentCount;
                muzzleCircle.SetPosition(
                    i,
                    new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
            }

            Color color = Color.Lerp(Color.white, gatheredColor, progress);
            muzzleCircle.startColor = color;
            muzzleCircle.endColor = color;
        }

        private void ApplyLinePose(GatherLine line, float progress)
        {
            if (line?.GameObject == null || muzzle == null)
            {
                return;
            }

            float moveProgress = movementCurve != null
                ? movementCurve.Evaluate(progress)
                : progress;
            Vector3 targetPosition = muzzle.position;
            Vector3 centerPosition =
                targetPosition + (Vector3)(line.StartOffset * (1f - moveProgress));
            Vector2 inwardDirection = -line.StartOffset;
            if (inwardDirection.sqrMagnitude <= 0.0001f)
            {
                inwardDirection = Vector2.right;
            }

            inwardDirection.Normalize();
            line.GameObject.transform.SetPositionAndRotation(
                centerPosition,
                Quaternion.Euler(
                    0f,
                    0f,
                    Mathf.Atan2(inwardDirection.y, inwardDirection.x) * Mathf.Rad2Deg));

            float currentLength = line.Length
                * Mathf.Lerp(1f, endLengthMultiplier, progress);
            float halfLength = currentLength * 0.5f;
            line.Renderer.SetPosition(0, new Vector3(-halfLength, 0f, 0f));
            line.Renderer.SetPosition(1, new Vector3(halfLength, 0f, 0f));

            Color color = Color.Lerp(Color.white, gatheredColor, progress);
            if (progress > 0.9f)
            {
                color.a *= 1f - Mathf.InverseLerp(0.9f, 1f, progress);
            }

            line.Renderer.startColor = color;
            line.Renderer.endColor = color;
        }

        private GatherLine GetAvailableLine()
        {
            for (int i = 0; i < lines.Count; i++)
            {
                if (!lines[i].Active)
                {
                    return lines[i];
                }
            }

            GatherLine line = CreateLine(lines.Count);
            lines.Add(line);
            return line;
        }

        private GatherLine CreateLine(int index)
        {
            GameObject lineObject = new($"ChargeLine_{index}");
            lineObject.transform.SetParent(transform, false);
            LineRenderer renderer = lineObject.AddComponent<LineRenderer>();
            renderer.useWorldSpace = false;
            renderer.loop = false;
            renderer.positionCount = 2;
            renderer.widthMultiplier = Mathf.Max(0.01f, lineWidth);
            renderer.numCapVertices = 2;
            renderer.sortingLayerID = runtimeSortingLayerId;
            renderer.sortingOrder = runtimeSortingOrder;
            renderer.sharedMaterial = ResolveMaterial();

            GatherLine line = new()
            {
                GameObject = lineObject,
                Renderer = renderer
            };
            SetLineActive(line, false);
            return line;
        }

        private void EnsureMuzzleCircle()
        {
            if (muzzleCircle != null)
            {
                return;
            }

            muzzleCircleObject = new GameObject("ChargeMuzzleCircle");
            muzzleCircleObject.transform.SetParent(transform, false);
            muzzleCircle = muzzleCircleObject.AddComponent<LineRenderer>();
            muzzleCircle.useWorldSpace = false;
            muzzleCircle.loop = true;
            muzzleCircle.positionCount = Mathf.Max(8, circleSegments);
            muzzleCircle.widthMultiplier = Mathf.Max(0.01f, circleLineWidth);
            muzzleCircle.numCornerVertices = 2;
            muzzleCircle.numCapVertices = 2;
            muzzleCircle.sortingLayerID = runtimeSortingLayerId;
            muzzleCircle.sortingOrder = runtimeSortingOrder + 1;
            muzzleCircle.sharedMaterial = ResolveMaterial();
        }

        private void ResolveRuntimeSorting(Transform targetMuzzle)
        {
            runtimeSortingLayerId = SortingLayer.NameToID(sortingLayerName);
            runtimeSortingOrder = sortingOrder;
            if (!sortAboveMuzzleOwner || targetMuzzle == null)
            {
                return;
            }

            PlayerCombatController owner =
                targetMuzzle.GetComponentInParent<PlayerCombatController>();
            Transform rendererRoot = owner != null ? owner.transform : targetMuzzle.root;
            Renderer[] ownerRenderers = rendererRoot.GetComponentsInChildren<Renderer>(true);
            int bestLayerValue = int.MinValue;
            int bestOrder = int.MinValue;
            for (int i = 0; i < ownerRenderers.Length; i++)
            {
                Renderer ownerRenderer = ownerRenderers[i];
                if (ownerRenderer == null
                    || !ownerRenderer.enabled
                    || !ownerRenderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                int layerValue =
                    SortingLayer.GetLayerValueFromID(ownerRenderer.sortingLayerID);
                if (layerValue > bestLayerValue)
                {
                    bestLayerValue = layerValue;
                    runtimeSortingLayerId = ownerRenderer.sortingLayerID;
                    bestOrder = ownerRenderer.sortingOrder;
                }
                else if (layerValue == bestLayerValue)
                {
                    bestOrder = Mathf.Max(bestOrder, ownerRenderer.sortingOrder);
                }
            }

            if (bestOrder != int.MinValue)
            {
                runtimeSortingOrder =
                    bestOrder + Mathf.Max(1, ownerSortingOrderOffset);
            }
        }

        private void ApplyRuntimeSorting()
        {
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Renderer == null)
                {
                    continue;
                }

                lines[i].Renderer.sortingLayerID = runtimeSortingLayerId;
                lines[i].Renderer.sortingOrder = runtimeSortingOrder;
            }

            if (muzzleCircle != null)
            {
                muzzleCircle.sortingLayerID = runtimeSortingLayerId;
                muzzleCircle.sortingOrder = runtimeSortingOrder + 1;
            }
        }

        private void SetMuzzleCircleActive(bool active)
        {
            if (muzzleCircleObject != null)
            {
                muzzleCircleObject.SetActive(active);
            }
        }

        private Material ResolveMaterial()
        {
            if (lineMaterial != null)
            {
                return lineMaterial;
            }

            if (runtimeMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    runtimeMaterial = new Material(shader)
                    {
                        name = "ExecutionChargeLine_Runtime"
                    };
                }
            }

            return runtimeMaterial;
        }

        private static void SetLineActive(GatherLine line, bool active)
        {
            if (line?.GameObject == null)
            {
                return;
            }

            line.Active = active;
            line.GameObject.SetActive(active);
        }
    }
}
