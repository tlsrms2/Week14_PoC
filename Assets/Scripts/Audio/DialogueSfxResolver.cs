using System;

namespace Week14.Audio
{
    public static class DialogueSfxResolver
    {
        private const string ErisSfxId = "TalkERIS";
        private const string LeeOnSfxId = "TalkLeeOn";
        private const string DadSfxId = "TalkDad";

        public static string Resolve(string configuredId, string speaker)
        {
            if (!string.IsNullOrWhiteSpace(configuredId))
            {
                return configuredId;
            }

            string normalizedSpeaker = speaker?.Trim();
            if (string.IsNullOrEmpty(normalizedSpeaker))
            {
                return string.Empty;
            }

            if (Matches(normalizedSpeaker, "ERIS", "에리스"))
            {
                return ErisSfxId;
            }

            if (Matches(normalizedSpeaker, "이온", "LEEON", "LEE ON", "LEE-ON"))
            {
                return LeeOnSfxId;
            }

            if (Matches(normalizedSpeaker, "DAD", "FATHER", "아빠", "아버지"))
            {
                return DadSfxId;
            }

            return string.Empty;
        }

        public static bool IsTalkSfx(string sfxId)
        {
            return string.Equals(sfxId, ErisSfxId, StringComparison.Ordinal)
                || string.Equals(sfxId, LeeOnSfxId, StringComparison.Ordinal)
                || string.Equals(sfxId, DadSfxId, StringComparison.Ordinal);
        }

        public static void PlayTypingSfx(string sfxId, char character)
        {
            if (!IsTalkSfx(sfxId)
                || char.IsWhiteSpace(character)
                || char.IsControl(character))
            {
                return;
            }

            SoundManager.PlaySfx(sfxId);
        }

        private static bool Matches(string value, params string[] candidates)
        {
            for (int i = 0; i < candidates.Length; i++)
            {
                if (string.Equals(
                        value,
                        candidates[i],
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
