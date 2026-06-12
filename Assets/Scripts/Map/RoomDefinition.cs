using UnityEngine;

namespace Week14.Map
{
    [CreateAssetMenu(menuName = "Week14/Map/Room Definition", fileName = "RoomDefinition")]
    public sealed class RoomDefinition : ScriptableObject
    {
        [SerializeField] private string roomId;
        [SerializeField] private RoomType roomType;
        [SerializeField] private GameObject prefab;
        [SerializeField] private RoomDirection exits = RoomDirection.North | RoomDirection.East | RoomDirection.South | RoomDirection.West;
        [SerializeField, Min(0)] private int weight = 1;

        public string RoomId => string.IsNullOrWhiteSpace(roomId) ? name : roomId;
        public RoomType RoomType => roomType;
        public GameObject Prefab => prefab;
        public RoomDirection Exits => exits;
        public int Weight => weight;

        public void ConfigureRuntime(string id, GameObject roomPrefab, RoomDirection roomExits, int roomWeight, RoomType type = RoomType.Combat)
        {
            roomId = id;
            roomType = type;
            prefab = roomPrefab;
            exits = roomExits;
            weight = Mathf.Max(0, roomWeight);
        }
    }
}
