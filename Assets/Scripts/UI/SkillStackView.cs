using UnityEngine;
using UnityEngine.UI;
using Week14.Skills;

namespace Week14.UI
{
    public sealed class SkillStackView : MonoBehaviour
    {
        [Tooltip("스킬 스택 비율을 아래에서 위로 채울 Image입니다.")]
        [SerializeField] private Image fillImage;
        [Tooltip("스택이 요구치보다 낮을 때의 게이지 색상입니다.")]
        [SerializeField] private Color normalColor = Color.white;
        [Tooltip("스택이 요구치에 도달했을 때의 게이지 색상입니다.")]
        [SerializeField] private Color fullColor = Color.yellow;

        private SkillLoadoutManager target;

        private void Awake()
        {
            PrepareFillImage();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            PrepareFillImage();
        }
#endif

        private void OnEnable()
        {
            PrepareFillImage();
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
                Refresh();
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

            PrepareFillImage();
            fillImage.fillAmount = Mathf.Clamp01(current / (float)Mathf.Max(1, required));
            fillImage.color = required > 0 && current >= required ? fullColor : normalColor;
        }

        private void PrepareFillImage()
        {
            if (fillImage == null)
            {
                return;
            }

            fillImage.raycastTarget = false;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Vertical;
            fillImage.fillOrigin = (int)Image.OriginVertical.Bottom;
        }
    }
}
