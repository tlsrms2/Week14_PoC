using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Week14.Audio;
using Week14.Enemy;

namespace Week14.UI
{
    public sealed class BossDetailPanel : MonoBehaviour
    {
        [SerializeField] private RectTransform panelRect;
        [Tooltip("패널이 펼쳐질 때 재생할 SFX의 SoundLibrary ID입니다. 비워두면 재생하지 않습니다.")]
        [BossGraphSfxId]
        [SerializeField] private string showSfxId;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text crimeText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private Image iconImage;
        [Tooltip("패널이 다 펼쳐졌을 때의 높이입니다.")]
        [SerializeField, Min(0f)] private float expandedHeight = 240f;
        [Tooltip("높이 0에서 펼쳐진 높이까지 커지는 데 걸리는 시간입니다. 빠르게 보이도록 짧게 설정합니다.")]
        [SerializeField, Min(0f)] private float growSeconds = 0.12f;
        [Tooltip("패널이 다 펼쳐진 후 첫 항목이 나타나기까지의 지연 시간입니다.")]
        [SerializeField, Min(0f)] private float initialRevealDelaySeconds = 0.06f;
        [Tooltip("패널이 다 펼쳐진 후 항목들이 하나씩 나타나는 간격입니다.")]
        [SerializeField, Min(0f)] private float revealStaggerSeconds = 0.06f;
        [Tooltip("패널이 펼쳐진 후 이 배열 순서대로 하나씩 나타납니다. 가장 마지막에 나타나야 할 항목(예: ProgressBar)을 맨 마지막에 넣으세요.")]
        [SerializeField] private GameObject[] revealTargets = Array.Empty<GameObject>();
        [Tooltip("가장 먼저 FillAmount가 0에서 1로 채워지며 나타나는 이미지입니다.")]
        [SerializeField] private Image firstFillRevealImage;
        [Tooltip("첫 번째 이미지의 FillAmount가 0에서 1로 채워지는 데 걸리는 시간입니다.")]
        [SerializeField, Min(0f)] private float firstFillRevealSeconds = 0.15f;
        [Tooltip("첫 번째 연출이 끝난 후, 패널의 높이가 커지기 전에 FillAmount가 0에서 1로 채워지며 나타나는 이미지입니다.")]
        [SerializeField] private Image fillRevealImage;
        [Tooltip("FillAmount가 0에서 1로 채워지는 데 걸리는 시간입니다.")]
        [SerializeField, Min(0f)] private float fillRevealSeconds = 0.15f;

        private Coroutine showRoutine;
        private Coroutine growRoutine;
        private Coroutine revealRoutine;

        private void Awake()
        {
            if (panelRect == null)
            {
                panelRect = transform as RectTransform;
            }

            HideImmediate();
        }

        public void Show(string bossName, string crime, string description, Sprite icon)
        {
            if (nameText != null)
            {
                nameText.text = bossName;
            }

            if (crimeText != null)
            {
                crimeText.text = crime;
            }

            if (descriptionText != null)
            {
                descriptionText.text = description;
            }

            if (iconImage != null)
            {
                iconImage.sprite = icon;
                iconImage.enabled = icon != null;
            }

            if (!string.IsNullOrEmpty(showSfxId))
            {
                SoundManager.PlaySfx(showSfxId);
            }

            SetRevealTargetsActive(false);
            ResetFillImages();
            StopShowRoutine();
            showRoutine = StartCoroutine(ShowSequenceRoutine());
        }

        public void Hide()
        {
            StopShowRoutine();
            StopRevealRoutine();
            SetRevealTargetsActive(false);
            ResetFillImages();
            PlayGrow(0f, revealAfterGrow: false);
        }

        public void HideImmediate()
        {
            StopShowRoutine();

            if (growRoutine != null)
            {
                StopCoroutine(growRoutine);
                growRoutine = null;
            }

            StopRevealRoutine();
            SetRevealTargetsActive(false);
            ResetFillImages();
            SetHeight(0f);
        }

        private IEnumerator ShowSequenceRoutine()
        {
            yield return FillRevealRoutine(firstFillRevealImage, firstFillRevealSeconds);
            yield return FillRevealRoutine(fillRevealImage, fillRevealSeconds);
            showRoutine = null;
            PlayGrow(expandedHeight, revealAfterGrow: true);
        }

        private IEnumerator FillRevealRoutine(Image image, float seconds)
        {
            if (image == null)
            {
                yield break;
            }

            float t = 0f;
            while (seconds > 0f && t < seconds)
            {
                t += Time.unscaledDeltaTime;
                image.fillAmount = Mathf.Clamp01(t / seconds);
                yield return null;
            }

            image.fillAmount = 1f;
        }

        private void StopShowRoutine()
        {
            if (showRoutine != null)
            {
                StopCoroutine(showRoutine);
                showRoutine = null;
            }
        }

        private void ResetFillImages()
        {
            SetFillAmount(firstFillRevealImage, 0f);
            SetFillAmount(fillRevealImage, 0f);
        }

        private void SetFillAmount(Image image, float amount)
        {
            if (image != null)
            {
                image.fillAmount = amount;
            }
        }

        private void SetRevealTargetsActive(bool active)
        {
            if (revealTargets == null)
            {
                return;
            }

            for (int i = 0; i < revealTargets.Length; i++)
            {
                if (revealTargets[i] != null)
                {
                    revealTargets[i].SetActive(active);
                }
            }
        }

        private void StopRevealRoutine()
        {
            if (revealRoutine != null)
            {
                StopCoroutine(revealRoutine);
                revealRoutine = null;
            }
        }

        private void PlayGrow(float targetHeight, bool revealAfterGrow)
        {
            if (panelRect == null)
            {
                return;
            }

            if (growRoutine != null)
            {
                StopCoroutine(growRoutine);
            }

            growRoutine = StartCoroutine(GrowRoutine(targetHeight, revealAfterGrow));
        }

        private IEnumerator GrowRoutine(float targetHeight, bool revealAfterGrow)
        {
            float startHeight = panelRect.sizeDelta.y;
            float t = 0f;
            while (growSeconds > 0f && t < growSeconds)
            {
                t += Time.unscaledDeltaTime;
                SetHeight(Mathf.Lerp(startHeight, targetHeight, t / growSeconds));
                yield return null;
            }

            SetHeight(targetHeight);
            growRoutine = null;

            if (revealAfterGrow)
            {
                StopRevealRoutine();
                revealRoutine = StartCoroutine(RevealRoutine());
            }
        }

        private IEnumerator RevealRoutine()
        {
            if (initialRevealDelaySeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(initialRevealDelaySeconds);
            }

            for (int i = 0; i < revealTargets.Length; i++)
            {
                GameObject target = revealTargets[i];
                if (target == null)
                {
                    continue;
                }

                target.SetActive(true);
                if (revealStaggerSeconds > 0f)
                {
                    yield return new WaitForSecondsRealtime(revealStaggerSeconds);
                }
            }

            revealRoutine = null;
        }

        private void SetHeight(float height)
        {
            if (panelRect == null)
            {
                return;
            }

            Vector2 size = panelRect.sizeDelta;
            size.y = height;
            panelRect.sizeDelta = size;
        }
    }
}
