using TMPro;
using UnityEngine;
using Week14.Map;

namespace Week14.UI
{
    public sealed class RoomStatusView : MonoBehaviour
    {
        [SerializeField] private RoomGridManager gridManager;
        [SerializeField] private TextMeshProUGUI valueText;

        private void OnEnable()
        {
            if (gridManager != null)
            {
                gridManager.CurrentRoomSlotChanged += HandleRoomChanged;
                Refresh(gridManager.CurrentSlot);
            }
        }

        private void OnDisable()
        {
            if (gridManager != null)
            {
                gridManager.CurrentRoomSlotChanged -= HandleRoomChanged;
            }
        }

        public void Configure(RoomGridManager manager, TextMeshProUGUI text)
        {
            if (gridManager != null)
            {
                gridManager.CurrentRoomSlotChanged -= HandleRoomChanged;
            }

            gridManager = manager;
            valueText = text;

            if (gridManager != null)
            {
                gridManager.CurrentRoomSlotChanged += HandleRoomChanged;
                Refresh(gridManager.CurrentSlot);
            }
        }

        private void HandleRoomChanged(RoomSlot slot)
        {
            Refresh(slot);
        }

        private void Refresh(RoomSlot slot)
        {
            if (valueText == null)
            {
                return;
            }

            if (slot == null || slot.Definition == null)
            {
                valueText.text = "Room -";
                return;
            }

            valueText.text = $"{slot.Definition.RoomType} {slot.GridPosition.x},{slot.GridPosition.y}";
        }
    }
}
