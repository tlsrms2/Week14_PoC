using System;
using UnityEngine;
using Week14.Enemy;

namespace Week14.Combat
{
    public enum CommonExecutionCuePoint
    {
        [InspectorName("레터박스 완료 후")]
        AfterLetterbox = 0,
        [InspectorName("연속 사격 시작")]
        FlourishStart = 1,
        [InspectorName("연속 사격 각 발")]
        FlourishShot = 2,
        [InspectorName("마무리 사격 직전")]
        BeforePowerShot = 3,
        [InspectorName("마무리 사격 순간")]
        PowerShot = 4,
        [InspectorName("타격 연출 후")]
        AfterImpact = 5,
        [InspectorName("중간 처형 종료 직전")]
        SequenceEnd = 6
    }

    public enum CommonExecutionCueType
    {
        [InspectorName("사운드")]
        Sound = 0,
        [InspectorName("슬로우 모션")]
        SlowMotion = 1,
        [InspectorName("카메라 포커스")]
        CameraFocus = 2
    }

    public enum CommonExecutionFocusTarget
    {
        [InspectorName("플레이어와 보스 중간")]
        Midpoint = 0,
        [InspectorName("플레이어")]
        Player = 1,
        [InspectorName("보스")]
        Boss = 2,
        [InspectorName("보스 위치 + 오프셋")]
        BossOffset = 3,
        [InspectorName("마무리 탄환 경로")]
        FinalShotProjectile = 4
    }

    [Serializable]
    public sealed class CommonExecutionPresentationCue
    {
        [SerializeField] private bool enabled = true;
        [SerializeField] private CommonExecutionCuePoint cuePoint;
        [SerializeField] private CommonExecutionCueType cueType;
        [SerializeField, Min(0f)] private float delaySeconds;
        [SerializeField, Min(-1)] private int flourishShotIndex = -1;

        [SerializeField, BossGraphSfxId] private string sfxId;

        [SerializeField, Range(0.01f, 1f)] private float slowTimeScale = 0.15f;
        [SerializeField, Min(0f)] private float slowDurationSeconds = 0.25f;

        [SerializeField] private CommonExecutionFocusTarget focusTarget =
            CommonExecutionFocusTarget.Midpoint;
        [SerializeField] private Vector2 bossFocusOffset;
        [SerializeField, Range(0f, 1f)] private float cameraFocusWeight = 1f;
        [SerializeField, Range(0.1f, 2.5f)] private float cameraZoomMultiplier = 0.5f;
        [SerializeField, Min(0.01f)] private float cameraBlendSmoothTime = 0.25f;
        [SerializeField, Min(0.01f)] private float finalShotTravelSeconds = 0.3f;
        [SerializeField, Min(0f)] private float cameraHoldSeconds = 0.2f;
        [SerializeField] private bool restoreDefaultCameraAfterHold = true;

        public bool Enabled => enabled;
        public CommonExecutionCuePoint CuePoint => cuePoint;
        public CommonExecutionCueType CueType => cueType;
        public float DelaySeconds => Mathf.Max(0f, delaySeconds);
        public string SfxId => sfxId;
        public float SlowTimeScale => Mathf.Clamp(slowTimeScale, 0.01f, 1f);
        public float SlowDurationSeconds => Mathf.Max(0f, slowDurationSeconds);
        public CommonExecutionFocusTarget FocusTarget => focusTarget;
        public Vector2 BossFocusOffset => bossFocusOffset;
        public float CameraFocusWeight => Mathf.Clamp01(cameraFocusWeight);
        public float CameraZoomMultiplier => Mathf.Clamp(cameraZoomMultiplier, 0.1f, 2.5f);
        public float CameraBlendSmoothTime => Mathf.Max(0.01f, cameraBlendSmoothTime);
        public float FinalShotTravelSeconds => Mathf.Max(0.01f, finalShotTravelSeconds);
        public float CameraHoldSeconds => Mathf.Max(0f, cameraHoldSeconds);
        public bool RestoreDefaultCameraAfterHold => restoreDefaultCameraAfterHold;

        public bool Matches(CommonExecutionCuePoint point, int shotIndex)
        {
            if (!enabled || cuePoint != point)
            {
                return false;
            }

            return point != CommonExecutionCuePoint.FlourishShot
                || flourishShotIndex < 0
                || flourishShotIndex == shotIndex;
        }
    }

    [CreateAssetMenu(
        fileName = "CommonExecutionPresentation",
        menuName = "Week14/Combat/Common Execution Presentation")]
    public sealed class CommonExecutionPresentationProfile : ScriptableObject
    {
        [SerializeField] private CommonExecutionPresentationCue[] cues =
            Array.Empty<CommonExecutionPresentationCue>();

        public CommonExecutionPresentationCue[] Cues =>
            cues ?? Array.Empty<CommonExecutionPresentationCue>();
    }
}
