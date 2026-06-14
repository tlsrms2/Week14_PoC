using UnityEngine;

namespace Week14.Map
{
    [CreateAssetMenu(menuName = "Week14/Map/Room Definition", fileName = "RoomDefinition")]
    public sealed class RoomDefinition : ScriptableObject
    {
        [Tooltip("방을 식별하는 고유 ID입니다. 비워두면 에셋 이름을 사용합니다.")]
        [SerializeField] private string roomId;
        [Tooltip("방의 종류입니다. 시작방, 전투방, 출구방 등으로 분류합니다.")]
        [SerializeField] private RoomType roomType;
        [Tooltip("이 방을 생성할 때 사용할 프리팹입니다.")]
        [SerializeField] private GameObject prefab;
        [Tooltip("이 방에서 연결 가능한 출구 방향입니다.")]
        [SerializeField] private RoomDirection exits = RoomDirection.North | RoomDirection.East | RoomDirection.South | RoomDirection.West;
        [Tooltip("랜덤 방 선택 시 이 방이 뽑힐 가중치입니다. 높을수록 자주 등장합니다.")]
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
