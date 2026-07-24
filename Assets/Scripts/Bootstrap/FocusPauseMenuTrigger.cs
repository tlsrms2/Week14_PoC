using UnityEngine;
using Week14.UI;

namespace Week14.Bootstrap
{
    // 창모드에서 포커스를 잃으면 현재 씬의 일시정지 메뉴를 띄운다.
    // runInBackground가 꺼져 있어 엔진이 루프를 통째로 멈추므로, 포커스가 돌아와도
    // 플레이어가 직접 재개 버튼을 눌러야 게임이 다시 진행된다 — 그래서 복귀 프레임에
    // 밀린 시간이 한 번에 반영되어 보스가 벽을 건너뛰는 문제도 함께 발생하지 않는다.
    public sealed class FocusPauseMenuTrigger : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            GameObject host = new(nameof(FocusPauseMenuTrigger));
            host.AddComponent<FocusPauseMenuTrigger>();
            DontDestroyOnLoad(host);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
            {
                return;
            }

            PauseMenuView pauseMenuView = FindFirstObjectByType<PauseMenuView>();
            pauseMenuView?.PauseForFocusLoss();
        }
    }
}
