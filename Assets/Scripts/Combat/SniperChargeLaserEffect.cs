using UnityEngine;
using Week14.Input;

namespace Week14.Combat
{
    // 저격총 차지 연출: 총알이 날아갈 방향(중앙) 레이저 1개 + 지정한 각도 양 끝에서 시작하는 레이저 2개.
    // 양옆 레이저는 차지 진행도(0~1)에 따라 중앙으로 모여들다가, 완전 차지되면 중앙 레이저와 겹칩니다.
    public sealed class SniperChargeLaserEffect : MonoBehaviour
    {
        [SerializeField] private Shader lineShader;
        [SerializeField, Min(0f)] private float lineWidth = 0.04f;
        [SerializeField] private Color chargingColor = new Color(1f, 0.25f, 0.15f, 0.85f);
        [SerializeField] private Color fullyChargedColor = new Color(1f, 0.85f, 0f, 1f);
        [SerializeField, Min(0f)] private float colorTransitionSpeed = 10f;

        private LineRenderer centerLine;
        private LineRenderer leftLine;
        private LineRenderer rightLine;
        private Material lineMaterial;

        private bool isCharging;
        private float length = 5f;
        private float startHalfAngleDegrees;
        private float progress;
        private Color currentColor;

        private void Awake()
        {
            SetupLines();
            SetVisible(false);
        }

        public void BeginCharge(float laserLength, float spreadAngleDegrees)
        {
            length = Mathf.Max(0.1f, laserLength);
            startHalfAngleDegrees = Mathf.Max(0f, spreadAngleDegrees) * 0.5f;
            progress = 0f;
            currentColor = chargingColor;
            isCharging = true;
        }

        public void SetProgress(float value)
        {
            progress = Mathf.Clamp01(value);
        }

        public void EndCharge()
        {
            isCharging = false;
            SetVisible(false);
        }

        private void LateUpdate()
        {
            if (!isCharging)
            {
                return;
            }

            PlayerCombatController player = PlayerCombatController.Active;
            if (player == null || !player.IsReticleVisible)
            {
                SetVisible(false);
                return;
            }

            Camera cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            SetVisible(true);

            Transform fireOrigin = player.LeftFireOrigin;
            Vector3 origin = fireOrigin != null ? fireOrigin.position : player.transform.position;
            origin.z = 0f;

            // 실제 발사 방향(PlayerAimController.GetAimPoint)과 동일한 기준을 써야 한다 — 락온 타겟이
            // 있으면 총알은 마우스가 아니라 타겟 쪽으로 나가므로, 레이저도 마우스만 보면 방향이 어긋난다.
            Health lockOnTarget = player.LockOnTarget;
            Vector2 aimPoint = lockOnTarget != null && !lockOnTarget.IsDead
                ? (Vector2)lockOnTarget.transform.position
                : (Vector2)cam.ScreenToWorldPoint(GameInput.MouseScreenPosition);
            Vector2 aimDir = aimPoint - (Vector2)origin;
            if (aimDir.sqrMagnitude < 0.0001f)
            {
                aimDir = Vector2.right;
            }

            aimDir.Normalize();

            float halfAngle = Mathf.Lerp(startHalfAngleDegrees, 0f, progress);
            Color targetColor = progress >= 1f ? fullyChargedColor : chargingColor;
            currentColor = Color.Lerp(currentColor, targetColor, colorTransitionSpeed * Time.deltaTime);

            // 세 레이저가 lineMaterial 하나를 공유하므로(sharedMaterial) 여기서 한 번만 칠하면 셋 다 같이 바뀝니다.
            // LineRenderer.startColor/endColor(버텍스 컬러)는 URP Unlit 셰이더가 기본적으로 무시하기 때문에
            // ShotgunRangeIndicator와 동일하게 머티리얼의 _BaseColor를 직접 설정합니다.
            if (lineMaterial != null)
            {
                lineMaterial.color = currentColor;
            }

            DrawRay(centerLine, origin, aimDir, 0f);
            DrawRay(leftLine, origin, aimDir, -halfAngle);
            DrawRay(rightLine, origin, aimDir, halfAngle);
        }

        private void DrawRay(LineRenderer line, Vector3 origin, Vector2 aimDir, float angleOffsetDegrees)
        {
            if (line == null)
            {
                return;
            }

            Vector2 direction = Quaternion.Euler(0f, 0f, angleOffsetDegrees) * aimDir;
            Vector3 end = origin + (Vector3)(direction * length);

            line.SetPosition(0, origin);
            line.SetPosition(1, end);
        }

        private void SetVisible(bool visible)
        {
            if (centerLine != null) centerLine.enabled = visible;
            if (leftLine != null) leftLine.enabled = visible;
            if (rightLine != null) rightLine.enabled = visible;
        }

        private void SetupLines()
        {
            Shader shader = lineShader != null ? lineShader : Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                Debug.LogError("SniperChargeLaserEffect: Line Shader is not assigned. Assign a URP-compatible shader in the inspector " +
                    "(Shader.Find fallback is stripped from builds if unused elsewhere).", this);
                return;
            }

            lineMaterial = new Material(shader);
            lineMaterial.SetFloat("_Surface", 1f);
            lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            lineMaterial.SetInt("_ZWrite", 0);
            lineMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            centerLine = CreateLine("CenterLaser");
            leftLine = CreateLine("LeftLaser");
            rightLine = CreateLine("RightLaser");
        }

        private LineRenderer CreateLine(string objectName)
        {
            GameObject lineObject = new(objectName);
            lineObject.transform.SetParent(transform, false);

            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = lineWidth;
            line.endWidth = lineWidth;
            line.numCapVertices = 4;
            line.sharedMaterial = lineMaterial;
            line.enabled = false;
            return line;
        }
    }
}
