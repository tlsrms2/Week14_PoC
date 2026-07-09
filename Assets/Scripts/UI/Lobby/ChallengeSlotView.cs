using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;
using Week14.Challenge;
using Week14.Save;

namespace Week14.UI
{
    public sealed class ChallengeSlotView : MonoBehaviour
    {
        [Tooltip("이 슬롯에 표시할 챌린지 설명 TMP 텍스트입니다.")]
        [SerializeField] private TMP_Text descriptionText;
        [Tooltip("이 슬롯의 챌린지 완료 여부를 표시할 이미지입니다.")]
        [SerializeField] private Image completionImage;
        [Tooltip("이 슬롯의 챌린지 진행도를 (현재/최대) 형식으로 표시할 TMP 텍스트입니다.")]
        [SerializeField] private TMP_Text progressText;
        [Tooltip("결과 화면 공개 연출용 스윕 이미지입니다. Pivot X = 0으로 설정해야 왼쪽에서부터 Width가 커지는 것처럼 보입니다.")]
        [SerializeField] private Image sweepImage;

        private ChallengeDefinitionSO boundDefinition;
        private LocalizedString.ChangeHandler changeHandler;
        private Color originalDescriptionColor;
        private bool originalDescriptionColorCached;

        private void Awake()
        {
            changeHandler = SetDescriptionText;
            CacheOriginalDescriptionColor();
        }

        private void OnDisable()
        {
            Unbind();
        }

        public void Show(ChallengeDefinitionSO definition, string bossId, Sprite completedSprite, Sprite incompleteSprite, Color clearedTextColor)
        {
            CacheOriginalDescriptionColor();
            SetSweepWidth(0f);
            SetDescriptionText(definition.Description);
            BindLocalizedDescription(definition);

            bool completed = GameSaveManager.IsChallengeCompleted(GameSaveManager.BuildChallengeSaveKey(bossId, definition.ChallengeId));
            SetDescriptionColor(completed ? clearedTextColor : originalDescriptionColor);
            SetCompletionSprite(completed ? completedSprite : incompleteSprite);

            SetProgressText(definition.ShowProgress ? $"({definition.GetCurrentProgress(bossId)}/{definition.MaxProgress})" : string.Empty);
        }

        public void Clear()
        {
            Unbind();
            CacheOriginalDescriptionColor();
            SetDescriptionColor(originalDescriptionColor);
            SetDescriptionText(string.Empty);
            SetCompletionSprite(null);
            SetProgressText(string.Empty);
            SetSweepWidth(0f);
        }

        // BossChallengePanel의 연출 코루틴에서 yield return으로 직접 실행합니다 (슬롯이 완전히 끝나야 다음 슬롯이 시작되도록).
        public IEnumerator PlayRevealCoroutine(
            ChallengeDefinitionSO definition,
            string bossId,
            Sprite completedSprite,
            Sprite incompleteSprite,
            Color defaultTextColor,
            Color clearedTextColor,
            Color notClearedTextColor,
            float sweepWidth,
            float growSeconds,
            float shrinkSeconds)
        {
            SetDescriptionText(definition.Description);
            BindLocalizedDescription(definition);
            SetDescriptionColor(defaultTextColor);
            SetCompletionSprite(null);
            SetProgressText(string.Empty);
            SetSweepWidth(0f);

            yield return AnimateSweepWidth(0f, sweepWidth, growSeconds);

            bool completed = GameSaveManager.IsChallengeCompleted(GameSaveManager.BuildChallengeSaveKey(bossId, definition.ChallengeId));
            SetDescriptionColor(completed ? clearedTextColor : notClearedTextColor);
            SetCompletionSprite(completed ? completedSprite : incompleteSprite);
            SetProgressText(definition.ShowProgress ? $"({definition.GetCurrentProgress(bossId)}/{definition.MaxProgress})" : string.Empty);

            yield return AnimateSweepWidth(sweepWidth, 0f, shrinkSeconds);
        }

        private IEnumerator AnimateSweepWidth(float from, float to, float seconds)
        {
            if (sweepImage == null)
            {
                yield break;
            }

            if (seconds <= 0f)
            {
                SetSweepWidth(to);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                SetSweepWidth(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / seconds)));
                yield return null;
            }

            SetSweepWidth(to);
        }

        private void CacheOriginalDescriptionColor()
        {
            if (descriptionText != null && !originalDescriptionColorCached)
            {
                originalDescriptionColor = descriptionText.color;
                originalDescriptionColorCached = true;
            }
        }

        private void BindLocalizedDescription(ChallengeDefinitionSO definition)
        {
            if (!definition.HasLocalizedDescription)
            {
                return;
            }

            boundDefinition = definition;
            definition.LocalizedDescription.StringChanged += changeHandler;
            definition.LocalizedDescription.RefreshString();
        }

        private void Unbind()
        {
            if (boundDefinition != null && boundDefinition.HasLocalizedDescription)
            {
                boundDefinition.LocalizedDescription.StringChanged -= changeHandler;
            }

            boundDefinition = null;
        }

        private void SetDescriptionText(string value)
        {
            if (descriptionText != null)
            {
                descriptionText.text = value;
            }
        }

        private void SetDescriptionColor(Color color)
        {
            if (descriptionText != null)
            {
                descriptionText.color = color;
            }
        }

        private void SetCompletionSprite(Sprite sprite)
        {
            if (completionImage == null)
            {
                return;
            }

            completionImage.sprite = sprite;
            completionImage.enabled = sprite != null;
        }

        private void SetProgressText(string value)
        {
            if (progressText != null)
            {
                progressText.text = value;
            }
        }

        private void SetSweepWidth(float width)
        {
            if (sweepImage == null)
            {
                return;
            }

            Vector2 size = sweepImage.rectTransform.sizeDelta;
            size.x = width;
            sweepImage.rectTransform.sizeDelta = size;
        }
    }
}
