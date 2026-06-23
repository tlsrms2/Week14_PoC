using UnityEngine;
using UnityEngine.SceneManagement;

namespace Week14.UI
{
    public sealed class BossPanelController : MonoBehaviour
    {
        [SerializeField] private BossMapZoomController zoomController;
        [SerializeField] private BossDetailPanel detailPanel;

        private BossData selectedBossData;

        private void OnEnable()
        {
            zoomController?.ResetImmediate();
            detailPanel?.HideImmediate();
        }

        public void SelectBoss(Transform posterTransform, BossData bossData)
        {
            if (bossData == null)
            {
                return;
            }

            if (zoomController != null && posterTransform != null)
            {
                zoomController.ZoomTo(posterTransform.position);
            }

            selectedBossData = bossData;
            detailPanel?.Show(bossData.BossName, bossData.Crime, bossData.Description, bossData.Icon);
        }

        public void Deselect()
        {
            zoomController?.ZoomOut();
            detailPanel?.Hide();
        }

        public void EnterSelectedBoss()
        {
            if (selectedBossData == null || string.IsNullOrEmpty(selectedBossData.SceneName))
            {
                return;
            }

            SceneManager.LoadScene(selectedBossData.SceneName);
        }
    }
}
