using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Week14.Audio;
using Week14.Enemy;

namespace Week14.UI
{
    public sealed class LobbyMenuController : MonoBehaviour, IBackClosable
    {
        [Tooltip("로비 씬이 시작될 때 재생할 BGM의 SoundLibrary ID입니다. 비워두면 재생하지 않습니다.")]
        [BossGraphBgmId]
        [SerializeField] private string lobbyBgmId;

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
        [Tooltip("보스 패널이 확대될 때 꺼지고, 닫히면 다시 켜질 오브젝트들입니다.")]
        [SerializeField] private GameObject[] bossObjectsToHideWhenOpen = Array.Empty<GameObject>();
        [Tooltip("bossRoot에 마우스를 올리면 켜지는 오브젝트입니다. 보스 패널이 열려있는 동안에는 호버 여부와 상관없이 계속 켜져 있습니다.")]
        [SerializeField] private GameObject bossHoverHighlight;

        [Tooltip("로비에 떠있는 로드아웃 선택 버튼(클릭 대상)입니다.")]
        [SerializeField] private Transform loadoutRoot;
        [Tooltip("loadoutRoot에 대응하는 로드아웃 패널 콘텐츠 루트입니다. 이 아래에 있는 모든 Collider2D가 패널이 열렸을 때만 활성화됩니다.")]
        [SerializeField] private Transform loadoutPanelContent;
        [Tooltip("로드아웃 패널이 열렸을 때 카메라가 바라볼 월드 포지션입니다.")]
        [SerializeField] private Transform loadoutFocusPoint;
        [Tooltip("로드아웃 패널이 열렸을 때 카메라의 orthographic size입니다.")]
        [SerializeField, Min(0.01f)] private float loadoutOpenOrthographicSize = 2f;
        [Tooltip("로드아웃 패널이 확대될 때 꺼지고, 닫히면 다시 켜질 오브젝트들입니다.")]
        [SerializeField] private GameObject[] loadoutObjectsToHideWhenOpen = Array.Empty<GameObject>();
        [Tooltip("loadoutRoot에 마우스를 올리면 켜지는 오브젝트입니다. 로드아웃 패널이 열려있는 동안에는 호버 여부와 상관없이 계속 켜져 있습니다.")]
        [SerializeField] private GameObject loadoutHoverHighlight;

        [Tooltip("평소(닫힘) 상태로 돌아가거나 줌인하는 데 걸리는 시간입니다.")]
        [SerializeField, Min(0f)] private float zoomSeconds = 0.25f;

        [Tooltip("보스/로드아웃 패널이 확대되어 있을 때만 보여줄 CloseAll 버튼들입니다. 평소(닫힘) 상태에서는 모두 비활성화됩니다.")]
        [SerializeField] private GameObject[] closeAllButtons = Array.Empty<GameObject>();

        private Vector3 restCameraPosition;
        private float restOrthographicSize = 5f;

        private Transform activeRoot;
        private Transform activeContent;
        private Transform activeOtherRoot;
        private GameObject[] activeObjectsToHide;
        private Coroutine zoomRoutine;
        private bool bossRootHovering;
        private bool loadoutRootHovering;

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

            if (!string.IsNullOrEmpty(lobbyBgmId))
            {
                SoundManager.PlayBgm(lobbyBgmId);
            }

            CloseAllImmediate();
        }

        private void OnDestroy()
        {
            UIBackStack.Remove(this);

            if (!string.IsNullOrEmpty(lobbyBgmId))
            {
                SoundManager.StopBgm();
            }
        }

        public void OnBossRootPointerEnter()
        {
            bossRootHovering = true;
            RefreshHoverHighlight(bossHoverHighlight, bossRootHovering, bossRoot);
        }

        public void OnBossRootPointerExit()
        {
            bossRootHovering = false;
            RefreshHoverHighlight(bossHoverHighlight, bossRootHovering, bossRoot);
        }

        public void OnLoadoutRootPointerEnter()
        {
            loadoutRootHovering = true;
            RefreshHoverHighlight(loadoutHoverHighlight, loadoutRootHovering, loadoutRoot);
        }

        public void OnLoadoutRootPointerExit()
        {
            loadoutRootHovering = false;
            RefreshHoverHighlight(loadoutHoverHighlight, loadoutRootHovering, loadoutRoot);
        }

        public void OpenBossSelect()
        {
            Open(bossRoot, bossPanelContent, bossFocusPoint, bossOpenOrthographicSize, loadoutRoot, bossObjectsToHideWhenOpen);
        }

        public void OpenLoadoutSelect()
        {
            Open(loadoutRoot, loadoutPanelContent, loadoutFocusPoint, loadoutOpenOrthographicSize, bossRoot, loadoutObjectsToHideWhenOpen);
        }

        public bool CloseByBack()
        {
            if (activeRoot == null)
            {
                return false;
            }

            CloseAll();
            return true;
        }

        public void CloseAll()
        {
            if (activeRoot == null)
            {
                UIBackStack.Remove(this);
                return;
            }

            Transform root = activeRoot;
            Transform content = activeContent;
            Transform otherRoot = activeOtherRoot;
            GameObject[] objectsToHide = activeObjectsToHide;

            activeRoot = null;
            activeContent = null;
            activeOtherRoot = null;
            activeObjectsToHide = null;

            SetInteractable(root, true);
            SetContentInteractable(content, false);
            SetHoverScaleSuppressed(root, false);
            SetHoverScaleSuppressed(content, false);
            SetInteractable(otherRoot, true);
            SetHoverScaleSuppressed(otherRoot, false);
            SetObjectsActive(objectsToHide, true);
            SetCloseAllButtonVisible(false);
            RefreshAllHoverHighlights();
            PlayZoom(restCameraPosition, restOrthographicSize);
            UIBackStack.Remove(this);
        }

        private void CloseAllImmediate()
        {
            UIBackStack.Remove(this);
            StopZoom();
            activeRoot = null;
            activeContent = null;
            activeOtherRoot = null;
            activeObjectsToHide = null;

            SetInteractable(bossRoot, true);
            SetContentInteractable(bossPanelContent, false);
            SetObjectsActive(bossObjectsToHideWhenOpen, true);
            SetInteractable(loadoutRoot, true);
            SetContentInteractable(loadoutPanelContent, false);
            SetObjectsActive(loadoutObjectsToHideWhenOpen, true);

            ApplyCameraTransform(restCameraPosition, restOrthographicSize);
            SetCloseAllButtonVisible(false);
            RefreshAllHoverHighlights();
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
            Transform otherRoot,
            GameObject[] objectsToHide)
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
            activeObjectsToHide = objectsToHide;

            SetInteractable(root, false);
            SetContentInteractable(content, true);
            SetHoverScaleSuppressed(root, true);
            SetHoverScaleSuppressed(content, true);
            SetInteractable(otherRoot, false);
            SetHoverScaleSuppressed(otherRoot, true);
            SetObjectsActive(objectsToHide, false);
            SetCloseAllButtonVisible(true);
            UIBackStack.Push(this);

            // otherRoot의 콜라이더가 방금 꺼져서 PointerExit가 다시는 안 올 수 있다.
            // 그 전에 호버 중이었다면 hovering 플래그가 영영 true로 남아있게 되니 여기서 같이 꺼준다.
            if (otherRoot == bossRoot)
            {
                bossRootHovering = false;
            }
            else if (otherRoot == loadoutRoot)
            {
                loadoutRootHovering = false;
            }

            RefreshAllHoverHighlights();

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

            // bossRoot/loadoutRoot가 이제 Button이 아니라 Collider2D + EventTrigger로 클릭을 받는
            // 월드 오브젝트라, 반대쪽 패널이 열려있는 동안엔 콜라이더 자체를 꺼서 클릭/호버가 안 먹게 막는다.
            Collider2D collider = root.GetComponent<Collider2D>();
            if (collider != null)
            {
                collider.enabled = interactable;
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

        private void RefreshHoverHighlight(GameObject highlight, bool hovering, Transform root)
        {
            if (highlight == null)
            {
                return;
            }

            // 패널이 열려있는 동안에는 마우스가 버튼 위를 벗어나도(카메라가 줌인되며 버튼 위치가 바뀌므로
            // 호버가 쉽게 풀린다) 계속 켜져 있어야 하므로, 호버 여부와 "지금 이 root로 열려있는지"를 같이 본다.
            // 단, 반대쪽 패널이 열려있는 동안에는(activeRoot가 다른 root) 이쪽을 호버해도 켜지면 안 된다.
            bool isThisOpen = activeRoot == root;
            bool hoverAllowed = hovering && activeRoot == null;
            highlight.SetActive(isThisOpen || hoverAllowed);
        }

        private void RefreshAllHoverHighlights()
        {
            RefreshHoverHighlight(bossHoverHighlight, bossRootHovering, bossRoot);
            RefreshHoverHighlight(loadoutHoverHighlight, loadoutRootHovering, loadoutRoot);
        }

        private static void SetObjectsActive(GameObject[] objects, bool active)
        {
            if (objects == null)
            {
                return;
            }

            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] != null)
                {
                    objects[i].SetActive(active);
                }
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
