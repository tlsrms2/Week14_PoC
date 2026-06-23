using UnityEngine;
using UnityEngine.SceneManagement;

namespace Week14.UI
{
    public sealed class BossPanelController : MonoBehaviour
    {
        private BossData selectedBossData;
        private BossSelectIcon selectedIcon;

        private void OnEnable()
        {
            ClearSelection();
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
            if (selectedBossData == null || string.IsNullOrEmpty(selectedBossData.SceneName))
            {
                return;
            }

            SceneManager.LoadScene(selectedBossData.SceneName);
        }

        private void ClearSelection()
        {
            selectedIcon?.SetSelected(false);
            selectedIcon = null;
            selectedBossData = null;
        }
    }
}
