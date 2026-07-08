using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Week14.Combat
{
    // Main Camera에 붙이면 "Player" 레이어를 별도의 오버레이 카메라로 분리해서 그립니다.
    // TimeSlowScreenFx 같은 화면 전체 틴트(Volume) 효과가 이 카메라 본체에는 적용되지만,
    // 오버레이 카메라로 그려지는 플레이어(와 그 잔상)는 항상 원래 색 그대로 보이게 됩니다.
    [RequireComponent(typeof(Camera))]
    public sealed class PlayerOverlayCameraTag : MonoBehaviour
    {
        private const string PlayerLayerName = "Player";
        private const string LightingLayerName = "Lighting";

        private Camera worldCamera;
        private Camera overlayCamera;

        private void Awake()
        {
            worldCamera = GetComponent<Camera>();

            int playerLayer = LayerMask.NameToLayer(PlayerLayerName);
            if (playerLayer < 0)
            {
                Debug.LogWarning($"'{PlayerLayerName}' 레이어가 없어 {nameof(PlayerOverlayCameraTag)}를 비활성화합니다.", this);
                enabled = false;
                return;
            }

            worldCamera.cullingMask &= ~(1 << playerLayer);
            overlayCamera = CreateOverlayCamera(playerLayer);
        }

        // URP에서는 "이전 카메라가 그린 화면 위에 겹쳐 그리기"가 레거시 Camera.clearFlags/depth로는
        // 동작하지 않고, 반드시 Overlay 타입으로 만들어 Base 카메라의 스택에 등록해야 합니다.
        // 이걸 빼먹으면 오버레이 카메라가 매 프레임 자체적으로 화면을 지워버려 화면이 까맣게 나옵니다.
        private Camera CreateOverlayCamera(int playerLayer)
        {
            GameObject overlayObject = new GameObject("PlayerOverlayCamera");
            overlayObject.transform.SetParent(transform, false);

            // Light2D도 카메라의 cullingMask 대상이라, 조명(특히 Global Light)이 있는 레이어를
            // 같이 포함하지 않으면 오버레이 카메라가 빛을 못 받아 플레이어가 검게 그려집니다.
            int cullingMask = 1 << playerLayer;
            int lightingLayer = LayerMask.NameToLayer(LightingLayerName);
            if (lightingLayer >= 0)
            {
                cullingMask |= 1 << lightingLayer;
            }

            Camera cam = overlayObject.AddComponent<Camera>();
            cam.CopyFrom(worldCamera);
            cam.cullingMask = cullingMask;

            UniversalAdditionalCameraData overlayData = cam.GetUniversalAdditionalCameraData();
            overlayData.renderType = CameraRenderType.Overlay;
            overlayData.renderPostProcessing = false;
            overlayData.volumeLayerMask = 0;

            UniversalAdditionalCameraData worldData = worldCamera.GetUniversalAdditionalCameraData();
            worldData.renderType = CameraRenderType.Base;
            worldData.renderPostProcessing = true;
            worldData.cameraStack.Add(cam);

            return cam;
        }

        private void LateUpdate()
        {
            if (overlayCamera == null || worldCamera == null)
            {
                return;
            }

            overlayCamera.orthographic = worldCamera.orthographic;
            overlayCamera.orthographicSize = worldCamera.orthographicSize;
            overlayCamera.fieldOfView = worldCamera.fieldOfView;
            overlayCamera.nearClipPlane = worldCamera.nearClipPlane;
            overlayCamera.farClipPlane = worldCamera.farClipPlane;
            overlayCamera.rect = worldCamera.rect;
        }
    }
}
