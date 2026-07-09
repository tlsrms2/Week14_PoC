using System;
using System.Collections.Generic;
using UnityEngine;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class ConductorConductingPattern
    {
        [SerializeField] private string patternId = "Pattern";
        [SerializeField] private List<ConductorConductingStroke> strokes = new()
        {
            new ConductorConductingStroke()
        };

        public string PatternId => patternId;
        public IReadOnlyList<ConductorConductingStroke> Strokes => strokes;

        public bool HasDrawableStroke
        {
            get
            {
                if (strokes == null)
                {
                    return false;
                }

                for (int i = 0; i < strokes.Count; i++)
                {
                    if (strokes[i] != null && strokes[i].HasDrawablePoints)
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }

    [Serializable]
    public sealed class ConductorConductingStroke : ISerializationCallbackReceiver
    {
        private const int DefaultCurveSegments = 18;

        [SerializeField] private Vector2 start = new(-0.25f, 0f);
        [SerializeField] private Vector2 end = new(0.25f, 0f);
        [SerializeField] private bool hasControlPoint;
        [SerializeField] private Vector2 controlPoint;
        [SerializeField, HideInInspector] private List<Vector2> points = new();
        [SerializeField, HideInInspector] private bool migratedLegacyPoints;

        public Vector2 Start => start;
        public Vector2 End => end;
        public bool HasControlPoint => hasControlPoint;
        public Vector2 ControlPoint => controlPoint;
        public bool HasDrawablePoints => Vector2.Distance(start, end) > 0.0001f;

        public void BuildRenderPoints(List<Vector2> results)
        {
            results.Clear();
            if (!HasDrawablePoints)
            {
                return;
            }

            if (!hasControlPoint)
            {
                results.Add(start);
                results.Add(end);
                return;
            }

            int segments = Mathf.Max(2, DefaultCurveSegments);
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                results.Add(EvaluateQuadratic(start, controlPoint, end, t));
            }
        }

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            if (migratedLegacyPoints || points == null || points.Count < 2)
            {
                return;
            }

            start = points[0];
            end = points[^1];
            if (points.Count > 2)
            {
                hasControlPoint = true;
                controlPoint = points[points.Count / 2];
            }

            migratedLegacyPoints = true;
        }

        private static Vector2 EvaluateQuadratic(Vector2 a, Vector2 b, Vector2 c, float t)
        {
            float inverse = 1f - t;
            return inverse * inverse * a + 2f * inverse * t * b + t * t * c;
        }
    }

    public sealed class ConductorConductingPatternIdAttribute : PropertyAttribute
    {
    }
}
