using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;

namespace Week14.UI
{
    // 언어가 바뀔 때마다 씬의 모든 TMP_Text 폰트를 언어별로 일괄 교체합니다.
    // languageCode에 매핑된 폰트가 없으면 각 텍스트의 원래(프리팹/씬에 지정된) 폰트로 복원합니다.
    // SoundManager와 동일하게 프리팹으로 각 씬에 배치해서 씁니다(중복 인스턴스는 Awake에서 정리됨).
    public sealed class FontLocalizationManager : MonoBehaviour
    {
        [System.Serializable]
        private struct LocaleFontMapping
        {
            [Tooltip("언어 코드 (예: ko, en, ja). Locale 코드가 이 값으로 시작하면 매칭됩니다.")]
            public string languageCode;
            public TMP_FontAsset fontAsset;
        }

        [Tooltip("매핑에 없는 언어는 각 텍스트의 원래 폰트를 그대로 사용합니다.")]
        [SerializeField] private List<LocaleFontMapping> fontMappings = new();

        private static FontLocalizationManager instance;

        private readonly Dictionary<TMP_Text, TMP_FontAsset> originalFonts = new();

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
            SceneManager.sceneLoaded += HandleSceneLoaded;
            LocalizationSettings.SelectedLocaleChanged += HandleLocaleChanged;
            StartCoroutine(ApplyInitialFontRoutine());
        }

        private void OnDestroy()
        {
            if (instance != this)
            {
                return;
            }

            SceneManager.sceneLoaded -= HandleSceneLoaded;
            LocalizationSettings.SelectedLocaleChanged -= HandleLocaleChanged;
            instance = null;
        }

        private IEnumerator ApplyInitialFontRoutine()
        {
            yield return LocalizationSettings.InitializationOperation;
            ApplyFontForLocale(LocalizationSettings.SelectedLocale);
        }

        private void HandleLocaleChanged(Locale newLocale)
        {
            ApplyFontForLocale(newLocale);
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (LocalizationSettings.InitializationOperation.IsDone)
            {
                ApplyFontForLocale(LocalizationSettings.SelectedLocale);
            }
        }

        private void ApplyFontForLocale(Locale locale)
        {
            if (locale == null)
            {
                return;
            }

            PruneDestroyedEntries();

            TMP_FontAsset mappedFont = FindMappedFont(locale.Identifier.Code);
            TMP_Text[] texts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (TMP_Text text in texts)
            {
                if (!originalFonts.TryGetValue(text, out TMP_FontAsset originalFont))
                {
                    originalFont = text.font;
                    originalFonts[text] = originalFont;
                }

                text.font = mappedFont != null ? mappedFont : originalFont;
            }
        }

        private TMP_FontAsset FindMappedFont(string localeCode)
        {
            string code = localeCode.ToLowerInvariant();
            foreach (LocaleFontMapping mapping in fontMappings)
            {
                if (!string.IsNullOrEmpty(mapping.languageCode)
                    && code.StartsWith(mapping.languageCode.ToLowerInvariant()))
                {
                    return mapping.fontAsset;
                }
            }

            return null;
        }

        private void PruneDestroyedEntries()
        {
            List<TMP_Text> stale = null;
            foreach (TMP_Text key in originalFonts.Keys)
            {
                if (key == null)
                {
                    (stale ??= new List<TMP_Text>()).Add(key);
                }
            }

            if (stale == null)
            {
                return;
            }

            foreach (TMP_Text key in stale)
            {
                originalFonts.Remove(key);
            }
        }
    }
}
