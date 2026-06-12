using UnityEngine;

namespace Week14.Map
{
    public sealed class RoomSlot : MonoBehaviour
    {
        [SerializeField] private Vector2Int gridPosition;
        [SerializeField] private Transform contentRoot;

        private GameObject spawnedRoom;

        public Vector2Int GridPosition => gridPosition;
        public RoomDefinition Definition { get; private set; }
        public bool IsVisible { get; private set; }
        public GameObject SpawnedRoom => spawnedRoom;

        public void Initialize(Vector2Int position)
        {
            gridPosition = position;

            if (contentRoot == null)
            {
                contentRoot = transform;
            }
        }

        public void SetDefinition(RoomDefinition definition)
        {
            if (Definition == definition && spawnedRoom != null)
            {
                return;
            }

            ClearSpawnedRoom();
            Definition = definition;

            if (definition == null || definition.Prefab == null)
            {
                return;
            }

            spawnedRoom = Instantiate(definition.Prefab, contentRoot);
            spawnedRoom.transform.localPosition = Vector3.zero;
            spawnedRoom.transform.localRotation = Quaternion.identity;
            spawnedRoom.transform.localScale = Vector3.one;
            spawnedRoom.SetActive(IsVisible);
        }

        public void SetVisible(bool visible)
        {
            IsVisible = visible;

            if (spawnedRoom != null)
            {
                spawnedRoom.SetActive(visible);
            }
        }

        public T GetRoomComponent<T>() where T : Component
        {
            return spawnedRoom == null ? null : spawnedRoom.GetComponentInChildren<T>(true);
        }

        private void ClearSpawnedRoom()
        {
            if (spawnedRoom == null)
            {
                return;
            }

            Destroy(spawnedRoom);
            spawnedRoom = null;
        }
    }
}
