using UnityEngine;
using Week14.Save;

namespace Week14.Bootstrap
{
    // 저장된 해상도/화면모드(SettingsManager.ResolutionWidth/Height, FullScreenModeValue)를 게임 프로세스 시작 시 한 번 적용합니다.
    // 설정 파일이 아예 없는 최초 실행이라도, 전체화면으로 시작하는 경우엔 항상 FullScreenModeResolver를 거쳐
    // DWM 늘림/레터박스 미적용을 막습니다(콘텐츠 해상도가 없으면 이 게임의 디자인 기준 해상도를 대신 씀).
    public static class ResolutionBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplySavedResolution()
        {
            int width = SettingsManager.ResolutionWidth;
            int height = SettingsManager.ResolutionHeight;
            FullScreenMode requestedMode = SettingsManager.FullScreenModeValue ?? Screen.fullScreenMode;

            if (width <= 0 || height <= 0)
            {
                if (requestedMode == FullScreenMode.Windowed)
                {
                    if (SettingsManager.FullScreenModeValue.HasValue)
                    {
                        Screen.fullScreenMode = requestedMode;
                    }

                    return;
                }

                width = FullScreenModeResolver.DefaultContentWidth;
                height = FullScreenModeResolver.DefaultContentHeight;
            }

            (int effectiveWidth, int effectiveHeight) = FullScreenModeResolver.GetEffectiveScreenResolution(width, height, requestedMode);
            FullScreenMode mode = FullScreenModeResolver.Resolve(effectiveWidth, effectiveHeight, requestedMode);
            Screen.SetResolution(effectiveWidth, effectiveHeight, mode);
            SettingsManager.SetResolution(width, height);
        }
    }
}
