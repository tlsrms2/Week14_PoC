using UnityEngine;
using Week14.Save;

namespace Week14.Bootstrap
{
    // 저장된 해상도/화면모드(SettingsManager.ResolutionWidth/Height, FullScreenModeValue)를 게임 프로세스 시작 시 한 번 적용합니다.
    // 저장된 값이 없으면 플랫폼 기본값을 그대로 둡니다.
    public static class ResolutionBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplySavedResolution()
        {
            int width = SettingsManager.ResolutionWidth;
            int height = SettingsManager.ResolutionHeight;
            FullScreenMode requestedMode = SettingsManager.FullScreenModeValue ?? Screen.fullScreenMode;

            if (width > 0 && height > 0)
            {
                FullScreenMode mode = FullScreenModeResolver.Resolve(width, height, requestedMode);
                Screen.SetResolution(width, height, mode);
            }
            else if (SettingsManager.FullScreenModeValue.HasValue)
            {
                Screen.fullScreenMode = requestedMode;
            }
        }
    }
}
