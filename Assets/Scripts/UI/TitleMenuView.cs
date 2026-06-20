using UnityEngine;
using Week14.Bootstrap;

namespace Week14.UI
{
    public sealed class TitleMenuView : MonoBehaviour
    {
        [SerializeField] private string mainSceneName = "MainScene";

        public void StartGame()
        {
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
