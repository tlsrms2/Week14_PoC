using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    public sealed class Conductor : GraphBossAI, IMinionPlayerHitHandler, IMinionMovementPathIndicatorOwner
    {
        private static readonly int IsWalkParameter = Animator.StringToHash("isWalk");
        private static readonly int StunParameter = Animator.StringToHash("Stun");
        private static readonly int EndStunParameter = Animator.StringToHash("EndStun");
        private const string FacingSpriteRendererName = "Conductor-side-Idle-x64_0";
        private const string MovementPathIndicatorName = "MovementPathIndicator";
        private const float MovementPathIndicatorWidth = 0.025f;

        [SerializeField, Min(0f)] private float bodyHitDamageMultiplier = 1f;
        [SerializeField, Min(0f)] private float minionHitDamageMultiplier = 0.5f;
        [SerializeField, Range(0f, 1f)] private float minionOutlineIdleAlpha = 0.3f;
        [SerializeField, Min(0f)] private float minionOutlineFlashSeconds = 0.12f;
        [SerializeField] private Animator walkAnimator;
        [SerializeField, Min(0f)] private float walkVelocityThreshold = 0.01f;
        [SerializeField, HideInInspector] private List<ConductorConductingPattern> conductingPatterns = new();

        private readonly Dictionary<Health, Minion> spawnedMinionsByHealth = new();
        private readonly Dictionary<Minion, Transform> spawnedMinionOutlines = new();
        private readonly Dictionary<Minion, Coroutine> outlineFlashRoutines = new();
        private readonly Dictionary<Minion, MovementPathIndicatorState> movementPathIndicators = new();
        private bool hasAppliedWalkState;
        private bool lastIsWalking;
        private SpriteRenderer facingSpriteRenderer;
        private static Material movementPathIndicatorMaterial;

        protected override bool RotatesBodyToPlayer => false;
        protected override bool ShouldUseExecutionAvailableBodyColor => false;
        public IReadOnlyList<ConductorConductingPattern> ConductingPatterns => conductingPatterns;

        public override bool ReceivePlayerHit(int bulletDamage, bool strongHit, Vector3 hitPosition, Vector2 hitDirection, Color hitColor)
        {
            int sharedDamage = GetBodySharedDamage(bulletDamage);
            return base.ReceivePlayerHit(sharedDamage, strongHit, hitPosition, hitDirection, hitColor);
        }

        public int GetBodySharedDamage(int bulletDamage)
        {
            return GetSharedDamage(bulletDamage, bodyHitDamageMultiplier);
        }

        public bool TryGetMinionSharedDamage(Minion minion, int bulletDamage, out int sharedDamage)
        {
            sharedDamage = 0;

            Health minionHealth = minion != null ? minion.Health : null;
            if (minionHealth == null || !spawnedMinionsByHealth.ContainsKey(minionHealth))
            {
                return false;
            }

            sharedDamage = GetSharedDamage(bulletDamage, minionHitDamageMultiplier);
            return true;
        }

        public bool TryHandleMinionPlayerHit(
            Minion minion,
            int bulletDamage,
            bool strongHit,
            Vector3 hitPosition,
            Vector2 hitDirection,
            Color hitColor)
        {
            if (!TryGetMinionSharedDamage(minion, bulletDamage, out int sharedDamage))
            {
                return false;
            }

            base.ReceivePlayerHit(sharedDamage, strongHit, hitPosition, hitDirection, hitColor);
            return true;
        }

        public void BeginMinionMovementPathIndicator(Minion minion, IReadOnlyList<Vector2> points, bool loop)
        {
            if (minion == null || points == null || points.Count < 2)
            {
                EndMinionMovementPathIndicator(minion);
                return;
            }

            MovementPathIndicatorState state = GetMovementPathIndicatorState(minion);
            state.Points.Clear();
            for (int i = 0; i < points.Count; i++)
            {
                state.Points.Add(points[i]);
            }

            state.Loop = loop;
            state.Active = true;
            DrawMinionMovementPathIndicator(state, 0f);
        }

        public void TickMinionMovementPathIndicator(Minion minion, Vector2 current)
        {
            if (minion == null
                || !movementPathIndicators.TryGetValue(minion, out MovementPathIndicatorState state)
                || !state.Active)
            {
                return;
            }

            if (state.Loop)
            {
                DrawMinionMovementPathIndicator(state, 0f);
                return;
            }

            float travelled = GetTravelledDistanceOnPath(state.Points, current);
            DrawMinionMovementPathIndicator(state, travelled);
        }

        public void EndMinionMovementPathIndicator(Minion minion)
        {
            if (minion == null || !movementPathIndicators.TryGetValue(minion, out MovementPathIndicatorState state))
            {
                return;
            }

            state.Active = false;
            SetMinionMovementPathIndicatorVisible(state, false);
        }

        public bool TryGetConductingPattern(string patternId, out ConductorConductingPattern pattern)
        {
            pattern = null;
            if (conductingPatterns == null || conductingPatterns.Count == 0)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(patternId))
            {
                pattern = conductingPatterns[0];
                return pattern != null;
            }

            for (int i = 0; i < conductingPatterns.Count; i++)
            {
                ConductorConductingPattern candidate = conductingPatterns[i];
                if (candidate != null
                    && string.Equals(candidate.PatternId, patternId, StringComparison.OrdinalIgnoreCase))
                {
                    pattern = candidate;
                    return true;
                }
            }

            return false;
        }

        public IEnumerator PlayConductingPattern(
            string patternId,
            BossActionContext context,
            ConductorConductingCueSettings settings,
            System.Func<bool> shouldCancel = null)
        {
            if (!TryGetConductingPattern(patternId, out ConductorConductingPattern pattern)
                || pattern == null
                || !pattern.HasDrawableStroke)
            {
                yield break;
            }

            Transform anchor = BodyRoot != null ? BodyRoot : transform;
            GameObject visualObject = new($"ConductorConductingPattern_{pattern.PatternId}");
            visualObject.transform.SetParent(anchor, false);
            visualObject.transform.localPosition = settings.HeadOffset;
            visualObject.transform.localRotation = Quaternion.identity;
            visualObject.transform.localScale = Vector3.one;

            ConductorConductingPatternVisual visual = visualObject.AddComponent<ConductorConductingPatternVisual>();
            visual.Configure(pattern, settings);
            context?.RegisterTransientVisual(visualObject);

            try
            {
                IReadOnlyList<ConductorConductingStroke> strokes = pattern.Strokes;
                for (int i = 0; i < strokes.Count; i++)
                {
                    if (ShouldCancelConductingPattern(shouldCancel))
                    {
                        yield break;
                    }

                    ConductorConductingStroke stroke = strokes[i];
                    if (stroke == null || !stroke.HasDrawablePoints)
                    {
                        continue;
                    }

                    yield return DrawConductingStroke(context, visual, i, stroke, settings, shouldCancel);
                    if (ShouldCancelConductingPattern(shouldCancel))
                    {
                        yield break;
                    }

                    if (settings.StrokeIntervalSeconds > 0f)
                    {
                        yield return WaitConductingSeconds(context, settings.StrokeIntervalSeconds, settings.StopMovement, shouldCancel);
                    }
                }

                if (settings.HoldSeconds > 0f)
                {
                    yield return WaitConductingSeconds(context, settings.HoldSeconds, settings.StopMovement, shouldCancel);
                }

                if (settings.FadeSeconds > 0f)
                {
                    yield return FadeConductingVisual(context, visual, settings.FadeSeconds, settings.StopMovement, shouldCancel);
                }
            }
            finally
            {
                context?.UnregisterTransientVisual(visualObject);

                if (visual != null)
                {
                    visual.ClearAndDestroy();
                }
            }
        }

        public override EnemyProjectile FireMinionProjectile(
            Minion source,
            BossProjectileSettings settings,
            Vector3 origin,
            Vector2 direction,
            bool playMuzzleFlash)
        {
            EnemyProjectile projectile = base.FireMinionProjectile(source, settings, origin, direction, playMuzzleFlash);
            if (projectile != null)
            {
                FlashMinionOutline(source);
            }

            return projectile;
        }

        protected override void OnMinionSpawned(Minion minion)
        {
            base.OnMinionSpawned(minion);
            TrackSpawnedMinion(minion);
            TrackMinionOutline(minion);
        }

        protected override void OnBossDied()
        {
            ApplyWalkState(false, true);
            UntrackAllSpawnedMinions();
            base.OnBossDied();
        }

        protected override void OnHpEmptyBegan()
        {
            SetAnimatorTrigger(StunParameter);
        }

        protected override void OnHpEmptyRecovered()
        {
            SetAnimatorTrigger(EndStunParameter);
        }

        protected override void OnDisable()
        {
            ApplyWalkState(false, true);
            UntrackAllSpawnedMinions();
            base.OnDisable();
        }

        private void LateUpdate()
        {
            UpdateFacingSprite();
            UpdateWalkState();

            foreach (KeyValuePair<Minion, Transform> entry in spawnedMinionOutlines)
            {
                if (entry.Key == null
                    || entry.Value == null
                    || outlineFlashRoutines.ContainsKey(entry.Key))
                {
                    continue;
                }

                ApplyMinionOutlineIdle(entry.Value);
            }
        }

        private void UpdateFacingSprite()
        {
            SpriteRenderer spriteRenderer = ResolveFacingSpriteRenderer();
            if (spriteRenderer == null || Player == null)
            {
                return;
            }

            spriteRenderer.flipX = Player.position.x > transform.position.x;
        }

        private void UpdateWalkState()
        {
            if (Body == null)
            {
                ApplyWalkState(false, false);
                return;
            }

            float threshold = Mathf.Max(0f, walkVelocityThreshold);
            bool isWalking = Body.linearVelocity.sqrMagnitude > threshold * threshold;
            ApplyWalkState(isWalking, false);
        }

        private void ApplyWalkState(bool isWalking, bool force)
        {
            Animator targetAnimator = ResolveWalkAnimator();
            if (targetAnimator == null)
            {
                return;
            }

            if (!force && hasAppliedWalkState && lastIsWalking == isWalking)
            {
                return;
            }

            targetAnimator.SetBool(IsWalkParameter, isWalking);
            lastIsWalking = isWalking;
            hasAppliedWalkState = true;
        }

        private void SetAnimatorTrigger(int parameter)
        {
            Animator targetAnimator = ResolveWalkAnimator();
            if (targetAnimator == null)
            {
                return;
            }

            targetAnimator.SetTrigger(parameter);
        }

        private Animator ResolveWalkAnimator()
        {
            if (walkAnimator != null)
            {
                return walkAnimator;
            }

            walkAnimator = BodyRoot != null
                ? BodyRoot.GetComponentInChildren<Animator>(true)
                : GetComponentInChildren<Animator>(true);
            return walkAnimator;
        }

        private SpriteRenderer ResolveFacingSpriteRenderer()
        {
            if (facingSpriteRenderer != null)
            {
                return facingSpriteRenderer;
            }

            Transform root = BodyRoot != null ? BodyRoot : transform;
            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                Transform child = children[i];
                if (child != null && child.name == FacingSpriteRendererName)
                {
                    facingSpriteRenderer = child.GetComponent<SpriteRenderer>();
                    return facingSpriteRenderer;
                }
            }

            return null;
        }

        private void TrackSpawnedMinion(Minion minion)
        {
            Health minionHealth = minion != null ? minion.Health : null;
            if (minionHealth == null || minionHealth.IsDead || spawnedMinionsByHealth.ContainsKey(minionHealth))
            {
                return;
            }

            spawnedMinionsByHealth.Add(minionHealth, minion);
            minionHealth.Died += HandleSpawnedMinionDied;
        }

        private void HandleSpawnedMinionDied(Health minionHealth)
        {
            UntrackSpawnedMinion(minionHealth);
        }

        private void UntrackSpawnedMinion(Health minionHealth)
        {
            if (minionHealth == null || !spawnedMinionsByHealth.TryGetValue(minionHealth, out Minion minion))
            {
                return;
            }

            spawnedMinionsByHealth.Remove(minionHealth);
            minionHealth.Died -= HandleSpawnedMinionDied;
            UntrackMinionOutline(minion);
        }

        private void UntrackAllSpawnedMinions()
        {
            foreach (MovementPathIndicatorState indicator in movementPathIndicators.Values)
            {
                SetMinionMovementPathIndicatorVisible(indicator, false);
            }

            foreach (Minion minion in spawnedMinionsByHealth.Values)
            {
                UntrackMinionOutline(minion);
            }

            foreach (Health minionHealth in spawnedMinionsByHealth.Keys)
            {
                if (minionHealth != null)
                {
                    minionHealth.Died -= HandleSpawnedMinionDied;
                }
            }

            spawnedMinionsByHealth.Clear();
            spawnedMinionOutlines.Clear();
            outlineFlashRoutines.Clear();
            movementPathIndicators.Clear();
        }

        private void TrackMinionOutline(Minion minion)
        {
            Transform outline = FindMinionOutline(minion);
            if (minion == null || outline == null)
            {
                return;
            }

            spawnedMinionOutlines[minion] = outline;
            ApplyMinionOutlineIdle(outline);
        }

        private void FlashMinionOutline(Minion minion)
        {
            if (minion == null || !spawnedMinionOutlines.TryGetValue(minion, out Transform outline) || outline == null)
            {
                return;
            }

            if (outlineFlashRoutines.TryGetValue(minion, out Coroutine routine) && routine != null)
            {
                StopCoroutine(routine);
            }

            outlineFlashRoutines[minion] = StartCoroutine(FlashMinionOutlineRoutine(minion, outline));
        }

        private IEnumerator FlashMinionOutlineRoutine(Minion minion, Transform outline)
        {
            outline.gameObject.SetActive(true);
            SetMinionOutlineAlpha(outline, 1f);

            float remainingSeconds = minionOutlineFlashSeconds;
            while (remainingSeconds > 0f)
            {
                remainingSeconds -= EnemyTimeScale.DeltaTime;
                yield return null;
            }

            if (outline != null)
            {
                ApplyMinionOutlineIdle(outline);
            }

            outlineFlashRoutines.Remove(minion);
        }

        private void UntrackMinionOutline(Minion minion)
        {
            if (minion == null)
            {
                return;
            }

            EndMinionMovementPathIndicator(minion);
            movementPathIndicators.Remove(minion);

            if (outlineFlashRoutines.TryGetValue(minion, out Coroutine routine) && routine != null)
            {
                StopCoroutine(routine);
            }

            outlineFlashRoutines.Remove(minion);
            if (spawnedMinionOutlines.TryGetValue(minion, out Transform outline) && outline != null)
            {
                outline.gameObject.SetActive(false);
            }

            spawnedMinionOutlines.Remove(minion);
        }

        private void ApplyMinionOutlineIdle(Transform outline)
        {
            if (outline == null)
            {
                return;
            }

            outline.gameObject.SetActive(true);
            SetMinionOutlineAlpha(outline, minionOutlineIdleAlpha);
        }

        private static void SetMinionOutlineAlpha(Transform outline, float alpha)
        {
            if (outline == null)
            {
                return;
            }

            SpriteRenderer[] renderers = outline.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                {
                    continue;
                }

                Color color = renderers[i].color;
                color.a = alpha;
                renderers[i].color = color;
            }
        }

        private static Transform FindMinionOutline(Minion minion)
        {
            if (minion == null)
            {
                return null;
            }

            Transform[] children = minion.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i] != null && children[i].name == "Outline")
                {
                    return children[i];
                }
            }

            return null;
        }

        private static IEnumerator DrawConductingStroke(
            BossActionContext context,
            ConductorConductingPatternVisual visual,
            int strokeIndex,
            ConductorConductingStroke stroke,
            ConductorConductingCueSettings settings,
            System.Func<bool> shouldCancel)
        {
            if (visual == null || stroke == null)
            {
                yield break;
            }

            visual.SetStrokeProgress(strokeIndex, 0f);
            float duration = settings.StrokeDrawSeconds;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (visual == null || ShouldCancelConductingPattern(shouldCancel))
                {
                    yield break;
                }

                if (context != null && context.IsExecutionPaused)
                {
                    context.Stop();
                    yield return null;
                    continue;
                }

                if (settings.StopMovement)
                {
                    context?.Stop();
                }

                elapsed += EnemyTimeScale.DeltaTime;
                visual.SetStrokeProgress(strokeIndex, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            if (visual != null)
            {
                visual.SetStrokeProgress(strokeIndex, 1f);
            }
        }

        private static IEnumerator WaitConductingSeconds(
            BossActionContext context,
            float seconds,
            bool stopMovement,
            System.Func<bool> shouldCancel)
        {
            float remainingSeconds = Mathf.Max(0f, seconds);
            while (remainingSeconds > 0f)
            {
                if (ShouldCancelConductingPattern(shouldCancel))
                {
                    yield break;
                }

                if (context != null && context.IsExecutionPaused)
                {
                    context.Stop();
                    yield return null;
                    continue;
                }

                if (stopMovement)
                {
                    context?.Stop();
                }

                remainingSeconds -= EnemyTimeScale.DeltaTime;
                yield return null;
            }
        }

        private static IEnumerator FadeConductingVisual(
            BossActionContext context,
            ConductorConductingPatternVisual visual,
            float seconds,
            bool stopMovement,
            System.Func<bool> shouldCancel)
        {
            if (visual == null)
            {
                yield break;
            }

            float duration = Mathf.Max(0.01f, seconds);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (visual == null || ShouldCancelConductingPattern(shouldCancel))
                {
                    yield break;
                }

                if (context != null && context.IsExecutionPaused)
                {
                    context.Stop();
                    yield return null;
                    continue;
                }

                if (stopMovement)
                {
                    context?.Stop();
                }

                elapsed += EnemyTimeScale.DeltaTime;
                visual.SetAlpha(1f - Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            if (visual != null)
            {
                visual.SetAlpha(0f);
            }
        }

        private static bool ShouldCancelConductingPattern(System.Func<bool> shouldCancel)
        {
            return shouldCancel?.Invoke() == true;
        }

        private MovementPathIndicatorState GetMovementPathIndicatorState(Minion minion)
        {
            if (movementPathIndicators.TryGetValue(minion, out MovementPathIndicatorState state))
            {
                return state;
            }

            state = new MovementPathIndicatorState
            {
                Root = EnsureMinionMovementPathIndicatorRoot(minion)
            };
            movementPathIndicators[minion] = state;
            return state;
        }

        private static Transform EnsureMinionMovementPathIndicatorRoot(Minion minion)
        {
            Transform existing = minion.transform.Find(MovementPathIndicatorName);
            GameObject rootObject = existing != null ? existing.gameObject : new GameObject(MovementPathIndicatorName);
            rootObject.transform.SetParent(minion.transform, false);
            rootObject.transform.localPosition = Vector3.zero;
            rootObject.transform.localRotation = Quaternion.identity;
            rootObject.transform.localScale = Vector3.one;
            return rootObject.transform;
        }

        private static void DrawMinionMovementPathIndicator(MovementPathIndicatorState state, float travelled)
        {
            if (state == null || state.Root == null || state.Points.Count < 2)
            {
                SetMinionMovementPathIndicatorVisible(state, false);
                return;
            }

            LineRenderer line = EnsureMinionMovementPathIndicatorLine(state);
            if (line == null)
            {
                return;
            }

            state.RenderPoints.Clear();
            if (state.Loop)
            {
                for (int i = 0; i < state.Points.Count; i++)
                {
                    state.RenderPoints.Add(state.Points[i]);
                }
            }
            else
            {
                BuildRemainingPath(state.Points, Mathf.Max(0f, travelled), state.RenderPoints);
            }

            if (state.RenderPoints.Count < 2)
            {
                line.enabled = false;
                state.Active = false;
                return;
            }

            line.enabled = true;
            line.loop = state.Loop;
            line.positionCount = state.RenderPoints.Count;
            line.startColor = Color.black;
            line.endColor = Color.black;
            line.startWidth = MovementPathIndicatorWidth;
            line.endWidth = MovementPathIndicatorWidth;
            for (int i = 0; i < state.RenderPoints.Count; i++)
            {
                line.SetPosition(i, state.RenderPoints[i]);
            }

            state.Active = true;
        }

        private static LineRenderer EnsureMinionMovementPathIndicatorLine(MovementPathIndicatorState state)
        {
            if (state == null || state.Root == null)
            {
                return null;
            }

            if (state.Line != null)
            {
                return state.Line;
            }

            GameObject lineObject = new($"{MovementPathIndicatorName}_Line");
            lineObject.transform.SetParent(state.Root, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = false;
            line.positionCount = 2;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.sortingOrder = 17;
            line.material = GetMovementPathIndicatorMaterial();
            state.Line = line;
            return line;
        }

        private static void SetMinionMovementPathIndicatorVisible(MovementPathIndicatorState state, bool visible)
        {
            if (state == null)
            {
                return;
            }

            state.Active = visible && state.Active;
            if (state.Line != null)
            {
                state.Line.enabled = visible;
            }
        }

        private static Material GetMovementPathIndicatorMaterial()
        {
            if (movementPathIndicatorMaterial != null)
            {
                return movementPathIndicatorMaterial;
            }

            Shader shader = Shader.Find("Sprites/Default");
            movementPathIndicatorMaterial = shader != null ? new Material(shader) : null;
            return movementPathIndicatorMaterial;
        }

        private static float GetTravelledDistanceOnPath(IReadOnlyList<Vector2> points, Vector2 current)
        {
            float bestDistance = 0f;
            float bestSqrDistance = float.MaxValue;
            float totalDistance = 0f;

            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector2 start = points[i];
                Vector2 end = points[i + 1];
                Vector2 segment = end - start;
                float segmentLength = segment.magnitude;
                if (segmentLength <= 0.0001f)
                {
                    continue;
                }

                float t = Mathf.Clamp01(Vector2.Dot(current - start, segment) / (segmentLength * segmentLength));
                Vector2 closest = start + segment * t;
                float sqrDistance = ((Vector2)current - closest).sqrMagnitude;
                if (sqrDistance < bestSqrDistance)
                {
                    bestSqrDistance = sqrDistance;
                    bestDistance = totalDistance + segmentLength * t;
                }

                totalDistance += segmentLength;
            }

            return bestDistance;
        }

        private static void BuildRemainingPath(
            IReadOnlyList<Vector2> points,
            float travelled,
            List<Vector3> results)
        {
            results.Clear();
            float remainingTravel = travelled;

            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector2 start = points[i];
                Vector2 end = points[i + 1];
                float segmentLength = Vector2.Distance(start, end);
                if (segmentLength <= 0.0001f)
                {
                    continue;
                }

                if (remainingTravel >= segmentLength)
                {
                    remainingTravel -= segmentLength;
                    continue;
                }

                Vector2 segmentStart = remainingTravel > 0f
                    ? Vector2.Lerp(start, end, remainingTravel / segmentLength)
                    : start;
                results.Add(segmentStart);
                for (int j = i + 1; j < points.Count; j++)
                {
                    results.Add(points[j]);
                }

                return;
            }
        }

        private sealed class MovementPathIndicatorState
        {
            public readonly List<Vector2> Points = new();
            public readonly List<Vector3> RenderPoints = new();
            public Transform Root;
            public LineRenderer Line;
            public bool Active;
            public bool Loop;
        }

        private static int GetSharedDamage(int bulletDamage, float multiplier)
        {
            if (bulletDamage <= 0 || multiplier <= 0f)
            {
                return 0;
            }

            return Mathf.Max(1, Mathf.RoundToInt(bulletDamage * multiplier));
        }
    }
}
