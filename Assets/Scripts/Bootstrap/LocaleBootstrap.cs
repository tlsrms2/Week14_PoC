using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using Week14.Save;

namespace Week14.Bootstrap
{
    // 저장된 언어(SettingsManager.LanguageCode)를 게임 프로세스 시작 시 한 번 적용합니다.
    // [RuntimeInitializeOnLoadMethod]는 어떤 씬이 제일 먼저 로드되든(빌드에서는 항상 씬 0이지만,
    // 에디터에서는 지금 열려있는 씬으로 바로 Play할 수도 있음) 상관없이 자동으로 호출되므로,
    // 특정 씬(타이틀 등)에만 의존하는 방식보다 안전합니다.
    public static class LocaleBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplySavedLanguage()
        {
            LocalizationSettings.InitializationOperation.Completed += _ => ApplyLanguageCode(SettingsManager.LanguageCode);
        }

        private static void ApplyLanguageCode(string languageCode)
        {
            IReadOnlyList<Locale> locales = LocalizationSettings.AvailableLocales.Locales;
            for (int i = 0; i < locales.Count; i++)
            {
                if (locales[i].Identifier.Code != languageCode)
                {
                    continue;
                }

                LocalizationSettings.SelectedLocale = locales[i];
                return;
            }

            Debug.LogWarning($"[LocaleBootstrap] 저장된 언어 코드 '{languageCode}'와 일치하는 Locale을 찾지 못했습니다.");
        }
    }
}
