using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class ConductorConductingCueAction : BossAction, ISerializationCallbackReceiver
    {
        [SerializeField, ConductorConductingPatternId] private string patternId = "Pattern";
        [SerializeField] private bool stopMovement = true;
        [SerializeField] private Vector2 headOffset = new(0f, 0.5f);
        [SerializeField, Min(0.01f)] private float scale = 0.225f;
        [SerializeField] private Color color = new(0.55f, 0f, 0f, 0.9f);
        [SerializeField, Min(0.001f)] private float lineWidth = 0.125f;
        [SerializeField, Min(0.01f)] private float strokeDrawSeconds = 0.18f;
        [SerializeField, Min(0f)] private float strokeIntervalSeconds = 0.04f;
        [SerializeField, Min(0f)] private float holdSeconds = 0.2f;
        [SerializeField, Min(0f)] private float fadeSeconds = 0.12f;
        [SerializeField] private int sortingOrder = 90;
        [Header("Completed Stroke")]
        [SerializeField] private Color completedFlashColor = Color.white;
        [SerializeField, Min(0f)] private float completedFlashSeconds = 0.08f;
        [SerializeField] private Color completedColor = Color.red;
        [SerializeField] private bool drawCompletedOutline = true;
        [SerializeField] private Color completedOutlineColor = Color.black;
        [SerializeField, Min(0f)] private float completedOutlineWidth = 0.025f;
        [SerializeField, HideInInspector] private bool migratedCompletedStrokeSettings;

        public string PatternId => patternId;

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            if (!migratedCompletedStrokeSettings)
            {
                color = new Color(0.55f, 0f, 0f, 0.9f);
                completedFlashColor = Color.white;
                if (completedFlashSeconds <= 0f)
                {
                    completedFlashSeconds = 0.08f;
                }

                completedColor = Color.red;
                drawCompletedOutline = true;
                completedOutlineColor = Color.black;
                if (completedOutlineWidth <= 0f)
                {
                    completedOutlineWidth = 0.025f;
                }

                migratedCompletedStrokeSettings = true;
            }
        }

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
                sortingOrder,
                completedFlashColor,
                completedFlashSeconds,
                completedColor,
                drawCompletedOutline,
                completedOutlineColor,
                completedOutlineWidth);
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
                + settings.CompletedFlashSeconds
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
            int sortingOrder,
            Color completedFlashColor,
            float completedFlashSeconds,
            Color completedColor,
            bool drawCompletedOutline,
            Color completedOutlineColor,
            float completedOutlineWidth)
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
            CompletedFlashColor = completedFlashColor;
            CompletedFlashSeconds = Mathf.Max(0f, completedFlashSeconds);
            CompletedColor = completedColor;
            DrawCompletedOutline = drawCompletedOutline;
            CompletedOutlineColor = completedOutlineColor;
            CompletedOutlineWidth = Mathf.Max(0f, completedOutlineWidth);
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
        public Color CompletedFlashColor { get; }
        public float CompletedFlashSeconds { get; }
        public Color CompletedColor { get; }
        public bool DrawCompletedOutline { get; }
        public Color CompletedOutlineColor { get; }
        public float CompletedOutlineWidth { get; }
    }
}
