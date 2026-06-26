using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Week14.UI
{
    public sealed class LobbyMenuController : MonoBehaviour
    {
        [Tooltip("실제로 줌인/줌아웃시킬 카메라입니다. 비워두면 Camera.main을 사용합니다.")]
        [SerializeField] private Camera targetCamera;

        [Tooltip("로비에 떠있는 보스 선택 버튼(클릭 대상)입니다. Button/HoverDarkenImage가 붙어있어야 합니다.")]
        [SerializeField] private Transform bossRoot;
        [Tooltip("bossRoot에 대응하는 보스 패널 콘텐츠 루트입니다. 이 아래에 있는 모든 Collider2D가 패널이 열렸을 때만 활성화됩니다.")]
        [SerializeField] private Transform bossPanelContent;
        [Tooltip("보스 패널이 열렸을 때 카메라가 바라볼 월드 포지션입니다. 씬에 빈 오브젝트로 만들어 배치하세요.")]
        [SerializeField] private Transform bossFocusPoint;
        [Tooltip("보스 패널이 열렸을 때 카메라의 orthographic size입니다. 작을수록 더 확대됩니다.")]
        [SerializeField, Min(0.01f)] private float bossOpenOrthographicSize = 2f;

        [Tooltip("로비에 떠있는 로드아웃 선택 버튼(클릭 대상)입니다.")]
        [SerializeField] private Transform loadoutRoot;
        [Tooltip("loadoutRoot에 대응하는 로드아웃 패널 콘텐츠 루트입니다. 이 아래에 있는 모든 Collider2D가 패널이 열렸을 때만 활성화됩니다.")]
        [SerializeField] private Transform loadoutPanelContent;
        [Tooltip("로드아웃 패널이 열렸을 때 카메라가 바라볼 월드 포지션입니다.")]
        [SerializeField] private Transform loadoutFocusPoint;
        [Tooltip("로드아웃 패널이 열렸을 때 카메라의 orthographic size입니다.")]
        [SerializeField, Min(0.01f)] private float loadoutOpenOrthographicSize = 2f;

        [Tooltip("평소(닫힘) 상태로 돌아가거나 줌인하는 데 걸리는 시간입니다.")]
        [SerializeField, Min(0f)] private float zoomSeconds = 0.25f;

        [Tooltip("보스/로드아웃 패널이 확대되어 있을 때만 보여줄 CloseAll 버튼들입니다. 평소(닫힘) 상태에서는 모두 비활성화됩니다.")]
        [SerializeField] private GameObject[] closeAllButtons = Array.Empty<GameObject>();

        private Vector3 restCameraPosition;
        private float restOrthographicSize = 5f;

        private Transform activeRoot;
        private Transform activeContent;
        private Transform activeOtherRoot;
        private Coroutine zoomRoutine;

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (targetCamera != null)
            {
                restCameraPosition = targetCamera.transform.position;
                restOrthographicSize = targetCamera.orthographicSize;
            }

            CloseAllImmediate();
        }

        public void OpenBossSelect()
        {
            Open(bossRoot, bossPanelContent, bossFocusPoint, bossOpenOrthographicSize, loadoutRoot);
        }

        public void OpenLoadoutSelect()
        {
            Open(loadoutRoot, loadoutPanelContent, loadoutFocusPoint, loadoutOpenOrthographicSize, bossRoot);
        }

        public void CloseAll()
        {
            if (activeRoot == null)
            {
                return;
            }

            Transform root = activeRoot;
            Transform content = activeContent;
            Transform otherRoot = activeOtherRoot;

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
            PlayZoom(restCameraPosition, restOrthographicSize);
        }

        private void CloseAllImmediate()
        {
            StopZoom();
            activeRoot = null;
            activeContent = null;
            activeOtherRoot = null;

            SetInteractable(bossRoot, true);
            SetContentInteractable(bossPanelContent, false);
            SetInteractable(loadoutRoot, true);
            SetContentInteractable(loadoutPanelContent, false);

            ApplyCameraTransform(restCameraPosition, restOrthographicSize);
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

        private void Open(
            Transform root,
            Transform content,
            Transform focusPoint,
            float openOrthographicSize,
            Transform otherRoot)
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
            activeOtherRoot = otherRoot;

            SetInteractable(root, false);
            SetContentInteractable(content, true);
            SetHoverScaleSuppressed(root, true);
            SetHoverScaleSuppressed(content, true);
            SetInteractable(otherRoot, false);
            SetHoverScaleSuppressed(otherRoot, true);
            SetCloseAllButtonVisible(true);

            Vector3 targetPosition = focusPoint != null
                ? new Vector3(focusPoint.position.x, focusPoint.position.y, restCameraPosition.z)
                : restCameraPosition;

            PlayZoom(targetPosition, openOrthographicSize);
        }

        private void PlayZoom(Vector3 targetPosition, float targetOrthographicSize)
        {
            StopZoom();

            if (targetCamera == null)
            {
                return;
            }

            zoomRoutine = StartCoroutine(ZoomRoutine(targetPosition, targetOrthographicSize));
        }

        private void StopZoom()
        {
            if (zoomRoutine != null)
            {
                StopCoroutine(zoomRoutine);
                zoomRoutine = null;
            }
        }

        private IEnumerator ZoomRoutine(Vector3 targetPosition, float targetOrthographicSize)
        {
            Vector3 startPosition = targetCamera.transform.position;
            float startOrthographicSize = targetCamera.orthographicSize;
            float t = 0f;

            while (zoomSeconds > 0f && t < zoomSeconds)
            {
                t += Time.unscaledDeltaTime;
                float ratio = t / zoomSeconds;
                ApplyCameraTransform(
                    Vector3.Lerp(startPosition, targetPosition, ratio),
                    Mathf.Lerp(startOrthographicSize, targetOrthographicSize, ratio));
                yield return null;
            }

            ApplyCameraTransform(targetPosition, targetOrthographicSize);
            zoomRoutine = null;
        }

        private void ApplyCameraTransform(Vector3 position, float orthographicSize)
        {
            if (targetCamera == null)
            {
                return;
            }

            targetCamera.transform.position = position;
            targetCamera.orthographicSize = orthographicSize;
        }

        private static void SetInteractable(Transform root, bool interactable)
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

        private static void SetContentInteractable(Transform content, bool interactable)
        {
            if (content == null)
            {
                return;
            }

            // 아이콘의 Collider2D.enabled를 여기서 직접 건드리면, 아이콘 자신의 OnEnable/Start(잠금 상태 갱신)가
            // 실행되는 시점과 경쟁해서 어느 쪽이 나중에 실행되느냐에 따라 값이 다시 뒤집힐 수 있다.
            // 대신 각 아이콘에게 "패널이 열렸다"는 사실만 알려주고, 콜라이더 활성화 여부는 아이콘이
            // (잠금 해제 여부 + 패널 열림 여부) 둘을 합쳐서 직접 계산하게 한다.
            IPanelGatedInteractable[] gated = content.GetComponentsInChildren<IPanelGatedInteractable>(true);
            for (int i = 0; i < gated.Length; i++)
            {
                gated[i].SetPanelOpen(interactable);
            }
        }

        private static void SetHoverScaleSuppressed(Transform target, bool suppressed)
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
