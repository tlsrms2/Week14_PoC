using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Week14.UI
{
    public sealed class LobbyMenuController : MonoBehaviour
    {
        [Tooltip("로비에 작게 떠있는 보스 선택 버튼입니다. 클릭하면 이 오브젝트의 위치가 화면 중앙으로 이동합니다.")]
        [SerializeField] private RectTransform bossRoot;
        [Tooltip("bossRoot의 자식으로, 항상 활성화돼 있는 보스 패널 내용물입니다. 에디터에 세팅해둔 현재 localScale을 평소(닫힘) 크기로 그대로 사용합니다.")]
        [SerializeField] private RectTransform bossPanelContent;
        [Tooltip("패널이 열렸을 때 bossPanelContent가 가질 localScale입니다. 평소 크기보다 커야 확대되는 게 보입니다.")]
        [SerializeField, Min(0.01f)] private float bossOpenScale = 4f;

        [Tooltip("로비에 작게 떠있는 로드아웃 선택 버튼입니다. 클릭하면 이 오브젝트의 위치가 화면 중앙으로 이동합니다.")]
        [SerializeField] private RectTransform loadoutRoot;
        [Tooltip("loadoutRoot의 자식으로, 항상 활성화돼 있는 로드아웃 패널 내용물입니다. 에디터에 세팅해둔 현재 localScale을 평소(닫힘) 크기로 그대로 사용합니다.")]
        [SerializeField] private RectTransform loadoutPanelContent;
        [Tooltip("패널이 열렸을 때 loadoutPanelContent가 가질 localScale입니다. 평소 크기보다 커야 확대되는 게 보입니다.")]
        [SerializeField, Min(0.01f)] private float loadoutOpenScale = 4f;

        [Tooltip("로비 배경(BG 등)이 들어있는 컨테이너입니다. 패널이 열릴 때 이 전체가 클릭한 버튼 쪽으로 확대됩니다. bossRoot/loadoutRoot는 여기 들어있지 않아야 합니다.")]
        [SerializeField] private RectTransform backgroundRoot;
        [Tooltip("작은 상태에서 전체 크기로 확대/축소되는 데 걸리는 시간입니다.")]
        [SerializeField, Min(0f)] private float zoomSeconds = 0.25f;
        [Tooltip("패널이 열릴 때 배경이 확대되는 배율입니다. 1 = 확대 안 함.")]
        [SerializeField, Min(1f)] private float backgroundZoomScale = 1.6f;

        [Tooltip("보스/로드아웃 패널이 확대되어 있을 때만 보여줄 CloseAll 버튼들입니다. 평소(닫힘) 상태에서는 모두 비활성화됩니다.")]
        [SerializeField] private GameObject[] closeAllButtons = Array.Empty<GameObject>();

        private Vector2 bossRestPosition;
        private Vector2 loadoutRestPosition;
        private float bossRestScale = 1f;
        private float loadoutRestScale = 1f;
        private Vector2 backgroundBasePosition;

        private RectTransform activeRoot;
        private RectTransform activeContent;
        private Vector2 activeRestPosition;
        private float activeRestScale;
        private RectTransform activeOtherRoot;
        private Vector2 activeOtherRestPosition;
        private Coroutine zoomRoutine;

        private void Awake()
        {
            if (bossRoot != null)
            {
                bossRestPosition = bossRoot.anchoredPosition;
            }

            if (loadoutRoot != null)
            {
                loadoutRestPosition = loadoutRoot.anchoredPosition;
            }

            // 평소(닫힘) 크기는 강제로 정하지 않고, 에디터에 이미 세팅해둔 현재 localScale을 그대로 기준으로 쓴다.
            if (bossPanelContent != null)
            {
                bossRestScale = bossPanelContent.localScale.x;
            }

            if (loadoutPanelContent != null)
            {
                loadoutRestScale = loadoutPanelContent.localScale.x;
            }

            if (backgroundRoot != null)
            {
                backgroundBasePosition = backgroundRoot.anchoredPosition;
            }

            CloseAllImmediate();
        }

        public void OpenBossSelect()
        {
            Open(bossRoot, bossPanelContent, bossRestPosition, bossRestScale, bossOpenScale, loadoutRoot, loadoutRestPosition);
        }

        public void OpenLoadoutSelect()
        {
            Open(loadoutRoot, loadoutPanelContent, loadoutRestPosition, loadoutRestScale, loadoutOpenScale, bossRoot, bossRestPosition);
        }

        public void CloseAll()
        {
            if (activeRoot == null)
            {
                return;
            }

            RectTransform root = activeRoot;
            RectTransform content = activeContent;
            Vector2 targetPosition = activeRestPosition;
            float targetScale = activeRestScale;
            RectTransform otherRoot = activeOtherRoot;
            Vector2 otherRestPosition = activeOtherRestPosition;

            activeRoot = null;
            activeContent = null;
            activeOtherRoot = null;

            SetInteractable(root, true);
            SetContentInteractable(content, false);
            SetHoverScaleSuppressed(root, false);
            SetHoverScaleSuppressed(content, false);
            SetInteractable(otherRoot, true);
            SetHoverScaleSuppressed(otherRoot, false);
            SetCloseAllButtonVisible(false);
            PlayZoom(root, content, targetPosition, targetScale, 1f, backgroundBasePosition, otherRoot, otherRestPosition);
        }

        private void CloseAllImmediate()
        {
            StopZoom();
            activeRoot = null;
            activeContent = null;
            activeOtherRoot = null;

            ResetImmediate(bossRoot, bossPanelContent, bossRestPosition, bossRestScale);
            ResetImmediate(loadoutRoot, loadoutPanelContent, loadoutRestPosition, loadoutRestScale);

            if (backgroundRoot != null)
            {
                backgroundRoot.localScale = Vector3.one;
                backgroundRoot.anchoredPosition = backgroundBasePosition;
            }

            SetCloseAllButtonVisible(false);
        }

        private void SetCloseAllButtonVisible(bool visible)
        {
            if (closeAllButtons == null)
            {
                return;
            }

            for (int i = 0; i < closeAllButtons.Length; i++)
            {
                if (closeAllButtons[i] != null)
                {
                    closeAllButtons[i].SetActive(visible);
                }
            }
        }

        private static void ResetImmediate(RectTransform root, RectTransform content, Vector2 restPosition, float restScale)
        {
            if (root != null)
            {
                root.localScale = Vector3.one;
                root.anchoredPosition = restPosition;
                SetInteractable(root, true);
            }

            if (content != null)
            {
                content.localScale = new Vector3(restScale, restScale, 1f);
                SetContentInteractable(content, false);
            }
        }

        private void Open(
            RectTransform root,
            RectTransform content,
            Vector2 restPosition,
            float restScale,
            float openScale,
            RectTransform otherRoot,
            Vector2 otherRestPosition)
        {
            if (root == null)
            {
                return;
            }

            if (activeRoot != null && activeRoot != root)
            {
                CloseAll();
            }

            activeRoot = root;
            activeContent = content;
            activeRestPosition = restPosition;
            activeRestScale = restScale;
            activeOtherRoot = otherRoot;
            activeOtherRestPosition = otherRestPosition;

            SetInteractable(root, false);
            SetContentInteractable(content, true);
            SetHoverScaleSuppressed(root, true);
            SetHoverScaleSuppressed(content, true);
            SetInteractable(otherRoot, false);
            SetHoverScaleSuppressed(otherRoot, true);
            SetCloseAllButtonVisible(true);

            // 배경 전체가 클릭한 버튼 지점을 향해 확대되도록, 버튼의 평소 위치(focal)를 기준으로
            // 배경의 목표 anchoredPosition을 계산한다. 확대가 끝나는 시점엔 focal 지점이
            // 화면 정중앙(0,0)에 오도록 한다: screenPos = position + focal * scale = 0 -> position = -focal * scale.
            Vector2 backgroundTargetPosition = -restPosition * backgroundZoomScale;

            PlayZoom(root, content, Vector2.zero, openScale, backgroundZoomScale, backgroundTargetPosition, otherRoot, otherRestPosition);
        }

        private void PlayZoom(
            RectTransform root,
            RectTransform content,
            Vector2 rootTargetPosition,
            float contentTargetScale,
            float backgroundTargetScale,
            Vector2 backgroundTargetPosition,
            RectTransform otherRoot,
            Vector2 otherRestPosition)
        {
            StopZoom();

            if (root == null)
            {
                return;
            }

            zoomRoutine = StartCoroutine(ZoomRoutine(
                root,
                content,
                rootTargetPosition,
                contentTargetScale,
                backgroundTargetScale,
                backgroundTargetPosition,
                otherRoot,
                otherRestPosition));
        }

        private void StopZoom()
        {
            if (zoomRoutine != null)
            {
                StopCoroutine(zoomRoutine);
                zoomRoutine = null;
            }
        }

        private IEnumerator ZoomRoutine(
            RectTransform root,
            RectTransform content,
            Vector2 rootTargetPosition,
            float contentTargetScale,
            float backgroundTargetScale,
            Vector2 backgroundTargetPosition,
            RectTransform otherRoot,
            Vector2 otherRestPosition)
        {
            Vector2 rootStartPosition = root.anchoredPosition;
            float contentStartScale = content != null ? content.localScale.x : 1f;
            float backgroundStartScale = backgroundRoot != null ? backgroundRoot.localScale.x : 1f;
            Vector2 backgroundStartPosition = backgroundRoot != null ? backgroundRoot.anchoredPosition : Vector2.zero;
            float t = 0f;

            while (zoomSeconds > 0f && t < zoomSeconds)
            {
                t += Time.unscaledDeltaTime;
                float ratio = t / zoomSeconds;
                root.anchoredPosition = Vector2.Lerp(rootStartPosition, rootTargetPosition, ratio);

                if (content != null)
                {
                    float scale = Mathf.Lerp(contentStartScale, contentTargetScale, ratio);
                    content.localScale = new Vector3(scale, scale, 1f);
                }

                float bgScale = Mathf.Lerp(backgroundStartScale, backgroundTargetScale, ratio);
                Vector2 bgPosition = Vector2.Lerp(backgroundStartPosition, backgroundTargetPosition, ratio);
                ApplyBackgroundTransform(backgroundRoot, bgScale, bgPosition);
                ApplyOtherRootTransform(otherRoot, otherRestPosition, bgScale, bgPosition);

                yield return null;
            }

            root.anchoredPosition = rootTargetPosition;

            if (content != null)
            {
                content.localScale = new Vector3(contentTargetScale, contentTargetScale, 1f);
            }

            ApplyBackgroundTransform(backgroundRoot, backgroundTargetScale, backgroundTargetPosition);
            ApplyOtherRootTransform(otherRoot, otherRestPosition, backgroundTargetScale, backgroundTargetPosition);

            zoomRoutine = null;
        }

        private static void ApplyBackgroundTransform(RectTransform background, float scale, Vector2 position)
        {
            if (background == null)
            {
                return;
            }

            background.localScale = new Vector3(scale, scale, 1f);
            background.anchoredPosition = position;
        }

        private static void ApplyOtherRootTransform(RectTransform otherRoot, Vector2 otherRestPosition, float backgroundScale, Vector2 backgroundPosition)
        {
            if (otherRoot == null)
            {
                return;
            }

            // otherRoot는 backgroundRoot의 자식은 아니지만, 같은 줌에 묶인 것처럼
            // backgroundRoot와 똑같은 비율로 같이 움직이고 커지게 한다.
            otherRoot.localScale = new Vector3(backgroundScale, backgroundScale, 1f);
            otherRoot.anchoredPosition = backgroundPosition + (otherRestPosition * backgroundScale);
        }

        private static void SetInteractable(RectTransform root, bool interactable)
        {
            if (root == null)
            {
                return;
            }

            Button button = root.GetComponent<Button>();
            if (button != null)
            {
                button.interactable = interactable;
            }
        }

        private static void SetContentInteractable(RectTransform content, bool interactable)
        {
            if (content == null)
            {
                return;
            }

            CanvasGroup group = content.GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.blocksRaycasts = interactable;
                group.interactable = interactable;
            }
        }

        private static void SetHoverScaleSuppressed(RectTransform target, bool suppressed)
        {
            if (target == null)
            {
                return;
            }

            HoverDarkenImage hoverDarken = target.GetComponent<HoverDarkenImage>();
            if (hoverDarken != null)
            {
                hoverDarken.SetScaleEffectSuppressed(suppressed);
            }
        }
    }
}
