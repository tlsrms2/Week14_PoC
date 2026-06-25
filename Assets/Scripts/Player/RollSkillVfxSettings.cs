using UnityEngine;

namespace Week14.Combat
{
    public struct RollSkillVfxSettings
    {
        private const float DefaultAfterimageInterval = 0.045f;
        private const float DefaultAfterimageSeconds = 0.18f;
        private const float DefaultAutoParryAbsorbSeconds = 0.18f;

        private static readonly Color DefaultAfterimageColor = new Color(0.55f, 0.95f, 1f, 0.38f);
        private static readonly Color DefaultAutoParryAbsorbColor = new Color(0.45f, 0.95f, 1f, 0.85f);

        public RollSkillVfxSettings(
            float afterimageInterval,
            float afterimageSeconds,
            Color afterimageColor,
            float autoParryAbsorbSeconds,
            Color autoParryAbsorbColor)
        {
            AfterimageInterval = afterimageInterval;
            AfterimageSeconds = afterimageSeconds;
            AfterimageColor = afterimageColor;
            AutoParryAbsorbSeconds = autoParryAbsorbSeconds;
            AutoParryAbsorbColor = autoParryAbsorbColor;
        }

        public static RollSkillVfxSettings Default
        {
            get
            {
                return new RollSkillVfxSettings(
                    DefaultAfterimageInterval,
                    DefaultAfterimageSeconds,
                    DefaultAfterimageColor,
                    DefaultAutoParryAbsorbSeconds,
                    DefaultAutoParryAbsorbColor);
            }
        }

        public float AfterimageInterval { get; private set; }
        public float AfterimageSeconds { get; private set; }
        public Color AfterimageColor { get; private set; }
        public float AutoParryAbsorbSeconds { get; private set; }
        public Color AutoParryAbsorbColor { get; private set; }

        internal RollSkillVfxSettings Sanitized
        {
            get
            {
                return new RollSkillVfxSettings(
                    Mathf.Max(0.01f, AfterimageInterval),
                    Mathf.Max(0f, AfterimageSeconds),
                    AfterimageColor,
                    Mathf.Max(0f, AutoParryAbsorbSeconds),
                    AutoParryAbsorbColor);
            }
        }
    }
}
