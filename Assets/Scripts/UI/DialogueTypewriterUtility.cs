using System;
using TMPro;
using UnityEngine;

namespace Week14.UI
{
    internal static class DialogueTypewriterUtility
    {
        public static void NotifyRevealedCharacters(
            TMP_Text text,
            int previousVisibleCount,
            int currentVisibleCount,
            Action<char> onCharacterRevealed)
        {
            if (text == null
                || onCharacterRevealed == null
                || currentVisibleCount <= previousVisibleCount)
            {
                return;
            }

            TMP_TextInfo textInfo = text.textInfo;
            int startIndex = Mathf.Clamp(
                previousVisibleCount,
                0,
                textInfo.characterCount);
            int endIndex = Mathf.Clamp(
                currentVisibleCount,
                startIndex,
                textInfo.characterCount);

            for (int i = startIndex; i < endIndex; i++)
            {
                onCharacterRevealed(textInfo.characterInfo[i].character);
            }
        }
    }
}
