using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Week14.Save;

namespace Week14.UI
{
    public sealed class ResolutionDropdownView : MonoBehaviour
    {
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [SerializeField] private Toggle fullscreenToggle;

        private readonly List<Resolution> resolutions = new List<Resolution>();

        private void Awake()
        {
            resolutionDropdown ??= GetComponentInChildren<TMP_Dropdown>(true);
        }

        private void OnEnable()
        {
            PopulateDropdown();
            SyncFullscreenToggle();
        }

        private void SyncFullscreenToggle()
        {
            if (fullscreenToggle == null)
            {
                return;
            }

            FullScreenMode mode = SettingsManager.FullScreenModeValue ?? Screen.fullScreenMode;
            fullscreenToggle.SetIsOnWithoutNotify(mode != FullScreenMode.Windowed);
        }

        public void SetFullscreen(bool isFullscreen)
        {
            FullScreenMode requestedMode = isFullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            SettingsManager.SetFullScreenMode(requestedMode);

            int width = SettingsManager.ResolutionWidth > 0 ? SettingsManager.ResolutionWidth : Screen.width;
            int height = SettingsManager.ResolutionHeight > 0 ? SettingsManager.ResolutionHeight : Screen.height;
            FullScreenMode mode = FullScreenModeResolver.Resolve(width, height, requestedMode);
            Screen.SetResolution(width, height, mode);
        }

        private void PopulateDropdown()
        {
            if (resolutionDropdown == null)
            {
                return;
            }

            resolutions.Clear();
            var seen = new HashSet<(int width, int height)>();
            var options = new List<string>();

            foreach (Resolution resolution in Screen.resolutions)
            {
                if (!seen.Add((resolution.width, resolution.height)))
                {
                    continue;
                }

                resolutions.Add(resolution);
                options.Add($"{resolution.width} x {resolution.height}");
            }

            int selectedIndex = FindIndex(SettingsManager.ResolutionWidth, SettingsManager.ResolutionHeight);
            if (selectedIndex < 0)
            {
                selectedIndex = FindIndex(Screen.width, Screen.height);
            }

            resolutionDropdown.ClearOptions();
            resolutionDropdown.AddOptions(options);
            resolutionDropdown.SetValueWithoutNotify(Mathf.Max(selectedIndex, 0));
            resolutionDropdown.RefreshShownValue();

            Debug.Log($"[ResolutionDropdownView] 지원 해상도 {options.Count}개: {string.Join(", ", options)}");
        }

        private int FindIndex(int width, int height)
        {
            for (int i = 0; i < resolutions.Count; i++)
            {
                if (resolutions[i].width == width && resolutions[i].height == height)
                {
                    return i;
                }
            }

            return -1;
        }

        public void SetResolution(int index)
        {
            if (index < 0 || index >= resolutions.Count)
            {
                return;
            }

            Resolution resolution = resolutions[index];
            SettingsManager.SetResolution(resolution.width, resolution.height);

            FullScreenMode requestedMode = SettingsManager.FullScreenModeValue ?? Screen.fullScreenMode;
            FullScreenMode mode = FullScreenModeResolver.Resolve(resolution.width, resolution.height, requestedMode);
            Screen.SetResolution(resolution.width, resolution.height, mode);
        }
    }
}
