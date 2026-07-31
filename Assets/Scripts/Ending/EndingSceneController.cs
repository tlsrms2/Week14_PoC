using UnityEngine;
using Week14.Audio;
using Week14.Cutscene;
using Week14.Enemy;
using Week14.GameFlow;
using Week14.Save;

namespace Week14.Ending
{
    public sealed class EndingSceneController : MonoBehaviour
    {
        [Header("Cutscene")]
        [SerializeField] private CutscenePlayer cutscenePlayer;
        [SerializeField] private CutsceneDefinition endingCutscene;
        [SerializeField] private bool markEndingSeen = true;

        [Header("Outro")]
        [SerializeField] private EndingOutroSequence outroSequence;

        [Header("Audio")]
        [Tooltip("크레딧이 시작될 때 재생할 SoundLibrary BGM ID입니다. 비워두면 현재 BGM을 유지합니다.")]
        [BossGraphBgmId]
        [SerializeField] private string creditBgmId = "Credit BGM";
        [SerializeField, Min(0f)] private float creditBgmFadeSeconds = 1f;

        [Header("Navigation")]
        [SerializeField] private bool returnToTitleAfterCredits = true;
        [SerializeField] private string fallbackTitleSceneName = "TitleScene";

        private void Awake()
        {
            cutscenePlayer ??= FindFirstObjectByType<CutscenePlayer>(FindObjectsInactive.Include);
        }

        private void Start()
        {
            PlayEndingCutscene();
        }

        private void PlayEndingCutscene()
        {
            if (endingCutscene != null && cutscenePlayer != null)
            {
                cutscenePlayer.Play(endingCutscene, ShowOutro);
                return;
            }

            ShowOutro();
        }

        private void ShowOutro()
        {
            if (!string.IsNullOrWhiteSpace(creditBgmId))
            {
                SoundManager.PlayBgm(creditBgmId, creditBgmFadeSeconds);
            }

            if (markEndingSeen)
            {
                GameSaveManager.MarkEndingSeen();
            }

            outroSequence.Play(FinishEnding);
        }

        private void FinishEnding()
        {
            if (returnToTitleAfterCredits)
            {
                GameFlowController.ReturnToTitle(fallbackTitleSceneName);
            }
        }
    }
}
