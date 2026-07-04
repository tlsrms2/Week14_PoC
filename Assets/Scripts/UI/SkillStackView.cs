using UnityEngine;
using UnityEngine.UI;
using Week14.Skills;

namespace Week14.UI
{
    public sealed class SkillStackView : MonoBehaviour
    {
        [Tooltip("스킬 스택 비율을 아래에서 위로 채울 Image입니다.")]
        [SerializeField] private Image fillImage;
        [Tooltip("쿨타임이 아직 남았을 때의 게이지 색상입니다.")]
        [SerializeField] private Color normalColor = Color.white;
        [Tooltip("쿨타임이 끝나 사용 가능할 때의 게이지 색상입니다.")]
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

            target.CooldownChanged += HandleCooldownChanged;
        }

        private void Unsubscribe()
        {
            if (target == null)
            {
                return;
            }

            target.CooldownChanged -= HandleCooldownChanged;
        }

        private void HandleCooldownChanged(float remaining, float duration)
        {
            SetFillAmount(remaining, duration);
        }

        private void Refresh()
        {
            if (target == null)
            {
                SetFillAmount(0f, -1f);
                return;
            }

            SetFillAmount(target.CooldownRemaining, target.CooldownDuration);
        }

        private void SetFillAmount(float remaining, float duration)
        {
            if (fillImage == null)
            {
                return;
            }

            PrepareFillImage();

            if (duration < 0f)
            {
                // 장착된 액티브 스킬이 없는 상태 - 게이지를 비워서 사용 불가임을 표시합니다.
                fillImage.fillAmount = 0f;
                fillImage.color = normalColor;
                return;
            }

            bool ready = remaining <= 0f;
            fillImage.fillAmount = duration > 0f ? Mathf.Clamp01(1f - (remaining / duration)) : 1f;
            fillImage.color = ready ? fullColor : normalColor;
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
