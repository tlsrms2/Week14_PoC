using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Week14.Save;

namespace Week14.Bootstrap
{
    [DefaultExecutionOrder(-10000)]
    public sealed class ReferenceViewportController : MonoBehaviour
    {
        private static readonly Rect FullViewportRect = new(0f, 0f, 1f, 1f);
        private const float TargetAspect = FullScreenModeResolver.DefaultContentWidth
            / (float)FullScreenModeResolver.DefaultContentHeight;

        private static ReferenceViewportController instance;

        private Camera backgroundCamera;
        private Camera trackedMainCamera;
        private Rect trackedOriginalRect = FullViewportRect;
        private Rect lastAppliedRect = new(-1f, -1f, -1f, -1f);
        private int lastScreenWidth = -1;
        private int lastScreenHeight = -1;
        private bool hasAppliedToTrackedCamera;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (instance != null)
            {
                return;
            }

            ReferenceViewportController existing = FindFirstObjectByType<ReferenceViewportController>(
                FindObjectsInactive.Include);
            if (existing != null)
            {
                instance = existing;
                return;
            }

            var controllerObject = new GameObject(nameof(ReferenceViewportController));
            controllerObject.AddComponent<ReferenceViewportController>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureBackgroundCamera();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }

            RestoreTrackedCameraRect();
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TrackMainCamera(null);
            ApplyForActiveScene(force: true);
        }

        private void LateUpdate()
        {
            ApplyForActiveScene(force: false);
        }

        private void ApplyForActiveScene(bool force)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!ShouldApplyToScene(activeScene.name))
            {
                RestoreTrackedCameraRect();
                SetBackgroundCameraActive(false);
                return;
            }

            Camera mainCamera = ResolveMainCamera();
            if (mainCamera == null)
            {
                SetBackgroundCameraActive(false);
                return;
            }

            if (trackedMainCamera != mainCamera)
            {
                TrackMainCamera(mainCamera);
                force = true;
            }

            Rect targetRect = CalculateReferenceViewportRect(Screen.width, Screen.height);
            if (!force
                && hasAppliedToTrackedCamera
                && lastScreenWidth == Screen.width
                && lastScreenHeight == Screen.height
                && Approximately(lastAppliedRect, targetRect))
            {
                UpdateBackgroundCameraDepth(mainCamera);
                return;
            }

            mainCamera.rect = targetRect;
            hasAppliedToTrackedCamera = true;
            lastAppliedRect = targetRect;
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;

            EnsureBackgroundCamera();
            SetBackgroundCameraActive(true);
            UpdateBackgroundCameraDepth(mainCamera);
        }

        private static bool ShouldApplyToScene(string sceneName)
        {
            return sceneName is "TitleScene"
                or "CutsceneScene"
                or "TutorialScene"
                or "LobbyScene"
                or "MainScene"
                or "Boss_Assassin"
                or "Boss_Conductor"
                or "Boss_Hacker"
                or "Boss_Muscle"
                or "EndingScene";
        }

        private static Rect CalculateReferenceViewportRect(int screenWidth, int screenHeight)
        {
            if (screenWidth <= 0 || screenHeight <= 0)
            {
                return FullViewportRect;
            }

            float screenAspect = screenWidth / (float)screenHeight;
            if (screenAspect > TargetAspect)
            {
                float widthRatio = TargetAspect / screenAspect;
                return new Rect((1f - widthRatio) * 0.5f, 0f, widthRatio, 1f);
            }

            float heightRatio = screenAspect / TargetAspect;
            return new Rect(0f, (1f - heightRatio) * 0.5f, 1f, heightRatio);
        }

        private Camera ResolveMainCamera()
        {
            if (trackedMainCamera != null && trackedMainCamera.CompareTag("MainCamera"))
            {
                return trackedMainCamera;
            }

            GameObject mainCameraObject = GameObject.FindGameObjectWithTag("MainCamera");
            return mainCameraObject != null ? mainCameraObject.GetComponent<Camera>() : null;
        }

        private void TrackMainCamera(Camera nextCamera)
        {
            RestoreTrackedCameraRect();
            trackedMainCamera = nextCamera;
            trackedOriginalRect = nextCamera != null ? nextCamera.rect : FullViewportRect;
            hasAppliedToTrackedCamera = false;
            lastAppliedRect = new Rect(-1f, -1f, -1f, -1f);
            lastScreenWidth = -1;
            lastScreenHeight = -1;
        }

        private void RestoreTrackedCameraRect()
        {
            if (trackedMainCamera != null && hasAppliedToTrackedCamera)
            {
                trackedMainCamera.rect = trackedOriginalRect;
            }

            hasAppliedToTrackedCamera = false;
        }

        private void EnsureBackgroundCamera()
        {
            if (backgroundCamera != null)
            {
                return;
            }

            var backgroundObject = new GameObject("ReferenceViewportBackgroundCamera");
            backgroundObject.transform.SetParent(transform, false);

            backgroundCamera = backgroundObject.AddComponent<Camera>();
            backgroundCamera.clearFlags = CameraClearFlags.SolidColor;
            backgroundCamera.backgroundColor = Color.black;
            backgroundCamera.cullingMask = 0;
            backgroundCamera.rect = FullViewportRect;
            backgroundCamera.useOcclusionCulling = false;
            backgroundCamera.allowHDR = false;
            backgroundCamera.allowMSAA = false;

            UniversalAdditionalCameraData cameraData = backgroundCamera.GetUniversalAdditionalCameraData();
            cameraData.renderType = CameraRenderType.Base;
            cameraData.renderPostProcessing = false;
            cameraData.volumeLayerMask = 0;
        }

        private void SetBackgroundCameraActive(bool active)
        {
            if (backgroundCamera != null)
            {
                backgroundCamera.gameObject.SetActive(active);
            }
        }

        private void UpdateBackgroundCameraDepth(Camera mainCamera)
        {
            if (backgroundCamera == null || mainCamera == null)
            {
                return;
            }

            backgroundCamera.targetDisplay = mainCamera.targetDisplay;
            backgroundCamera.depth = mainCamera.depth - 1f;
        }

        private static bool Approximately(Rect lhs, Rect rhs)
        {
            return Mathf.Approximately(lhs.x, rhs.x)
                && Mathf.Approximately(lhs.y, rhs.y)
                && Mathf.Approximately(lhs.width, rhs.width)
                && Mathf.Approximately(lhs.height, rhs.height);
        }
    }
}
