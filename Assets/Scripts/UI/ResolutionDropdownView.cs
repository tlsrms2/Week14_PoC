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
        [SerializeField]
        private List<Vector2Int> fixedResolutions = new List<Vector2Int>
        {
            new Vector2Int(1280, 720),
            new Vector2Int(1920, 1080),
            new Vector2Int(2560, 1440),
            new Vector2Int(3840, 2160),
            new Vector2Int(1280, 800),
            new Vector2Int(1680, 1050),
            new Vector2Int(1920, 1200),
            new Vector2Int(2560, 1600),
        };

        private readonly List<Vector2Int> resolutions = new List<Vector2Int>();

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

            int contentWidth = SettingsManager.ResolutionWidth > 0 ? SettingsManager.ResolutionWidth : Screen.width;
            int contentHeight = SettingsManager.ResolutionHeight > 0 ? SettingsManager.ResolutionHeight : Screen.height;
            SettingsManager.SetResolution(contentWidth, contentHeight);
            (int width, int height) = FullScreenModeResolver.GetEffectiveScreenResolution(contentWidth, contentHeight, requestedMode);
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
            var seen = new HashSet<Vector2Int>();
            var options = new List<string>();

            var sortedResolutions = new List<Vector2Int>(fixedResolutions);
            sortedResolutions.Sort((a, b) => (a.x * a.y).CompareTo(b.x * b.y));

            foreach (Vector2Int resolution in sortedResolutions)
            {
                if (!seen.Add(resolution))
                {
                    continue;
                }

                resolutions.Add(resolution);
                options.Add($"{resolution.x} x {resolution.y}");
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
        }

        private int FindIndex(int width, int height)
        {
            for (int i = 0; i < resolutions.Count; i++)
            {
                if (resolutions[i].x == width && resolutions[i].y == height)
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

            Vector2Int resolution = resolutions[index];
            SettingsManager.SetResolution(resolution.x, resolution.y);

            FullScreenMode requestedMode = SettingsManager.FullScreenModeValue ?? Screen.fullScreenMode;
            (int width, int height) = FullScreenModeResolver.GetEffectiveScreenResolution(resolution.x, resolution.y, requestedMode);
            FullScreenMode mode = FullScreenModeResolver.Resolve(width, height, requestedMode);
            Screen.SetResolution(width, height, mode);
        }
    }
}
