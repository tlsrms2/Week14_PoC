using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class ConductorConductingCueAction : BossAction
    {
        [SerializeField, ConductorConductingPatternId] private string patternId = "Pattern";
        [SerializeField] private bool stopMovement = true;
        [SerializeField] private Vector2 headOffset = new(0f, 1.45f);
        [SerializeField, Min(0.01f)] private float scale = 1f;
        [SerializeField] private Color color = new(0.62f, 0.92f, 1f, 0.9f);
        [SerializeField, Min(0.001f)] private float lineWidth = 0.045f;
        [SerializeField, Min(0.01f)] private float strokeDrawSeconds = 0.18f;
        [SerializeField, Min(0f)] private float strokeIntervalSeconds = 0.04f;
        [SerializeField, Min(0f)] private float holdSeconds = 0.2f;
        [SerializeField, Min(0f)] private float fadeSeconds = 0.12f;
        [SerializeField] private int sortingOrder = 90;

        public string PatternId => patternId;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context?.Boss is not Conductor conductor)
            {
                yield break;
            }

            yield return conductor.PlayConductingPattern(patternId, context, CreateSettings());
        }

        public ConductorConductingCueSettings CreateSettings()
        {
            return new ConductorConductingCueSettings(
                stopMovement,
                headOffset,
                scale,
                color,
                lineWidth,
                strokeDrawSeconds,
                strokeIntervalSeconds,
                holdSeconds,
                fadeSeconds,
                sortingOrder);
        }

        public bool TryGetTotalSeconds(Conductor conductor, out float seconds)
        {
            seconds = 0f;
            if (conductor == null
                || !conductor.TryGetConductingPattern(patternId, out ConductorConductingPattern pattern)
                || pattern == null
                || !pattern.HasDrawableStroke)
            {
                return false;
            }

            ConductorConductingCueSettings settings = CreateSettings();
            int strokeCount = CountDrawableStrokes(pattern);
            seconds = strokeCount * (settings.StrokeDrawSeconds + settings.StrokeIntervalSeconds)
                + settings.HoldSeconds
                + settings.FadeSeconds;
            return seconds > 0f;
        }

        private static int CountDrawableStrokes(ConductorConductingPattern pattern)
        {
            IReadOnlyList<ConductorConductingStroke> strokes = pattern?.Strokes;
            if (strokes == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < strokes.Count; i++)
            {
                if (strokes[i] != null && strokes[i].HasDrawablePoints)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public readonly struct ConductorConductingCueSettings
    {
        public ConductorConductingCueSettings(
            bool stopMovement,
            Vector2 headOffset,
            float scale,
            Color color,
            float lineWidth,
            float strokeDrawSeconds,
            float strokeIntervalSeconds,
            float holdSeconds,
            float fadeSeconds,
            int sortingOrder)
        {
            StopMovement = stopMovement;
            HeadOffset = headOffset;
            Scale = Mathf.Max(0.01f, scale);
            Color = color;
            LineWidth = Mathf.Max(0.001f, lineWidth);
            StrokeDrawSeconds = Mathf.Max(0.01f, strokeDrawSeconds);
            StrokeIntervalSeconds = Mathf.Max(0f, strokeIntervalSeconds);
            HoldSeconds = Mathf.Max(0f, holdSeconds);
            FadeSeconds = Mathf.Max(0f, fadeSeconds);
            SortingOrder = sortingOrder;
        }

        public bool StopMovement { get; }
        public Vector2 HeadOffset { get; }
        public float Scale { get; }
        public Color Color { get; }
        public float LineWidth { get; }
        public float StrokeDrawSeconds { get; }
        public float StrokeIntervalSeconds { get; }
        public float HoldSeconds { get; }
        public float FadeSeconds { get; }
        public int SortingOrder { get; }
    }
}
