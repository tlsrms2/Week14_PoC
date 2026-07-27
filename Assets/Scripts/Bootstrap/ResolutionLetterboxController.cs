using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Week14.Save;

namespace Week14.Bootstrap
{
    // 전체화면에서 모니터 실제 비율과 설정 해상도의 비율이 다르면 화면을 늘려서(stretch) 채우는 대신,
    // 메인 카메라의 rect를 화면 중앙의 올바른 비율 영역으로 좁히고 나머지를 검은색으로 채운다(레터박스/필러박스).
    // OS/드라이버가 borderless를 어떻게 늘리든 상관없이 Unity가 그리는 프레임 자체에서 비율을 보정하므로 항상 동작한다.
    public sealed class ResolutionLetterboxController : MonoBehaviour
    {
        // SettingsManager.ResolutionWidth/Height가 아직 저장되지 않은 상태(설정을 한 번도 안 바꾼 최초 실행 등)에서
        // 화면 자기 자신과 비교해버리면 항상 "일치"로 나와 레터박스가 무력화되므로, 그 대신 이 게임의 실제 디자인
        // 기준 해상도(FullScreenModeResolver.DefaultContentWidth/Height, UICanvas 기준 해상도와 동일)를 기준으로 삼는다.
        private const float DefaultTargetAspect = FullScreenModeResolver.DefaultContentWidth / (float)FullScreenModeResolver.DefaultContentHeight;

        private static ResolutionLetterboxController instance;

        private Camera backgroundCamera;
        private Camera mainCamera;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (instance != null)
            {
                return;
            }

            var controllerObject = new GameObject(nameof(ResolutionLetterboxController));
            instance = controllerObject.AddComponent<ResolutionLetterboxController>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            CreateBackgroundCamera();
            AcquireMainCamera();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }

            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            AcquireMainCamera();
        }

        private void AcquireMainCamera()
        {
            GameObject mainCameraObject = GameObject.FindGameObjectWithTag("MainCamera");
            mainCamera = mainCameraObject != null ? mainCameraObject.GetComponent<Camera>() : null;
        }

        private void CreateBackgroundCamera()
        {
            var backgroundObject = new GameObject("LetterboxBackgroundCamera");
            backgroundObject.transform.SetParent(transform, false);

            backgroundCamera = backgroundObject.AddComponent<Camera>();
            backgroundCamera.clearFlags = CameraClearFlags.SolidColor;
            backgroundCamera.backgroundColor = Color.black;
            backgroundCamera.cullingMask = 0;
            backgroundCamera.rect = new Rect(0f, 0f, 1f, 1f);
            backgroundCamera.useOcclusionCulling = false;
            backgroundCamera.allowHDR = false;
            backgroundCamera.allowMSAA = false;

            UniversalAdditionalCameraData cameraData = backgroundCamera.GetUniversalAdditionalCameraData();
            cameraData.renderType = CameraRenderType.Base;
            cameraData.renderPostProcessing = false;
            cameraData.volumeLayerMask = 0;
        }

        private void LateUpdate()
        {
            if (mainCamera == null)
            {
                AcquireMainCamera();
                if (mainCamera == null)
                {
                    return;
                }
            }

            ApplyLetterbox();
        }

        private void ApplyLetterbox()
        {
            int screenWidth = Screen.width;
            int screenHeight = Screen.height;
            if (screenWidth <= 0 || screenHeight <= 0)
            {
                return;
            }

            int targetWidth = SettingsManager.ResolutionWidth;
            int targetHeight = SettingsManager.ResolutionHeight;
            float targetAspect = targetWidth > 0 && targetHeight > 0
                ? targetWidth / (float)targetHeight
                : DefaultTargetAspect;
            float screenAspect = screenWidth / (float)screenHeight;

            Rect rect;
            if (screenAspect > targetAspect)
            {
                float widthRatio = targetAspect / screenAspect;
                rect = new Rect((1f - widthRatio) * 0.5f, 0f, widthRatio, 1f);
            }
            else
            {
                float heightRatio = screenAspect / targetAspect;
                rect = new Rect(0f, (1f - heightRatio) * 0.5f, 1f, heightRatio);
            }

            mainCamera.rect = rect;

            if (backgroundCamera != null)
            {
                backgroundCamera.depth = mainCamera.depth - 1f;
            }
        }
    }
}
