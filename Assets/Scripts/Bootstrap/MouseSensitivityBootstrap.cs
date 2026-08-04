using UnityEngine;
using Week14.Save;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif

namespace Week14.Bootstrap
{
    // 마우스 감도(SettingsManager.MouseSensitivity)를 실제 OS 커서 이동 자체에 적용한다.
    // 물리 마우스의 원시 델타(Mouse.delta)에 감도를 곱해 누적한 좌표로 매 프레임 OS 커서를 강제 이동
    // (WarpCursorPosition)시키므로, 실제 좌표를 그대로 읽는 CursorController(메뉴 커서)와
    // GameInput.MouseScreenPosition(전투 조준, 카메라 룩어헤드) 양쪽 모두 코드 수정 없이 감도가 반영된다.
    // 감도 1.0이면 누적값이 OS가 자연스럽게 이동시켰을 위치와 같아 워프해도 좌표가 그대로라 기존 동작과 동일하다.
    // 마우스를 창 안에 가두지 않는다 — 누적 좌표가 창 경계를 벗어나려는 순간 추적을 놓아줘서 커서가
    // 자유롭게 창 밖(다른 모니터, 작업표시줄 등)으로 나갈 수 있게 하고, 창 안으로 되돌아오면 그 시점
    // 실제 위치부터 다시 추적한다.
    [DefaultExecutionOrder(-20000)]
    public sealed class MouseSensitivityBootstrap : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            GameObject host = new(nameof(MouseSensitivityBootstrap));
            host.AddComponent<MouseSensitivityBootstrap>();
            DontDestroyOnLoad(host);
        }

#if ENABLE_INPUT_SYSTEM
        private Vector2 targetPosition;
        private bool tracking;

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            Vector2 currentPosition = mouse.position.ReadValue();

            if (!tracking || !Application.isFocused)
            {
                // 추적이 꺼진 상태(막 시작했거나, 포커스를 잃었거나, 커서가 창 밖으로 나가서 놓아준 상태)에선
                // 실제 커서가 창 안으로 들어와 있을 때만 그 위치부터 추적을 재개한다 — 그래야 복귀 시
                // 누적 오차 때문에 순간 이동하거나 반응이 밀리는 일이 없다.
                bool insideWindow = Application.isFocused
                    && currentPosition.x >= 0f && currentPosition.x <= Screen.width
                    && currentPosition.y >= 0f && currentPosition.y <= Screen.height;

                if (!insideWindow)
                {
                    return;
                }

                targetPosition = currentPosition;
                tracking = true;
                return;
            }

            float sensitivity = SettingsManager.MouseSensitivity;
            Vector2 candidate = targetPosition + mouse.delta.ReadValue() * sensitivity;

            bool candidateInsideWindow = candidate.x >= 0f && candidate.x <= Screen.width
                && candidate.y >= 0f && candidate.y <= Screen.height;

            if (!candidateInsideWindow)
            {
                // 마우스를 안 가두므로, 창 경계를 넘어가려는 순간엔 추적을 놓아줘서 커서가 자유롭게
                // 창 밖(다른 모니터, 작업표시줄 등)으로 나갈 수 있게 한다.
                tracking = false;
                return;
            }

            targetPosition = candidate;
            mouse.WarpCursorPosition(targetPosition);
            InputState.Change(mouse.position, targetPosition);
        }
#endif
    }
}
