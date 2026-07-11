using UnityEngine;
using Week14.GameFlow;

namespace Week14.UI
{
    // "선택 후 진입" 2단계 방식에서 BossSlot의 클릭 즉시 진입 방식으로 대체되어 더 이상 사용하지 않음. 참고용으로 보관.
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

            GameFlowController.EnterBoss(selectedBossData);
        }

        private void ClearSelection()
        {
            selectedIcon?.SetSelected(false);
            selectedIcon = null;
            selectedBossData = null;
        }
    }
}
