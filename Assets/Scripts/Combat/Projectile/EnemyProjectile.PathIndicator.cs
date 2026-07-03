using UnityEngine;

namespace Week14.Combat
{
    public partial class EnemyProjectile
    {
        private bool ShouldShowPathIndicator()
        {
            return ShowsPathIndicator
                && !suppressPathIndicator
                && projectileSpeed > 0f
                && projectileLifetime > 0f;
        }

        private void ResetClonedPathIndicators()
        {
            pathIndicatorActive = false;
            pathIndicatorLength = 0f;
            pathIndicatorEndsAt = 0f;
            pathIndicatorHasRadialSplitPoint = false;
            radialSplitIndicatorLines.Clear();
            pathIndicatorDashes.Clear();
            homingAimReticleLines.Clear();
            pathIndicatorRoot = null;

            Transform existing = transform.Find(PathIndicatorName);
            if (existing == null)
            {
                return;
            }

            existing.gameObject.SetActive(false);
            existing.SetParent(null, false);
            Destroy(existing.gameObject);
        }

        private void UpdatePathIndicatorPreview()
        {
            if (!ShouldShowPathIndicator())
            {
                SetPathIndicatorVisible(false);
                return;
            }

            if (IsHomingProjectile)
            {
                DrawHomingPathIndicator();
                return;
            }

            float length = GetSplitAwarePathIndicatorLength(
                transform.position,
                flightDirection,
                projectileLifetime,
                out bool hasRadialSplitPoint,
                out Vector2 radialSplitPoint);
            DrawPathIndicator(transform.position, flightDirection, length, 0f);
            DrawRadialSplitIndicatorIfNeeded(hasRadialSplitPoint, radialSplitPoint);
        }

        private void BeginPathIndicator()
        {
            if (!ShouldShowPathIndicator())
            {
                SetPathIndicatorVisible(false);
                return;
            }

            pathIndicatorStart = transform.position;
            pathIndicatorDirection = flightDirection.sqrMagnitude > 0.0001f ? flightDirection.normalized : Vector2.left;
            float visibleSeconds = Mathf.Max(0f, destroyAt - Time.time);
            pathIndicatorEndsAt = Time.time + visibleSeconds;
            pathIndicatorLength = GetSplitAwarePathIndicatorLength(
                pathIndicatorStart,
                pathIndicatorDirection,
                visibleSeconds,
                out pathIndicatorHasRadialSplitPoint,
                out pathIndicatorRadialSplitPoint);
            pathIndicatorActive = true;

            if (IsHomingProjectile)
            {
                DrawHomingPathIndicator();
                return;
            }

            DrawPathIndicator(pathIndicatorStart, pathIndicatorDirection, pathIndicatorLength, 0f);
            DrawRadialSplitIndicatorIfNeeded(pathIndicatorHasRadialSplitPoint, pathIndicatorRadialSplitPoint);
        }

        private void TickPathIndicator()
        {
            if (!pathIndicatorActive || pathIndicatorLength <= 0f)
            {
                return;
            }

            if (Time.time >= pathIndicatorEndsAt)
            {
                SetPathIndicatorVisible(false);
                return;
            }

            if (IsHomingProjectile)
            {
                DrawHomingPathIndicator();
                return;
            }

            float travelled = Vector2.Dot((Vector2)transform.position - pathIndicatorStart, pathIndicatorDirection);
            DrawPathIndicator(pathIndicatorStart, pathIndicatorDirection, pathIndicatorLength, Mathf.Max(0f, travelled));
            DrawRadialSplitIndicatorIfNeeded(pathIndicatorHasRadialSplitPoint, pathIndicatorRadialSplitPoint);
        }

        private float GetPathIndicatorLength(Vector2 start, Vector2 direction, float seconds)
        {
            float length = projectileSpeed * Mathf.Max(0f, seconds);
            return GetWallClippedLength(start, direction, length);
        }

        private float GetSplitAwarePathIndicatorLength(
            Vector2 start,
            Vector2 direction,
            float visibleSeconds,
            out bool hasRadialSplitPoint,
            out Vector2 radialSplitPoint)
        {
            hasRadialSplitPoint = TryGetRadialSplitIndicatorPoint(
                start,
                direction,
                visibleSeconds,
                out float radialSplitLength,
                out radialSplitPoint);

            return hasRadialSplitPoint
                ? radialSplitLength
                : GetPathIndicatorLength(start, direction, visibleSeconds);
        }

        private bool TryGetRadialSplitIndicatorPoint(
            Vector2 start,
            Vector2 direction,
            float visibleSeconds,
            out float length,
            out Vector2 point)
        {
            length = 0f;
            point = start;
            if (!splitRadiallyOnLaunch || radialSplitBulletCount <= 0 || projectileSpeed <= 0f)
            {
                return false;
            }

            Vector2 normalized = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.left;
            float secondsToSplit = launched && radialSplitAt > 0f
                ? Mathf.Max(0f, radialSplitAt - Time.time)
                : Mathf.Max(0f, radialSplitDelaySeconds);
            float availableSeconds = Mathf.Max(0f, visibleSeconds);
            if (secondsToSplit > availableSeconds + 0.01f)
            {
                return false;
            }

            float splitDistance = projectileSpeed * secondsToSplit;
            if (TryGetWallHit(start, normalized, splitDistance, out _))
            {
                return false;
            }

            length = splitDistance;
            point = start + normalized * length;
            return true;
        }

        private void DrawHomingPathIndicator()
        {
            if (!TryGetHomingIndicatorTarget(out Vector2 start, out Vector2 direction, out Vector2 aimPoint))
            {
                SetPathIndicatorVisible(false);
                return;
            }

            float length = Vector2.Distance(start, aimPoint);
            bool blockedByWall = TryGetWallHit(start, direction, length, out RaycastHit2D wallHit);
            if (blockedByWall)
            {
                length = wallHit.distance;
            }

            if (length > 0.01f)
            {
                DrawPathIndicator(start, direction, length, 0f, true);
            }
            else
            {
                SetPathDashesVisible(false);
            }

            if (blockedByWall)
            {
                SetHomingAimReticleVisible(false);
            }
            else
            {
                DrawHomingAimReticle(aimPoint, direction);
            }
            pathIndicatorActive = true;
        }

        private bool TryGetHomingIndicatorTarget(out Vector2 start, out Vector2 direction, out Vector2 aimPoint)
        {
            start = transform.position;
            direction = flightDirection.sqrMagnitude > 0.0001f ? flightDirection.normalized : Vector2.left;
            aimPoint = start;

            PlayerCombatController target = PlayerCombatController.Active;
            if (target == null || target.Health == null || target.Health.IsDead)
            {
                return false;
            }

            Vector2 targetCenter = target.transform.position;
            Vector2 toTarget = targetCenter - start;
            if (toTarget.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            direction = toTarget.normalized;
            aimPoint = GetHomingAimPoint(target, start, direction);
            return true;
        }

        private Vector2 GetHomingAimPoint(PlayerCombatController target, Vector2 start, Vector2 direction)
        {
            Vector2 fallback = (Vector2)target.transform.position - direction * GetPlayerContactRadius(target);
            Collider2D[] colliders = target.GetComponentsInChildren<Collider2D>();
            if (colliders == null || colliders.Length == 0)
            {
                return fallback;
            }

            if (TryGetClosestPlayerColliderPoint(colliders, start, direction, false, out Vector2 solidPoint))
            {
                return solidPoint;
            }

            return TryGetClosestPlayerColliderPoint(colliders, start, direction, true, out Vector2 triggerPoint)
                ? triggerPoint
                : fallback;
        }

        private static float GetPlayerContactRadius(PlayerCombatController target)
        {
            PlayerCombatConfig config = target != null ? target.Config : null;
            return config != null ? Mathf.Max(0.05f, config.PlayerBodyAimRadius) : 0.35f;
        }

        private static bool TryGetClosestPlayerColliderPoint(
            Collider2D[] colliders,
            Vector2 start,
            Vector2 direction,
            bool includeTriggers,
            out Vector2 point)
        {
            point = start;
            float bestDistanceSqr = float.PositiveInfinity;
            bool found = false;

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D collider = colliders[i];
                if (collider == null
                    || !collider.enabled
                    || !collider.gameObject.activeInHierarchy
                    || (!includeTriggers && collider.isTrigger))
                {
                    continue;
                }

                Vector2 closest = collider.ClosestPoint(start);
                Vector2 delta = closest - start;
                if (Vector2.Dot(direction, delta) < -0.001f)
                {
                    continue;
                }

                float distanceSqr = delta.sqrMagnitude;
                if (distanceSqr >= bestDistanceSqr)
                {
                    continue;
                }

                bestDistanceSqr = distanceSqr;
                point = closest;
                found = true;
            }

            return found;
        }

        private void DrawPathIndicator(Vector2 start, Vector2 direction, float length, float travelled)
        {
            DrawPathIndicator(start, direction, length, travelled, false);
        }

        private void DrawPathIndicator(Vector2 start, Vector2 direction, float length, float travelled, bool keepHomingReticle)
        {
            if (!keepHomingReticle)
            {
                SetHomingAimReticleVisible(false);
            }

            if (length <= 0.01f)
            {
                SetPathIndicatorVisible(false);
                return;
            }

            Vector2 normalized = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.left;
            int dashCount = Mathf.Min(MaxPathDashCount, Mathf.CeilToInt(length / (PathDashLength + PathDashGap)));
            Color color = GetProjectileIndicatorColor(keepHomingReticle ? 0.86f : 0.58f);
            float width = Mathf.Max(keepHomingReticle ? 0.018f : 0.013f, projectileRadius * (keepHomingReticle ? 0.2f : 0.14f));
            int visibleCount = 0;

            for (int i = 0; i < dashCount; i++)
            {
                float segmentStart = i * (PathDashLength + PathDashGap);
                float segmentEnd = Mathf.Min(segmentStart + PathDashLength, length);
                if (segmentEnd <= travelled)
                {
                    SetPathDashVisible(i, false);
                    continue;
                }

                segmentStart = Mathf.Max(segmentStart, travelled);
                LineRenderer dash = EnsurePathDash(i);
                if (dash == null)
                {
                    continue;
                }

                dash.enabled = true;
                dash.startColor = color;
                dash.endColor = color;
                dash.startWidth = width;
                dash.endWidth = width;
                dash.SetPosition(0, start + normalized * segmentStart);
                dash.SetPosition(1, start + normalized * segmentEnd);
                visibleCount++;
            }

            for (int i = dashCount; i < pathIndicatorDashes.Count; i++)
            {
                SetPathDashVisible(i, false);
            }

            pathIndicatorActive = visibleCount > 0;
        }

        private void DrawRadialSplitIndicatorIfNeeded(bool visible, Vector2 center)
        {
            if (!visible)
            {
                SetRadialSplitIndicatorVisible(false);
                return;
            }

            float radius = Mathf.Max(0.08f, projectileRadius * 0.72f);
            float innerRadius = radius * 0.42f;
            float width = Mathf.Max(0.014f, projectileRadius * 0.14f);
            Color color = GetProjectileIndicatorColor(0.9f);

            SetRadialSplitIndicatorStar(0, center, radius, innerRadius, -90f, color, width);
            SetRadialSplitIndicatorStar(1, center, radius * 0.82f, innerRadius * 0.82f, -54f, color, width);
            for (int i = RadialSplitIndicatorLineCount; i < radialSplitIndicatorLines.Count; i++)
            {
                if (radialSplitIndicatorLines[i] != null)
                {
                    radialSplitIndicatorLines[i].enabled = false;
                }
            }

            pathIndicatorActive = true;
        }

        private void SetRadialSplitIndicatorStar(
            int index,
            Vector2 center,
            float outerRadius,
            float innerRadius,
            float rotationDegrees,
            Color color,
            float width)
        {
            LineRenderer line = EnsureRadialSplitIndicatorLine(index);
            if (line == null)
            {
                return;
            }

            ConfigureRadialSplitIndicatorLine(line, color, width);
            line.loop = true;
            line.positionCount = 10;
            for (int i = 0; i < 10; i++)
            {
                float angle = (rotationDegrees + i * 36f) * Mathf.Deg2Rad;
                float pointRadius = i % 2 == 0 ? outerRadius : innerRadius;
                line.SetPosition(i, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * pointRadius);
            }
        }

        private static void ConfigureRadialSplitIndicatorLine(LineRenderer line, Color color, float width)
        {
            line.enabled = true;
            line.startColor = color;
            line.endColor = color;
            line.startWidth = width;
            line.endWidth = width;
        }

        private void DrawHomingAimReticle(Vector2 center, Vector2 direction)
        {
            Vector2 forward = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.left;
            Vector2 side = new(-forward.y, forward.x);
            float radius = Mathf.Max(0.2f, projectileRadius * 2.1f);
            float crossRadius = radius * 0.72f;
            Color color = GetProjectileIndicatorColor(0.95f);
            float width = Mathf.Max(0.03f, projectileRadius * 0.3f);

            SetHomingAimReticleCircle(0, center, radius, color, width);
            SetHomingAimReticleSegment(1, center - side * crossRadius, center + side * crossRadius, color, width);
            SetHomingAimReticleSegment(2, center - forward * crossRadius, center + forward * crossRadius, color, width);

            for (int i = HomingAimReticleLineCount; i < homingAimReticleLines.Count; i++)
            {
                if (homingAimReticleLines[i] != null)
                {
                    homingAimReticleLines[i].enabled = false;
                }
            }
        }

        private Color GetProjectileIndicatorColor(float alpha)
        {
            Color color = customIndicatorColorConfigured
                ? indicatorColor
                : launched ? launchedColor : chargingColor;
            color.a = Mathf.Clamp01(alpha);
            return color;
        }
        private void SetHomingAimReticleCircle(int index, Vector2 center, float radius, Color color, float width)
        {
            LineRenderer line = EnsureHomingAimReticleLine(index);
            if (line == null)
            {
                return;
            }

            line.enabled = true;
            line.loop = true;
            line.positionCount = HomingAimReticleCircleSegments;
            line.startColor = color;
            line.endColor = color;
            line.startWidth = width;
            line.endWidth = width;

            for (int i = 0; i < HomingAimReticleCircleSegments; i++)
            {
                float angle = Mathf.PI * 2f * i / HomingAimReticleCircleSegments;
                line.SetPosition(i, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
        }

        private void SetHomingAimReticleSegment(int index, Vector2 start, Vector2 end, Color color, float width)
        {
            LineRenderer line = EnsureHomingAimReticleLine(index);
            if (line == null)
            {
                return;
            }

            line.enabled = true;
            line.loop = false;
            line.positionCount = 2;
            line.startColor = color;
            line.endColor = color;
            line.startWidth = width;
            line.endWidth = width;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
        }

        private LineRenderer EnsurePathDash(int index)
        {
            EnsurePathIndicatorRoot();
            if (pathIndicatorRoot == null)
            {
                return null;
            }

            while (pathIndicatorDashes.Count <= index)
            {
                GameObject dashObject = new($"{PathIndicatorName}_{pathIndicatorDashes.Count:00}");
                dashObject.transform.SetParent(pathIndicatorRoot, false);
                LineRenderer dash = dashObject.AddComponent<LineRenderer>();
                dash.useWorldSpace = true;
                dash.loop = false;
                dash.positionCount = 2;
                dash.numCornerVertices = 0;
                dash.numCapVertices = 1;
                dash.sortingOrder = 17;
                dash.material = GetChargeVfxMaterial();
                pathIndicatorDashes.Add(dash);
            }

            return pathIndicatorDashes[index];
        }

        private LineRenderer EnsureHomingAimReticleLine(int index)
        {
            EnsurePathIndicatorRoot();
            if (pathIndicatorRoot == null)
            {
                return null;
            }

            while (homingAimReticleLines.Count <= index)
            {
                GameObject lineObject = new($"{HomingAimReticleName}_{homingAimReticleLines.Count:00}");
                lineObject.transform.SetParent(pathIndicatorRoot, false);
                LineRenderer line = lineObject.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.loop = false;
                line.positionCount = 2;
                line.numCornerVertices = 0;
                line.numCapVertices = 1;
                line.sortingOrder = 19;
                line.material = GetChargeVfxMaterial();
                homingAimReticleLines.Add(line);
            }

            return homingAimReticleLines[index];
        }

        private LineRenderer EnsureRadialSplitIndicatorLine(int index)
        {
            EnsurePathIndicatorRoot();
            if (pathIndicatorRoot == null || index < 0)
            {
                return null;
            }

            while (radialSplitIndicatorLines.Count <= index)
            {
                GameObject lineObject = new($"RadialSplitIndicator_{radialSplitIndicatorLines.Count:00}");
                lineObject.transform.SetParent(pathIndicatorRoot, false);
                LineRenderer line = lineObject.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.loop = false;
                line.positionCount = 2;
                line.numCornerVertices = 2;
                line.numCapVertices = 2;
                line.sortingOrder = 20;
                line.material = GetChargeVfxMaterial();
                radialSplitIndicatorLines.Add(line);
            }

            return radialSplitIndicatorLines[index];
        }

        private void EnsurePathIndicatorRoot()
        {
            if (pathIndicatorRoot != null)
            {
                return;
            }

            Transform existing = transform.Find(PathIndicatorName);
            GameObject rootObject = existing != null ? existing.gameObject : new GameObject(PathIndicatorName);
            rootObject.transform.SetParent(transform, false);
            rootObject.transform.localPosition = Vector3.zero;
            rootObject.transform.localRotation = Quaternion.identity;
            rootObject.transform.localScale = Vector3.one;
            pathIndicatorRoot = rootObject.transform;
        }

        private void SetPathDashVisible(int index, bool visible)
        {
            if (index < 0 || index >= pathIndicatorDashes.Count || pathIndicatorDashes[index] == null)
            {
                return;
            }

            pathIndicatorDashes[index].enabled = visible;
        }

        private void SetPathIndicatorVisible(bool visible)
        {
            pathIndicatorActive = visible && pathIndicatorActive;
            SetPathDashesVisible(visible);
            SetHomingAimReticleVisible(visible);
            SetRadialSplitIndicatorVisible(visible);
        }

        private void SetPathDashesVisible(bool visible)
        {
            for (int i = 0; i < pathIndicatorDashes.Count; i++)
            {
                if (pathIndicatorDashes[i] != null)
                {
                    pathIndicatorDashes[i].enabled = visible;
                }
            }
        }

        private void SetHomingAimReticleVisible(bool visible)
        {
            for (int i = 0; i < homingAimReticleLines.Count; i++)
            {
                if (homingAimReticleLines[i] != null)
                {
                    homingAimReticleLines[i].enabled = visible;
                }
            }
        }

        private void SetRadialSplitIndicatorVisible(bool visible)
        {
            for (int i = 0; i < radialSplitIndicatorLines.Count; i++)
            {
                if (radialSplitIndicatorLines[i] != null)
                {
                    radialSplitIndicatorLines[i].enabled = visible;
                }
            }
        }

    }
}
