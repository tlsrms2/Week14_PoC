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

        // PixelBlockRevealView가 자기 연출 도중에 이 Graphic들의 실제 color(알파 포함)를 직접 건드리기
        // 때문에, SyncColorsTo 시점에 descriptionText.color 등을 "그대로 읽어서" 넘기면 이미 연출이
        // 알파 0으로 꺼놓은 값을 캐치해버릴 수 있다. 그래서 우리가 마지막으로 "의도한" 색을 별도로
        // 기억해뒀다가 그 값을 넘긴다.
        private Color lastIntendedDescriptionColor;
        private Color lastIntendedProgressColor;
        private Color lastIntendedSweepColor;
        private Color lastIntendedCompletionColor = Color.white;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnDisable()
        {
            Unbind();
        }

        public void Show(ChallengeDefinitionSO definition, string bossId, Sprite completedSprite, Sprite incompleteSprite, Color clearedTextColor)
        {
            EnsureInitialized();
            CacheOriginalDescriptionColor();
            SetSweepWidth(0f);
            SetDescriptionText(definition.Description);
            BindLocalizedDescription(definition);

            bool completed = GameSaveManager.IsChallengeCompleted(GameSaveManager.BuildChallengeSaveKey(bossId, definition.ChallengeId));
            Color descriptionColor = completed ? clearedTextColor : originalDescriptionColor;
            SetDescriptionColor(descriptionColor);
            SetSweepColor(descriptionColor);
            SetProgressColor(descriptionColor);
            SetCompletionSprite(completed ? completedSprite : incompleteSprite, descriptionColor);

            SetProgressText(definition.ShowProgress ? $"({definition.GetCurrentProgress(bossId)}/{definition.MaxProgress})" : string.Empty);
        }

        // 연출 시작 전, 이번 전투에서 판정될 슬롯을 기본 색상으로 미리 표시해둡니다(설명 텍스트와 아이콘이 곧바로 보이도록). 진행도는 전투 시작 전 값을 보여주고,
        // 판정 색·아이콘·최신 진행도는 PlayRevealCoroutine에서 스윕이 다 지나간 뒤 적용됩니다.
        public void Prime(ChallengeDefinitionSO definition, string bossId, Sprite incompleteSprite, Color defaultColor)
        {
            EnsureInitialized();
            Unbind();
            CacheOriginalDescriptionColor();
            SetDescriptionText(definition.Description);
            BindLocalizedDescription(definition);
            SetDescriptionColor(defaultColor);
            SetSweepColor(defaultColor);
            SetProgressColor(defaultColor);
            SetCompletionSprite(incompleteSprite, defaultColor);
            SetSweepWidth(0f);

            if (definition.ShowProgress)
            {
                string saveKey = GameSaveManager.BuildChallengeSaveKey(bossId, definition.ChallengeId);
                int progressBeforeRun = ChallengeManager.Instance != null
                    ? ChallengeManager.Instance.GetProgressBeforeRun(saveKey, definition.GetCurrentProgress(bossId))
                    : definition.GetCurrentProgress(bossId);
                SetProgressText($"({progressBeforeRun}/{definition.MaxProgress})");
            }
            else
            {
                SetProgressText(string.Empty);
            }
        }

        // PixelBlockRevealView처럼 활성화 시점의 자식 Graphic 색을 스냅샷해서 페이드인하는 연출이 결과창에
        // 붙어있으면, 우리가 방금 세팅한 색(예: 이미 클리어된 챌린지의 초록색)이 그 스냅샷엔 반영되지 않아
        // 페이드인 후 낡은(기본) 색으로 되돌아갈 수 있다. Show()/Prime() 직후 이 메서드로 현재 색을 그
        // 연출 쪽 캐시에도 명시적으로 밀어넣어준다. PixelBlockRevealView는 위치 기반으로 각 Graphic이
        // 드러나는 타이밍을 따로 계산하는데, 슬롯 안 요소들은 위치가 달라도 descriptionText와 같은
        // 타이밍에 같이 나타나야 하므로 타이밍도 descriptionText 기준으로 맞춰준다.
        public void SyncColorsTo(PixelBlockRevealView revealView)
        {
            if (revealView == null || descriptionText == null)
            {
                return;
            }

            revealView.SetOriginalColor(descriptionText, lastIntendedDescriptionColor);

            if (progressText != null)
            {
                revealView.SetOriginalColor(progressText, lastIntendedProgressColor);
                revealView.SyncRevealTiming(descriptionText, progressText);
            }

            if (sweepImage != null)
            {
                revealView.SetOriginalColor(sweepImage, lastIntendedSweepColor);
                revealView.SyncRevealTiming(descriptionText, sweepImage);
            }

            if (completionImage != null)
            {
                revealView.SetOriginalColor(completionImage, lastIntendedCompletionColor);
                revealView.SyncRevealTiming(descriptionText, completionImage);
            }
        }

        public void Clear()
        {
            EnsureInitialized();
            Unbind();
            CacheOriginalDescriptionColor();
            SetDescriptionColor(originalDescriptionColor);
            SetSweepColor(originalDescriptionColor);
            SetProgressColor(originalDescriptionColor);
            SetDescriptionText(string.Empty);
            SetCompletionSprite(null, originalDescriptionColor);
            SetProgressText(string.Empty);
            SetSweepWidth(0f);
        }

        // BossChallengePanel의 연출 코루틴에서 yield return으로 직접 실행합니다 (슬롯이 완전히 끝나야 다음 슬롯이 시작되도록).
        // 호출 전에 Prime()으로 설명 텍스트가 이미 표시되어 있어야 합니다. 스윕 색은 자라나기 시작할 때 바로 판정 색으로 적용되고,
        // 설명 텍스트/아이콘/진행도는 스윕이 다 자란 뒤(가려진 상태에서) 적용됩니다.
        public IEnumerator PlayRevealCoroutine(
            ChallengeDefinitionSO definition,
            string bossId,
            Sprite completedSprite,
            Sprite incompleteSprite,
            Color clearedTextColor,
            Color notClearedTextColor,
            float sweepWidth,
            float growSeconds,
            float shrinkSeconds)
        {
            string saveKey = GameSaveManager.BuildChallengeSaveKey(bossId, definition.ChallengeId);
            bool completed = GameSaveManager.IsChallengeCompleted(saveKey);
            Color resultColor = completed ? clearedTextColor : notClearedTextColor;
            SetSweepColor(resultColor);

            yield return AnimateSweepWidth(0f, sweepWidth, growSeconds);

            SetDescriptionColor(resultColor);
            SetProgressColor(resultColor);
            SetCompletionSprite(completed ? completedSprite : incompleteSprite, resultColor);
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

        private void EnsureInitialized()
        {
            changeHandler ??= SetDescriptionText;
            CacheOriginalDescriptionColor();
        }

        private void BindLocalizedDescription(ChallengeDefinitionSO definition)
        {
            if (!definition.HasLocalizedDescription)
            {
                return;
            }

            EnsureInitialized();
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
            lastIntendedDescriptionColor = color;
            if (descriptionText != null)
            {
                descriptionText.color = color;
            }
        }

        private void SetSweepColor(Color color)
        {
            lastIntendedSweepColor = color;
            if (sweepImage != null)
            {
                sweepImage.color = color;
            }
        }

        private void SetProgressColor(Color color)
        {
            lastIntendedProgressColor = color;
            if (progressText != null)
            {
                progressText.color = color;
            }
        }

        private void SetCompletionSprite(Sprite sprite, Color color)
        {
            if (completionImage == null)
            {
                return;
            }

            lastIntendedCompletionColor = color;
            completionImage.color = color;
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
