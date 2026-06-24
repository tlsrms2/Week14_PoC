using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Week14.Skills;

namespace Week14.UI
{
    public sealed class LoadoutTooltipPanel : MonoBehaviour
    {
        [SerializeField] private RectTransform panelRect;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text requiredStackText;
        [SerializeField] private Image iconImage;
        [Tooltip("호버한 버튼 기준 이 패널이 나타날 오프셋입니다.")]
        [SerializeField] private Vector2 anchorOffset = new(0f, 16f);
        [Tooltip("패널이 화면 가장자리에서 벗어나지 않도록 둘 최소 여백입니다.")]
        [SerializeField, Min(0f)] private float screenEdgePadding = 8f;
        [Tooltip("패널이 다 펼쳐졌을 때의 높이입니다.")]
        [SerializeField, Min(0f)] private float expandedHeight = 200f;
        [Tooltip("높이 0에서 펼쳐진 높이까지 커지는 데 걸리는 시간입니다. 빠르게 보이도록 짧게 설정합니다.")]
        [SerializeField, Min(0f)] private float growSeconds = 0.12f;
        [Tooltip("패널이 다 펼쳐진 후 첫 항목이 나타나기까지의 지연 시간입니다.")]
        [SerializeField, Min(0f)] private float initialRevealDelaySeconds = 0.06f;
        [Tooltip("패널이 다 펼쳐진 후 항목들이 하나씩 나타나는 간격입니다.")]
        [SerializeField, Min(0f)] private float revealStaggerSeconds = 0.06f;

        public static LoadoutTooltipPanel Instance { get; private set; }

        private GameObject[] revealTargets;
        private Coroutine growRoutine;
        private Coroutine revealRoutine;

        private void Awake()
        {
            Instance = this;

            if (panelRect == null)
            {
                panelRect = transform as RectTransform;
            }

            CacheRevealTargets();
            HideImmediate();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void Show(BaseSkillSO skill, RectTransform anchor)
        {
            if (skill == null)
            {
                return;
            }

            if (nameText != null)
            {
                nameText.text = skill.DisplayName;
            }

            if (descriptionText != null)
            {
                descriptionText.text = skill.Description;
            }

            if (requiredStackText != null)
            {
                requiredStackText.text = $"필요 스택: {skill.RequiredStack}";
            }

            if (iconImage != null)
            {
                iconImage.sprite = skill.Icon;
                iconImage.enabled = skill.Icon != null;
            }

            PositionAt(anchor);

            SetRevealTargetsActive(false);
            PlayGrow(expandedHeight, revealAfterGrow: true);
        }

        public void Hide()
        {
            StopRevealRoutine();
            SetRevealTargetsActive(false);
            PlayGrow(0f, revealAfterGrow: false);
        }

        public void HideImmediate()
        {
            if (growRoutine != null)
            {
                StopCoroutine(growRoutine);
                growRoutine = null;
            }

            StopRevealRoutine();
            SetRevealTargetsActive(false);
            SetHeight(0f);
        }

        private void PositionAt(RectTransform anchor)
        {
            if (anchor == null || panelRect == null)
            {
                return;
            }

            float signedOffsetX = anchor.position.x >= Screen.width / 2f
                ? Mathf.Abs(anchorOffset.x)
                : -Mathf.Abs(anchorOffset.x);
            Vector3 targetPosition = anchor.position + new Vector3(signedOffsetX, anchorOffset.y, 0f);
            panelRect.position = ClampToScreen(targetPosition);
        }

        private Vector3 ClampToScreen(Vector3 position)
        {
            float width = panelRect.rect.width * panelRect.lossyScale.x;
            float height = expandedHeight * panelRect.lossyScale.y;
            Vector2 pivot = panelRect.pivot;

            float minX = screenEdgePadding + (pivot.x * width);
            float maxX = Screen.width - screenEdgePadding - ((1f - pivot.x) * width);
            float minY = screenEdgePadding + (pivot.y * height);
            float maxY = Screen.height - screenEdgePadding - ((1f - pivot.y) * height);

            if (maxX >= minX)
            {
                position.x = Mathf.Clamp(position.x, minX, maxX);
            }

            if (maxY >= minY)
            {
                position.y = Mathf.Clamp(position.y, minY, maxY);
            }

            return position;
        }

        private void CacheRevealTargets()
        {
            revealTargets = new[]
            {
                iconImage != null ? iconImage.gameObject : null,
                nameText != null ? nameText.gameObject : null,
                requiredStackText != null ? requiredStackText.gameObject : null,
                descriptionText != null ? descriptionText.gameObject : null,
            };
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
