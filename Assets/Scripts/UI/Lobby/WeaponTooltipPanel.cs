using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Week14.Audio;
using Week14.Enemy;
using Week14.Weapons;

namespace Week14.UI
{
    public sealed class WeaponTooltipPanel : MonoBehaviour
    {
        [SerializeField] private RectTransform panelRect;
        [Tooltip("패널이 펼쳐질 때 재생할 SFX의 SoundLibrary ID입니다. 비워두면 재생하지 않습니다.")]
        [BossGraphSfxId]
        [SerializeField] private string showSfxId;
        [Tooltip("패널이 닫힐 때 재생할 SFX의 SoundLibrary ID입니다. 비워두면 재생하지 않습니다.")]
        [BossGraphSfxId]
        [SerializeField] private string hideSfxId;
        [Tooltip("같은 SFX가 이 시간(초) 안에 다시 재생되려 하면 막습니다. 마우스 잔떨림 등으로 Show/Hide가 짧은 간격에 반복될 때 중복 재생을 막는 용도입니다.")]
        [SerializeField, Min(0f)] private float sfxDebounceSeconds = 0.1f;
        [Tooltip("실제로 위치를 이동시킬 툴팁 전체(배경, 텍스트 등 포함)의 최상위 RectTransform입니다. 비워두면 이 컴포넌트가 붙은 오브젝트를 사용합니다.")]
        [SerializeField] private RectTransform rootRect;
        [Tooltip("월드<->스크린 좌표 변환에 사용할 카메라입니다. 비워두면 Camera.main을 사용합니다. (World Space Canvas 기준 위치 계산용)")]
        [SerializeField] private Camera worldCamera;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text maxAmmoText;
        [SerializeField] private TMP_Text parryingRangeText;
        [SerializeField] private TMP_Text bulletDamageText;
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
        [Tooltip("패널이 펼쳐진 후 이 배열 순서대로 하나씩 나타납니다. 가장 마지막에 나타나야 할 항목을 맨 마지막에 넣으세요.")]
        [SerializeField] private GameObject[] revealTargets = Array.Empty<GameObject>();
        [Tooltip("가장 먼저 FillAmount가 0에서 1로 채워지며 나타나는 이미지입니다.")]
        [SerializeField] private Image firstFillRevealImage;
        [Tooltip("첫 번째 이미지의 FillAmount가 0에서 1로 채워지는 데 걸리는 시간입니다.")]
        [SerializeField, Min(0f)] private float firstFillRevealSeconds = 0.15f;
        [Tooltip("첫 번째 연출이 끝난 후, 패널의 높이가 커지기 전에 FillAmount가 0에서 1로 채워지며 나타나는 이미지입니다.")]
        [SerializeField] private Image fillRevealImage;
        [Tooltip("FillAmount가 0에서 1로 채워지는 데 걸리는 시간입니다.")]
        [SerializeField, Min(0f)] private float fillRevealSeconds = 0.15f;

        public static WeaponTooltipPanel Instance { get; private set; }

        private Coroutine showRoutine;
        private Coroutine growRoutine;
        private Coroutine revealRoutine;
        private float lastShowSfxTime = float.NegativeInfinity;
        private float lastHideSfxTime = float.NegativeInfinity;

        private void Awake()
        {
            Instance = this;

            if (panelRect == null)
            {
                panelRect = transform as RectTransform;
            }

            if (rootRect == null)
            {
                rootRect = transform as RectTransform;
            }

            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }

            HideImmediate();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void Show(BaseWeaponSO weapon, Transform anchor)
        {
            if (weapon == null)
            {
                return;
            }

            if (nameText != null)
            {
                nameText.text = weapon.DisplayName;
            }

            if (descriptionText != null)
            {
                descriptionText.text = weapon.Description;
            }

            if (maxAmmoText != null)
            {
                maxAmmoText.text = $"최대 탄환: {weapon.MaxAmmo}";
            }

            if (parryingRangeText != null)
            {
                parryingRangeText.text = $"패링 범위: x{weapon.ParryingRange:0.##}";
            }

            if (bulletDamageText != null)
            {
                int[] damagePerAmmoStep = weapon.DamagePerAmmoStep ?? Array.Empty<int>();
                bulletDamageText.text = $"탄환 데미지: {string.Join("-", damagePerAmmoStep.Reverse())}";
            }

            if (iconImage != null)
            {
                iconImage.sprite = weapon.Icon;
                iconImage.enabled = weapon.Icon != null;
            }

            PositionAt(anchor);

            if (!string.IsNullOrEmpty(showSfxId))
            {
                float now = Time.unscaledTime;
                if (now - lastShowSfxTime >= sfxDebounceSeconds)
                {
                    lastShowSfxTime = now;
                    SoundManager.PlaySfx(showSfxId);
                }
            }

            SetRevealTargetsActive(false);
            ResetFillImages();
            StopShowRoutine();
            showRoutine = StartCoroutine(ShowSequenceRoutine());
        }

        public void Hide()
        {
            if (!string.IsNullOrEmpty(hideSfxId))
            {
                float now = Time.unscaledTime;
                if (now - lastHideSfxTime >= sfxDebounceSeconds)
                {
                    lastHideSfxTime = now;
                    SoundManager.PlaySfx(hideSfxId);
                }
            }

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

        private void PositionAt(Transform anchor)
        {
            if (anchor == null || rootRect == null)
            {
                return;
            }

            if (worldCamera == null)
            {
                rootRect.position = anchor.position + new Vector3(anchorOffset.x, anchorOffset.y, 0f);
                return;
            }

            // World Space Canvas라 rootRect.position은 월드 좌표라서, 오프셋/가장자리 클램프는
            // 화면 픽셀 공간으로 변환해서 계산한 뒤 다시 월드 좌표로 되돌린다.
            Vector3 anchorScreenPoint = worldCamera.WorldToScreenPoint(anchor.position);
            Vector3 targetScreenPoint = anchorScreenPoint + new Vector3(anchorOffset.x, anchorOffset.y, 0f);
            Vector3 clampedScreenPoint = ClampToScreen(targetScreenPoint);
            rootRect.position = worldCamera.ScreenToWorldPoint(
                new Vector3(clampedScreenPoint.x, clampedScreenPoint.y, anchorScreenPoint.z));
        }

        private Vector3 ClampToScreen(Vector3 screenPoint)
        {
            float pixelsPerWorldUnit = worldCamera != null && worldCamera.orthographicSize > 0f
                ? Screen.height / (worldCamera.orthographicSize * 2f)
                : 1f;
            float width = rootRect.rect.width * rootRect.lossyScale.x * pixelsPerWorldUnit;
            float height = expandedHeight * rootRect.lossyScale.y * pixelsPerWorldUnit;
            Vector2 pivot = rootRect.pivot;

            float minX = screenEdgePadding + (pivot.x * width);
            float maxX = Screen.width - screenEdgePadding - ((1f - pivot.x) * width);
            float minY = screenEdgePadding + (pivot.y * height);
            float maxY = Screen.height - screenEdgePadding - ((1f - pivot.y) * height);

            if (maxX >= minX)
            {
                screenPoint.x = Mathf.Clamp(screenPoint.x, minX, maxX);
            }

            if (maxY >= minY)
            {
                screenPoint.y = Mathf.Clamp(screenPoint.y, minY, maxY);
            }

            return screenPoint;
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
