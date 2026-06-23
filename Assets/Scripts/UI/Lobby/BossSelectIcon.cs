using UnityEngine;
using UnityEngine.EventSystems;
using Week14.Save;

namespace Week14.UI
{
    public sealed class BossSelectIcon : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private BossPanelController panelController;
        [SerializeField] private BossData bossData;

        private SpriteRenderer spriteRenderer;
        private Collider2D pointerCollider;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            pointerCollider = GetComponent<Collider2D>();
        }

        private void OnEnable()
        {
            RefreshLockState();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (panelController == null || bossData == null || !IsUnlocked())
            {
                return;
            }

            panelController.SelectBoss(transform, bossData);
        }

        private void RefreshLockState()
        {
            bool unlocked = IsUnlocked();

            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = unlocked;
            }

            if (pointerCollider != null)
            {
                pointerCollider.enabled = unlocked;
            }
        }

        private bool IsUnlocked()
        {
            return bossData != null && BossProgressManager.IsUnlocked(bossData.Id);
        }
    }
}
