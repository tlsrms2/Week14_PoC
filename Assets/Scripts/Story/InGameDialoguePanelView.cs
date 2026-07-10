using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Week14.Story
{
    public sealed class InGameDialoguePanelView : MonoBehaviour
    {
        [Serializable]
        private sealed class SpeakerProfile
        {
            [SerializeField] private string speaker;
            [SerializeField] private Sprite profileSprite;

            public Sprite ProfileSprite => profileSprite;

            public bool Matches(string value)
            {
                return !string.IsNullOrWhiteSpace(speaker)
                    && string.Equals(speaker, value, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Header("Root")]
        [SerializeField] private GameObject root;

        [Header("Text")]
        [SerializeField] private GameObject speakerRoot;
        [SerializeField] private TMP_Text speakerText;
        [SerializeField] private TMP_Text dialogueText;
        [SerializeField, Min(1f)] private float charactersPerSecond = 45f;

        [Header("Profile")]
        [SerializeField] private Image profileImage;
        [SerializeField] private Sprite fallbackProfileSprite;
        [SerializeField] private List<SpeakerProfile> speakerProfiles = new();

        [Header("Skip")]
        [SerializeField] private GameObject skipRoot;
        [SerializeField] private Image skipFillImage;

        public bool IsTyping { get; private set; }

        private CanvasGroup fallbackCanvasGroup;

        private void Awake()
        {
            Hide();
            SetSkipProgress(false, 0f);
        }

        public void ShowLine(string speaker, string text)
        {
            SetRootVisible(true);
            SetSpeaker(speaker);
            SetText(dialogueText, text);

            if (dialogueText != null)
            {
                dialogueText.maxVisibleCharacters = 0;
            }
        }

        public IEnumerator PlayTypewriter(
            string text,
            Func<bool> revealRequested = null,
            Func<bool> cancelRequested = null)
        {
            if (dialogueText == null)
            {
                yield break;
            }

            IsTyping = true;
            dialogueText.text = text ?? string.Empty;
            dialogueText.maxVisibleCharacters = 0;
            dialogueText.ForceMeshUpdate();

            int totalCharacters = dialogueText.textInfo.characterCount;
            float visibleCharacters = 0f;
            float speed = Mathf.Max(1f, charactersPerSecond);
            while (visibleCharacters < totalCharacters)
            {
                if (cancelRequested?.Invoke() == true || revealRequested?.Invoke() == true)
                {
                    break;
                }

                visibleCharacters += Time.unscaledDeltaTime * speed;
                dialogueText.maxVisibleCharacters = Mathf.Clamp(
                    Mathf.CeilToInt(visibleCharacters),
                    0,
                    totalCharacters);
                yield return null;
            }

            RevealAll();
            IsTyping = false;
        }

        public void RevealAll()
        {
            if (dialogueText != null)
            {
                dialogueText.maxVisibleCharacters = int.MaxValue;
            }
        }

        public void Hide()
        {
            SetRootVisible(false);
            SetSpeaker(null);
            SetText(dialogueText, string.Empty);

            if (dialogueText != null)
            {
                dialogueText.maxVisibleCharacters = 0;
            }

            IsTyping = false;
            SetSkipProgress(false, 0f);
        }

        public void SetSkipProgress(bool visible, float progress)
        {
            if (skipRoot != null)
            {
                skipRoot.SetActive(visible);
            }

            if (skipFillImage != null)
            {
                skipFillImage.fillAmount = Mathf.Clamp01(progress);
                skipFillImage.gameObject.SetActive(visible);
            }
        }

        private void SetSpeaker(string speaker)
        {
            bool hasSpeaker = !string.IsNullOrWhiteSpace(speaker);
            if (speakerRoot != null)
            {
                speakerRoot.SetActive(hasSpeaker);
            }

            SetText(speakerText, hasSpeaker ? speaker : string.Empty);

            Sprite profileSprite = hasSpeaker ? ResolveProfileSprite(speaker) : null;
            if (profileImage != null)
            {
                profileImage.sprite = profileSprite;
                profileImage.enabled = profileSprite != null;
            }
        }

        private Sprite ResolveProfileSprite(string speaker)
        {
            for (int i = 0; i < speakerProfiles.Count; i++)
            {
                SpeakerProfile profile = speakerProfiles[i];
                if (profile != null && profile.Matches(speaker))
                {
                    return profile.ProfileSprite;
                }
            }

            return fallbackProfileSprite;
        }

        private void SetRootVisible(bool visible)
        {
            if (root != null)
            {
                root.SetActive(visible);
                return;
            }

            fallbackCanvasGroup ??= GetComponent<CanvasGroup>();
            fallbackCanvasGroup ??= gameObject.AddComponent<CanvasGroup>();
            fallbackCanvasGroup.alpha = visible ? 1f : 0f;
            fallbackCanvasGroup.interactable = visible;
            fallbackCanvasGroup.blocksRaycasts = visible;
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null)
            {
                target.text = value ?? string.Empty;
            }
        }
    }
}
