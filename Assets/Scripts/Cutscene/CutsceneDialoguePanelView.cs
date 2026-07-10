using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace Week14.Cutscene
{
    public sealed class CutsceneDialoguePanelView : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private GameObject speakerRoot;
        [SerializeField] private TMP_Text speakerText;
        [SerializeField] private TMP_Text dialogueText;
        [SerializeField, Min(1f)] private float charactersPerSecond = 45f;

        public bool IsTyping { get; private set; }

        private CanvasGroup fallbackCanvasGroup;

        private void Awake()
        {
            Hide();
        }

        public void ShowLine(string speaker, string text)
        {
            SetRootVisible(true);

            bool hasSpeaker = !string.IsNullOrWhiteSpace(speaker);
            if (speakerRoot != null)
            {
                speakerRoot.SetActive(hasSpeaker);
            }

            SetText(speakerText, hasSpeaker ? speaker : string.Empty);
            SetText(dialogueText, text);

            if (dialogueText != null)
            {
                dialogueText.maxVisibleCharacters = 0;
            }
        }

        public IEnumerator PlayTypewriter(string text, Func<bool> revealRequested = null, Func<bool> cancelRequested = null)
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

            if (speakerRoot != null)
            {
                speakerRoot.SetActive(false);
            }

            SetText(speakerText, string.Empty);
            SetText(dialogueText, string.Empty);

            if (dialogueText != null)
            {
                dialogueText.maxVisibleCharacters = 0;
            }

            IsTyping = false;
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
