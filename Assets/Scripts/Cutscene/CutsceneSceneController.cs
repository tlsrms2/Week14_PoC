using UnityEngine;
using Week14.GameFlow;

namespace Week14.Cutscene
{
    public sealed class CutsceneSceneController : MonoBehaviour
    {
        [SerializeField] private CutscenePlayer cutscenePlayer;
        [SerializeField] private string fallbackLobbySceneName = "LobbyScene";

        private void Awake()
        {
            cutscenePlayer ??= FindFirstObjectByType<CutscenePlayer>(FindObjectsInactive.Include);
        }

        private void Start()
        {
            if (GameFlowController.PlayPendingCutscene(cutscenePlayer))
            {
                return;
            }

            Debug.LogWarning($"{nameof(CutsceneSceneController)}: pending cutscene is missing.");
            GameFlowController.ReturnToLobby(fallbackLobbySceneName);
        }
    }
}
