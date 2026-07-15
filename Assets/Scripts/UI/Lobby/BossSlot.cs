using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Week14.Challenge;
using Week14.GameFlow;
using Week14.Save;

namespace Week14.UI
{
    // 보스 패널의 보스칸 하나입니다. LoadoutWeaponIcon과 동일한 "마지막 호버 유지" 규칙을 따릅니다:
    // 호버하면 BossDescriptionPanel을 갱신하고, 빠져나가도 아무것도 비우지 않습니다(아웃라인/설명패널은
    // 마지막으로 호버한 보스를 그대로 유지합니다). 해금되지 않은 보스는 슬롯 자체를 비활성화해 숨깁니다.
    [RequireComponent(typeof(Image))]
    public sealed class BossSlot : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private BossData bossData;
        [Tooltip("\"모든 챌린지 완료\" 판정에 쓸, 이 보스의 챌린지 목록을 조회할 데이터베이스입니다.")]
        [SerializeField] private ChallengeDatabaseSO database;
        [Tooltip("이 보스가 마지막으로 호버되었을 때 켤 테두리 오브젝트입니다.")]
        [SerializeField] private GameObject hoverOutline;
        [Tooltip("보스 이름을 표시할 텍스트입니다.")]
        [SerializeField] private TMP_Text nameText;
        [Tooltip("클리어/모든 챌린지 완료 상태를 표시할 텍스트입니다. 클리어하지 않았으면 비워둡니다.")]
        [SerializeField] private TMP_Text statusText;
        [Tooltip("클리어했지만 모든 챌린지를 완료하지는 않았을 때 표시할 문구입니다.")]
        [SerializeField] private string clearedLabel = "[Pass]";
        [Tooltip("모든 챌린지를 완료했을 때 표시할 문구입니다.")]
        [SerializeField] private string completedLabel = "[COMPLETED]";
        [SerializeField] private Color clearedColor = Color.white;
        [SerializeField] private Color completedColor = new(1f, 0.85f, 0.3f);
        [Header("Hover Mask Reveal")]
        [Tooltip("마우스 오버 시 폭을 늘려 왼쪽부터 표시할 MaskParent 오브젝트입니다.")]
        [FormerlySerializedAs("hoverMask")]
        [SerializeField] private RectTransform maskParent;
        [Tooltip("MaskParent가 완전히 펼쳐졌을 때의 폭입니다. 0 이하면 자식 RectTransform 폭 중 가장 큰 값을 사용합니다.")]
        [Min(0f)]
        [SerializeField] private float maskParentRevealWidth;
        [Tooltip("MaskParent가 완전히 펼쳐지는 데 걸리는 시간입니다.")]
        [Min(0.01f)]
        [SerializeField] private float hoverMaskRevealDuration = 0.25f;
        [Tooltip("MaskParent 펼침 진행도를 조절하는 커브입니다. 기본값은 처음 느리고 뒤로 갈수록 빨라집니다.")]
        [SerializeField] private AnimationCurve hoverMaskRevealCurve = new(
            new Keyframe(0f, 0f, 0f, 0f),
            new Keyframe(1f, 1f, 2f, 2f));

        private Coroutine hoverMaskRevealRoutine;
        private float hoverMaskRevealProgress;

        private void OnValidate()
        {
            ResolveMaskParent();

            if (bossData == null)
            {
                return;
            }

            Image image = GetComponent<Image>();
            if (image != null)
            {
                image.sprite = bossData.PanelIcon;
            }

            if (nameText != null && !bossData.HasLocalizedBossName)
            {
                nameText.text = bossData.BossName;
            }
        }

        private void OnEnable()
        {
            if (bossData == null || !bossData.IsUnlocked())
            {
                gameObject.SetActive(false);
                return;
            }

            RefreshStatusLabel();
            RefreshNameText();
            ResolveMaskParent();
            SetHoverMaskRevealProgress(0f);
            BossHoverHighlight.HoveredBossIdChanged += HandleHoveredBossChanged;

            // 구독이 SetHovered 호출보다 늦게 시작됐을 수도 있으니, 지금 시점의 값으로 한 번 더 맞춘다.
            HandleHoveredBossChanged(BossHoverHighlight.CurrentHoveredBossId);
        }

        private void OnDisable()
        {
            BossHoverHighlight.HoveredBossIdChanged -= HandleHoveredBossChanged;
            UnbindLocalizedName();
            StopHoverMaskRevealRoutine();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            BossDescriptionPanel.Instance?.Show(bossData);
            PlayHoverMaskReveal(1f);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // 여기서 아무것도 비우지 않는다. 설명 패널과 아웃라인 모두 마지막으로
            // 호버한 보스를 그대로 유지한다.
            PlayHoverMaskReveal(0f);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (bossData == null || !bossData.IsUnlocked() || string.IsNullOrEmpty(bossData.SceneName))
            {
                return;
            }

            GameFlowController.EnterBoss(bossData);
        }

        private void HandleHoveredBossChanged(string hoveredBossId)
        {
            if (hoverOutline != null)
            {
                hoverOutline.SetActive(bossData != null && hoveredBossId == bossData.Id);
            }
        }

        private void ResolveMaskParent()
        {
            if (maskParent == null)
            {
                Transform maskParentTransform = transform.Find("MaskParent");
                if (maskParentTransform != null)
                {
                    maskParent = maskParentTransform as RectTransform;
                }
            }
        }

        private void PlayHoverMaskReveal(float targetProgress)
        {
            ResolveMaskParent();
            if (maskParent == null)
            {
                return;
            }

            maskParent.gameObject.SetActive(true);
            StopHoverMaskRevealRoutine();
            hoverMaskRevealRoutine = StartCoroutine(AnimateHoverMaskReveal(Mathf.Clamp01(targetProgress)));
        }

        private IEnumerator AnimateHoverMaskReveal(float targetProgress)
        {
            float startProgress = hoverMaskRevealProgress;
            float elapsed = 0f;

            while (elapsed < hoverMaskRevealDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalizedTime = Mathf.Clamp01(elapsed / hoverMaskRevealDuration);
                float curveProgress = hoverMaskRevealCurve != null ? hoverMaskRevealCurve.Evaluate(normalizedTime) : normalizedTime;
                SetHoverMaskRevealProgress(Mathf.LerpUnclamped(startProgress, targetProgress, curveProgress));
                yield return null;
            }

            SetHoverMaskRevealProgress(targetProgress);
            hoverMaskRevealRoutine = null;
        }

        private void SetHoverMaskRevealProgress(float progress)
        {
            ResolveMaskParent();
            hoverMaskRevealProgress = Mathf.Clamp01(progress);

            if (maskParent == null)
            {
                return;
            }

            Vector3 localScale = maskParent.localScale;
            localScale.x = 1f;
            maskParent.localScale = localScale;

            Vector2 offsetMin = maskParent.offsetMin;
            Vector2 offsetMax = maskParent.offsetMax;
            maskParent.pivot = new Vector2(0f, maskParent.pivot.y);
            maskParent.offsetMin = offsetMin;
            maskParent.offsetMax = offsetMax;

            Vector2 sizeDelta = maskParent.sizeDelta;
            sizeDelta.x = GetMaskParentRevealWidth() * hoverMaskRevealProgress;
            maskParent.sizeDelta = sizeDelta;

            maskParent.gameObject.SetActive(hoverMaskRevealProgress > 0f);
        }

        private float GetMaskParentRevealWidth()
        {
            if (maskParentRevealWidth > 0f)
            {
                return maskParentRevealWidth;
            }

            float maxChildWidth = 0f;
            for (int i = 0; i < maskParent.childCount; i++)
            {
                if (maskParent.GetChild(i) is RectTransform childRect)
                {
                    maxChildWidth = Mathf.Max(maxChildWidth, childRect.rect.width, childRect.sizeDelta.x);
                }
            }

            if (maxChildWidth > 0f)
            {
                return maxChildWidth;
            }

            return Mathf.Max(maskParent.rect.width, maskParent.sizeDelta.x);
        }

        private void StopHoverMaskRevealRoutine()
        {
            if (hoverMaskRevealRoutine == null)
            {
                return;
            }

            StopCoroutine(hoverMaskRevealRoutine);
            hoverMaskRevealRoutine = null;
        }

        private void RefreshNameText()
        {
            if (nameText == null || bossData == null)
            {
                return;
            }

            nameText.text = bossData.HasLocalizedBossName ? string.Empty : bossData.BossName;
            LoadoutSelectedSkillPanelLocalization.BindLocalizedString(bossData.LocalizedBossName, bossData.HasLocalizedBossName, SetNameText);
        }

        private void UnbindLocalizedName()
        {
            if (bossData == null)
            {
                return;
            }

            LoadoutSelectedSkillPanelLocalization.UnbindLocalizedString(bossData.LocalizedBossName, bossData.HasLocalizedBossName, SetNameText);
        }

        private void SetNameText(string value)
        {
            if (nameText != null)
            {
                nameText.text = value;
            }
        }

        private void RefreshStatusLabel()
        {
            if (statusText == null || bossData == null)
            {
                return;
            }

            if (!GameSaveManager.IsCleared(bossData.Id))
            {
                statusText.text = string.Empty;
                return;
            }

            if (AreAllChallengesCompleted(bossData.Id))
            {
                statusText.text = completedLabel;
                statusText.color = completedColor;
            }
            else
            {
                statusText.text = clearedLabel;
                statusText.color = clearedColor;
            }
        }

        private bool AreAllChallengesCompleted(string bossId)
        {
            if (database == null)
            {
                return false;
            }

            bool hasAny = false;
            foreach (ChallengeDefinitionSO definition in database.ForBoss(bossId))
            {
                hasAny = true;
                string saveKey = GameSaveManager.BuildChallengeSaveKey(bossId, definition.ChallengeId);
                if (!GameSaveManager.IsChallengeCompleted(saveKey))
                {
                    return false;
                }
            }

            return hasAny;
        }
    }
}
