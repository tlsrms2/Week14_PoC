using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Week14.Audio;

namespace Week14.UI
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class ButtonClickSfxBinder : MonoBehaviour
    {
        private const string ClickSfxId = "ButtonClick";
        private const string SceneTransitionSfxId = "Title_Button";
        private const float RefreshIntervalSeconds = 1f;

        private readonly HashSet<Button> boundButtons = new();
        private float nextRefreshTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (FindFirstObjectByType<ButtonClickSfxBinder>(
                    FindObjectsInactive.Include) != null)
            {
                return;
            }

            GameObject instance = new(nameof(ButtonClickSfxBinder));
            DontDestroyOnLoad(instance);
            instance.AddComponent<ButtonClickSfxBinder>();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            BindSceneButtons();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            UnbindButtons();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = Time.unscaledTime + RefreshIntervalSeconds;
            BindSceneButtons();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RemoveDestroyedButtons();
            BindSceneButtons();
        }

        private void BindSceneButtons()
        {
            Button[] buttons = FindObjectsByType<Button>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (button != null && boundButtons.Add(button))
                {
                    button.onClick.AddListener(PlayClickSfx);
                }
            }
        }

        private void RemoveDestroyedButtons()
        {
            boundButtons.RemoveWhere(button => button == null);
        }

        private void UnbindButtons()
        {
            foreach (Button button in boundButtons)
            {
                if (button != null)
                {
                    button.onClick.RemoveListener(PlayClickSfx);
                }
            }

            boundButtons.Clear();
        }

        private static void PlayClickSfx()
        {
            if (!SoundManager.WasSfxPlayedThisFrame(SceneTransitionSfxId))
            {
                SoundManager.PlaySfx(ClickSfxId);
            }
        }
    }
}
