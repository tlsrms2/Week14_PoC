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
    public sealed class ConductorConductingStroke
    {
        [SerializeField] private List<Vector2> points = new()
        {
            new Vector2(-0.25f, 0f),
            new Vector2(0.25f, 0f)
        };

        public IReadOnlyList<Vector2> Points => points;
        public bool HasDrawablePoints => points != null && points.Count >= 2;
    }

    public sealed class ConductorConductingPatternIdAttribute : PropertyAttribute
    {
    }
}
