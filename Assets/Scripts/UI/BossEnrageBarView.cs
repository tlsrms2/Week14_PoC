using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Week14.Combat;
using Week14.Enemy;

namespace Week14.UI
{
    public sealed class BossEnrageBarView : MonoBehaviour
    {
        [SerializeField] private BossAI target;
        [Tooltip("광폭화 진행도를 표시할 Filled 타입 Image입니다.")]
        [SerializeField] private Image fillImage;
        [Tooltip("플레이어 최대 탄환 감소량을 표시할 텍스트입니다.")]
        [SerializeField] private TextMeshProUGUI reductionText;
        [SerializeField] private Color phase0FillColor = new(1f, 0.48f, 0.08f, 1f);
        [SerializeField] private Color phase1FillColor = new(0.95f, 0.04f, 0.03f, 1f);
        [Tooltip("광폭화 단계에 따라 바뀌는 이미지입니다.")]
        [SerializeField] private Image phaseImage;
        [Tooltip("광폭화 단계별 이미지입니다. 인덱스 0이 단계0(평상시), 1이 단계1, 2가 단계2(최종 광폭화)에 대응합니다.")]
        [SerializeField] private Sprite[] phaseSprites;

        private void OnEnable()
        {
            Subscribe();
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void SetTarget(BossAI nextTarget)
        {
            if (target == nextTarget)
            {
                Refresh();
                return;
            }

            Unsubscribe();
            target = nextTarget;
            Subscribe();
            Refresh();
        }

        public void Refresh()
        {
            if (target == null)
            {
                SetValue(0, 0f);
                return;
            }

            SetValue(target.CurrentEnragePhase, target.CurrentEnrageProgress);
        }

        private void Subscribe()
        {
            if (target != null)
            {
                target.EnrageChanged += HandleEnrageChanged;
            }
        }

        private void Unsubscribe()
        {
            if (target != null)
            {
                target.EnrageChanged -= HandleEnrageChanged;
            }
        }

        private void HandleEnrageChanged(int phase, float progress)
        {
            SetValue(phase, progress);
        }

        private void SetValue(int phase, float progress)
        {
            if (fillImage != null)
            {
                fillImage.color = phase <= 0 ? phase0FillColor : phase1FillColor;
                fillImage.fillAmount = phase >= 2 ? 1f : Mathf.Clamp01(progress);
            }

            UpdatePhaseImage(phase);
            UpdateReductionText();
        }

        private void UpdatePhaseImage(int phase)
        {
            if (phaseImage == null || phaseSprites == null || phaseSprites.Length == 0)
            {
                return;
            }

            Sprite sprite = phaseSprites[Mathf.Clamp(phase, 0, phaseSprites.Length - 1)];
            phaseImage.sprite = sprite;
            phaseImage.enabled = sprite != null;
        }

        private void UpdateReductionText()
        {
            if (reductionText == null)
            {
                return;
            }

            PlayerCombatController player = PlayerCombatController.Active;
            if (player == null || player.Config == null || player.Bullets == null)
            {
                reductionText.text = string.Empty;
                return;
            }

            int reduction = player.Config.MaxBullets - player.Bullets.MaxBullets;
            reductionText.text = reduction > 0 ? $"-{reduction}" : string.Empty;
        }
    }
}
