using UnityEngine;
using Week14.Map;

namespace Week14.Encounter
{
    public sealed class RoomDoorLock : MonoBehaviour, IRoomEnterHandler
    {
        [SerializeField] private RoomEncounter encounter;
        [SerializeField] private GameObject[] doorBlockers;
        [SerializeField] private bool lockCombatRoomsOnly = true;

        private void Awake()
        {
            if (encounter == null)
            {
                encounter = GetComponentInParent<RoomEncounter>();
            }

            if (encounter == null)
            {
                return;
            }

            encounter.Started += HandleEncounterStarted;
            encounter.Cleared += HandleEncounterCleared;
        }

        private void OnDestroy()
        {
            if (encounter == null)
            {
                return;
            }

            encounter.Started -= HandleEncounterStarted;
            encounter.Cleared -= HandleEncounterCleared;
        }

        public void OnPlayerEnteredRoom(RoomSlot slot)
        {
            if (lockCombatRoomsOnly
                && slot.Definition != null
                && slot.Definition.RoomType != RoomType.Combat
                && slot.Definition.RoomType != RoomType.Boss)
            {
                SetLocked(false);
                return;
            }

            SetLocked(encounter != null && !encounter.IsCleared);
        }

        private void HandleEncounterStarted(RoomEncounter roomEncounter)
        {
            SetLocked(true);
        }

        private void HandleEncounterCleared(RoomEncounter roomEncounter)
        {
            SetLocked(false);
        }

        private void SetLocked(bool locked)
        {
            if (doorBlockers == null)
            {
                return;
            }

            for (int i = 0; i < doorBlockers.Length; i++)
            {
                if (doorBlockers[i] != null)
                {
                    doorBlockers[i].SetActive(locked);
                }
            }
        }
    }
}
