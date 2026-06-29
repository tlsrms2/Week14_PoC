using UnityEngine;
using Week14.Bootstrap;

namespace Week14.UI
{
    public sealed class BossPanelController : MonoBehaviour
    {
        private BossData selectedBossData;
        private BossSelectIcon selectedIcon;
        private bool sceneTransitionPending;

        private void OnEnable()
        {
            ClearSelection();
            sceneTransitionPending = false;
        }

        public void SelectBoss(BossSelectIcon icon, BossData bossData)
        {
            if (bossData == null)
            {
                return;
            }

            if (selectedIcon != null && selectedIcon != icon)
            {
                selectedIcon.SetSelected(false);
            }

            selectedIcon = icon;
            selectedBossData = bossData;
            selectedIcon?.SetSelected(true);
        }

        public void Deselect()
        {
            ClearSelection();
        }

        public void EnterSelectedBoss()
        {
            if (sceneTransitionPending || selectedBossData == null || string.IsNullOrEmpty(selectedBossData.SceneName))
            {
                return;
            }

            sceneTransitionPending = true;
            string sceneName = selectedBossData.SceneName;

            if (PixelBlockRevealView.TryPlayHide(gameObject, () => SceneTransition.LoadScene(sceneName)))
            {
                return;
            }

            SceneTransition.LoadScene(sceneName);
        }

        private void ClearSelection()
        {
            selectedIcon?.SetSelected(false);
            selectedIcon = null;
            selectedBossData = null;
        }
    }
}
