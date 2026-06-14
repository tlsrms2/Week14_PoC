using System.Collections.Generic;
using UnityEngine;

namespace Week14.Map
{
    [CreateAssetMenu(menuName = "Week14/Map/Room Grid Config", fileName = "RoomGridConfig")]
    public sealed class RoomGridConfig : ScriptableObject
    {
        [Header("그리드")]
        [Tooltip("방 그리드의 가로 칸 수입니다.")]
        [SerializeField, Min(1)] private int width = 5;
        [Tooltip("방 그리드의 세로 칸 수입니다.")]
        [SerializeField, Min(1)] private int height = 5;
        [Tooltip("각 방 슬롯 사이의 월드 좌표 간격입니다.")]
        [SerializeField] private Vector2 roomSize = new Vector2(24f, 14f);
        [Tooltip("플레이어 주변에서 활성화하거나 보여줄 방의 반경입니다.")]
        [SerializeField, Min(0)] private int visibleRadius = 1;

        [Header("셔플")]
        [Tooltip("방 배치를 다시 섞는 시간 간격입니다. 0이면 타이머 기준 셔플을 사실상 비활성화합니다.")]
        [SerializeField, Min(0f)] private float shuffleIntervalSeconds = 15f;
        [Tooltip("켜면 랜덤 배치에 고정 시드를 사용해서 같은 결과가 나오게 합니다.")]
        [SerializeField] private bool useFixedSeed;
        [Tooltip("고정 시드를 사용할 때 적용할 시드 값입니다.")]
        [SerializeField] private int fixedSeed = 1004;

        [Header("방 풀")]
        [Tooltip("플레이어 시작 위치에 배치할 방입니다.")]
        [SerializeField] private RoomDefinition startRoom;
        [Tooltip("출구 또는 목표 지점으로 사용할 방입니다.")]
        [SerializeField] private RoomDefinition exitRoom;
        [Tooltip("적절한 방을 찾지 못했을 때 대신 배치할 기본 방입니다.")]
        [SerializeField] private RoomDefinition fallbackRoom;
        [Tooltip("랜덤 배치에 사용할 방 후보 목록입니다.")]
        [SerializeField] private List<RoomDefinition> roomPool = new List<RoomDefinition>();

        public int Width => width;
        public int Height => height;
        public Vector2 RoomSize => new Vector2(Mathf.Max(0.01f, roomSize.x), Mathf.Max(0.01f, roomSize.y));
        public int VisibleRadius => visibleRadius;
        public float ShuffleIntervalSeconds => shuffleIntervalSeconds;
        public bool UseFixedSeed => useFixedSeed;
        public int FixedSeed => fixedSeed;
        public RoomDefinition StartRoom => startRoom;
        public RoomDefinition ExitRoom => exitRoom;
        public RoomDefinition FallbackRoom => fallbackRoom;
        public IReadOnlyList<RoomDefinition> RoomPool => roomPool;

        public void ConfigureRuntime(
            int gridWidth,
            int gridHeight,
            Vector2 gridRoomSize,
            int gridVisibleRadius,
            float gridShuffleIntervalSeconds,
            RoomDefinition gridFallbackRoom,
            IReadOnlyList<RoomDefinition> gridRoomPool)
        {
            width = Mathf.Max(1, gridWidth);
            height = Mathf.Max(1, gridHeight);
            roomSize = gridRoomSize;
            visibleRadius = Mathf.Max(0, gridVisibleRadius);
            shuffleIntervalSeconds = Mathf.Max(0f, gridShuffleIntervalSeconds);
            fallbackRoom = gridFallbackRoom;
            startRoom = gridFallbackRoom;
            exitRoom = gridFallbackRoom;

            roomPool.Clear();
            if (gridRoomPool == null)
            {
                return;
            }

            for (int i = 0; i < gridRoomPool.Count; i++)
            {
                if (gridRoomPool[i] != null)
                {
                    roomPool.Add(gridRoomPool[i]);
                }
            }
        }
    }
}
