using UnityEngine;
using Week14.Audio;
using Week14.Bootstrap;
using Week14.Enemy;

namespace Week14.UI
{
    public sealed class TitleMenuView : MonoBehaviour
    {
        [SerializeField] private string mainSceneName = "MainScene";
        [Tooltip("게임 시작 시 재생할 SFX의 SoundLibrary ID입니다. 비워두면 재생하지 않습니다.")]
        [BossGraphSfxId]
        [SerializeField] private string startGameSfxId;

        public void StartGame()
        {
            if (!string.IsNullOrEmpty(startGameSfxId))
            {
                SoundManager.PlaySfx(startGameSfxId);
            }

            SceneTransition.LoadScene(mainSceneName);
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
