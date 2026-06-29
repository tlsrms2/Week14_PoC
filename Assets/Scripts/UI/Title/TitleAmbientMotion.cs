using UnityEngine;

namespace Week14.UI
{
    [DisallowMultipleComponent]
    public sealed class TitleAmbientMotion : MonoBehaviour
    {
        private const float Tau = Mathf.PI * 2f;

        [Tooltip("미세하게 움직일 대상입니다. 비워두면 이 스크립트가 붙은 오브젝트를 움직입니다.")]
        [SerializeField] private Transform target;
        [Tooltip("일시정지나 Time.timeScale의 영향을 받지 않고 계속 움직이게 할지 여부입니다.")]
        [SerializeField] private bool useUnscaledTime = true;
        [Tooltip("전체 움직임 강도입니다. 0이면 움직이지 않고, 1이면 아래 설정값 그대로 적용됩니다.")]
        [SerializeField, Min(0f)] private float motionScale = 1f;

        [Header("위치 흔들림")]
        [Tooltip("대상의 위치를 미세하게 흔들지 여부입니다.")]
        [SerializeField] private bool animatePosition = true;
        [Tooltip("각 축별 최대 이동 거리입니다. 타이틀 캐릭터는 X/Y를 작게, Z는 보통 0으로 둡니다.")]
        [SerializeField] private Vector3 positionAmplitude = new Vector3(0.025f, 0.045f, 0f);
        [Tooltip("각 축별 흔들림 속도입니다. 값이 클수록 더 빠르게 움직입니다.")]
        [SerializeField] private Vector3 positionFrequency = new Vector3(0.27f, 0.36f, 0f);

        [Header("회전 흔들림")]
        [Tooltip("대상의 X/Y 회전을 미세하게 흔들지 여부입니다. Z 회전은 적용하지 않습니다.")]
        [SerializeField] private bool animateRotation = true;
        [Tooltip("X/Y 축별 최대 회전 각도입니다. Perspective 카메라에서 캐릭터에 입체감을 줄 때 사용합니다.")]
        [SerializeField] private Vector2 rotationAmplitude = new Vector2(1.4f, 1.1f);
        [Tooltip("X/Y 축별 회전 흔들림 속도입니다. 값이 클수록 더 빠르게 기울어집니다.")]
        [SerializeField] private Vector2 rotationFrequency = new Vector2(0.19f, 0.23f);

        [Header("위상")]
        [Tooltip("활성화될 때마다 흔들림 시작 지점을 무작위로 바꿀지 여부입니다.")]
        [SerializeField] private bool randomizePhaseOnEnable = true;
        [Tooltip("무작위 위상을 사용하지 않을 때 적용할 흔들림 시작 지점입니다.")]
        [SerializeField, Range(0f, Tau)] private float phaseOffset;
        [Tooltip("비활성화될 때 시작 위치와 회전으로 되돌릴지 여부입니다.")]
        [SerializeField] private bool restoreRestPoseOnDisable = true;

        private Transform activeTarget;
        private Vector3 restLocalPosition;
        private Quaternion restLocalRotation;
        private float startTime;
        private float runtimePhase;

        private void Awake()
        {
            ResolveTarget();
            CaptureRestPose();
        }

        private void OnEnable()
        {
            ResolveTarget();
            CaptureRestPose();

            startTime = GetTime();
            runtimePhase = randomizePhaseOnEnable ? Random.Range(0f, Tau) : phaseOffset;
            ApplyMotion();
        }

        private void Update()
        {
            ApplyMotion();
        }

        private void OnDisable()
        {
            if (restoreRestPoseOnDisable)
            {
                RestoreRestPose();
            }
        }

        public void CaptureRestPose()
        {
            if (activeTarget == null)
            {
                return;
            }

            restLocalPosition = activeTarget.localPosition;
            restLocalRotation = activeTarget.localRotation;
        }

        public void RestoreRestPose()
        {
            if (activeTarget == null)
            {
                return;
            }

            activeTarget.localPosition = restLocalPosition;
            activeTarget.localRotation = restLocalRotation;
        }

        private void ResolveTarget()
        {
            activeTarget = target != null ? target : transform;
        }

        private void ApplyMotion()
        {
            if (activeTarget == null)
            {
                return;
            }

            float elapsed = GetTime() - startTime;
            float scale = Mathf.Max(0f, motionScale);

            if (animatePosition)
            {
                activeTarget.localPosition = restLocalPosition + GetPositionOffset(elapsed, scale);
            }

            if (animateRotation)
            {
                activeTarget.localRotation = restLocalRotation * GetRotationOffset(elapsed, scale);
            }
        }

        private Vector3 GetPositionOffset(float elapsed, float scale)
        {
            return new Vector3(
                GetSine(elapsed, positionFrequency.x, runtimePhase) * positionAmplitude.x,
                GetSine(elapsed, positionFrequency.y, runtimePhase + 1.71f) * positionAmplitude.y,
                GetSine(elapsed, positionFrequency.z, runtimePhase + 3.17f) * positionAmplitude.z) * scale;
        }

        private Quaternion GetRotationOffset(float elapsed, float scale)
        {
            float x = GetSine(elapsed, rotationFrequency.x, runtimePhase + 0.83f) * rotationAmplitude.x * scale;
            float y = GetSine(elapsed, rotationFrequency.y, runtimePhase + 2.29f) * rotationAmplitude.y * scale;
            return Quaternion.Euler(x, y, 0f);
        }

        private float GetTime()
        {
            return useUnscaledTime ? Time.unscaledTime : Time.time;
        }

        private static float GetSine(float elapsed, float frequency, float phase)
        {
            return Mathf.Sin(elapsed * Tau * Mathf.Max(0f, frequency) + phase);
        }
    }
}
