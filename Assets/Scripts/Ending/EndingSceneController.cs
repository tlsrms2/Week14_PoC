using System;
using UnityEngine;
using Week14.Cutscene;
using Week14.GameFlow;
using Week14.Save;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Week14.Ending
{
    public sealed class EndingSceneController : MonoBehaviour
    {
        private enum EndingState
        {
            Cutscene,
            ThanksTo,
            Credits,
            Done
        }

        [Header("Cutscene")]
        [SerializeField] private CutscenePlayer cutscenePlayer;
        [SerializeField] private CutsceneDefinition endingCutscene;
        [SerializeField] private bool markEndingSeen = true;

        [Header("Panels")]
        [SerializeField] private GameObject thanksToRoot;
        [SerializeField] private GameObject creditsRoot;

        [Header("Navigation")]
        [SerializeField] private bool returnToTitleAfterCredits = true;
        [SerializeField] private string fallbackTitleSceneName = "TitleScene";

        private EndingState state;

        private void Awake()
        {
            cutscenePlayer ??= FindFirstObjectByType<CutscenePlayer>(FindObjectsInactive.Include);
            SetPanel(thanksToRoot, false);
            SetPanel(creditsRoot, false);
        }

        private void Start()
        {
            PlayEndingCutscene();
        }

        private void Update()
        {
            if (state == EndingState.Cutscene || !AdvancePressed())
            {
                return;
            }

            Advance();
        }

        private void PlayEndingCutscene()
        {
            state = EndingState.Cutscene;

            if (endingCutscene != null && cutscenePlayer != null)
            {
                cutscenePlayer.Play(endingCutscene, ShowThanksTo);
                return;
            }

            ShowThanksTo();
        }

        private void ShowThanksTo()
        {
            if (markEndingSeen)
            {
                GameSaveManager.MarkEndingSeen();
            }

            state = EndingState.ThanksTo;
            SetPanel(thanksToRoot, true);
            SetPanel(creditsRoot, false);
        }

        // 도전과제 연동용: 엔딩 크레딧 스크롤이 표시되는 시점에 발생합니다.
        public static event Action CreditsShown;

        private void ShowCredits()
        {
            state = EndingState.Credits;
            SetPanel(thanksToRoot, false);
            SetPanel(creditsRoot, true);
            CreditsShown?.Invoke();
        }

        private void Advance()
        {
            switch (state)
            {
                case EndingState.ThanksTo:
                    ShowCredits();
                    break;
                case EndingState.Credits:
                    FinishEnding();
                    break;
            }
        }

        private void FinishEnding()
        {
            state = EndingState.Done;

            if (returnToTitleAfterCredits)
            {
                GameFlowController.ReturnToTitle(fallbackTitleSceneName);
            }
        }

        private static void SetPanel(GameObject panel, bool visible)
        {
            if (panel != null)
            {
                panel.SetActive(visible);
            }
        }

        private static bool AdvancePressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            return (keyboard != null
                    && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame))
                || (mouse != null && mouse.leftButton.wasPressedThisFrame);
#else
            return Input.GetKeyDown(KeyCode.Space)
                || Input.GetKeyDown(KeyCode.Return)
                || Input.GetMouseButtonDown(0);
#endif
        }
    }
}
