using UnityEngine;
using UnityEngine.UI;
using Week14.Skills;

namespace Week14.UI
{
    public sealed class SkillStackView : MonoBehaviour
    {
        [Tooltip("스택 비율(현재 스택 / 요구 스택)을 표시할 Image입니다. Image Type이 Filled여야 합니다.")]
        [SerializeField] private Image fillImage;
        [Tooltip("스택이 요구치보다 적을 때의 색상입니다.")]
        [SerializeField] private Color normalColor = Color.white;
        [Tooltip("스택이 요구치에 도달했을 때(발동 가능)의 색상입니다.")]
        [SerializeField] private Color fullColor = Color.yellow;

        private SkillLoadoutManager target;

        private void OnEnable()
        {
            BindTarget(SkillLoadoutManager.Instance);
        }

        private void OnDisable()
        {
            Unsubscribe();
            target = null;
        }

        private void Update()
        {
            if (target != SkillLoadoutManager.Instance)
            {
                BindTarget(SkillLoadoutManager.Instance);
            }
        }

        private void BindTarget(SkillLoadoutManager nextTarget)
        {
            if (target == nextTarget)
            {
                return;
            }

            Unsubscribe();
            target = nextTarget;
            Subscribe();
            Refresh();
        }

        private void Subscribe()
        {
            if (target == null)
            {
                return;
            }

            target.StackChanged += HandleStackChanged;
        }

        private void Unsubscribe()
        {
            if (target == null)
            {
                return;
            }

            target.StackChanged -= HandleStackChanged;
        }

        private void HandleStackChanged(int current, int required)
        {
            SetFillAmount(current, required);
        }

        private void Refresh()
        {
            if (target == null)
            {
                SetFillAmount(0, 1);
                return;
            }

            SetFillAmount(target.CurrentStack, target.RequiredStack);
        }

        private void SetFillAmount(int current, int required)
        {
            if (fillImage == null)
            {
                return;
            }

            fillImage.fillAmount = Mathf.Clamp01(current / (float)Mathf.Max(1, required));
            fillImage.color = current >= required ? fullColor : normalColor;
        }
    }
}
