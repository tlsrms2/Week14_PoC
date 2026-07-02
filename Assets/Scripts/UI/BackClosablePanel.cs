using UnityEngine;

namespace Week14.UI
{
    public sealed class BackClosablePanel : MonoBehaviour, IBackClosable
    {
        [SerializeField] private GameObject targetRoot;

        private GameObject TargetRoot => targetRoot != null ? targetRoot : gameObject;

        private void OnEnable()
        {
            UIBackStack.Push(this);
        }

        private void OnDisable()
        {
            UIBackStack.Remove(this);
        }

        public bool CloseByBack()
        {
            TargetRoot.SetActive(false);
            return true;
        }
    }
}
