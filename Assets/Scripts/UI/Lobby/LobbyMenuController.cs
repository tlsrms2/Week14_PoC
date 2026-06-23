using UnityEngine;

namespace Week14.UI
{
    public sealed class LobbyMenuController : MonoBehaviour
    {
        [SerializeField] private GameObject bossPanelRoot;
        [SerializeField] private GameObject loadoutPanelRoot;

        private GameObject activeRoot;

        private void Awake()
        {
            CloseAll();
        }

        public void OpenBossSelect()
        {
            SwitchRoot(bossPanelRoot);
        }

        public void OpenLoadoutSelect()
        {
            SwitchRoot(loadoutPanelRoot);
        }

        public void CloseAll()
        {
            SwitchRoot(null);
        }

        private void SwitchRoot(GameObject nextRoot)
        {
            activeRoot = nextRoot;
            SetRootActive(bossPanelRoot);
            SetRootActive(loadoutPanelRoot);
        }

        private void SetRootActive(GameObject root)
        {
            if (root != null)
            {
                root.SetActive(root == activeRoot);
            }
        }
    }
}
