using System.Collections.Generic;
using UnityEngine;

namespace Week14.Map
{
    [CreateAssetMenu(menuName = "Week14/Map/Room Grid Config", fileName = "RoomGridConfig")]
    public sealed class RoomGridConfig : ScriptableObject
    {
        [Header("그리드")]
        [SerializeField, Min(1)] private int width = 5;
        [SerializeField, Min(1)] private int height = 5;
        [SerializeField] private Vector2 roomSize = new Vector2(24f, 14f);
        [SerializeField, Min(0)] private int visibleRadius = 1;

        [Header("셔플")]
        [SerializeField, Min(0f)] private float shuffleIntervalSeconds = 15f;
        [SerializeField] private bool useFixedSeed;
        [SerializeField] private int fixedSeed = 1004;

        [Header("방 풀")]
        [SerializeField] private RoomDefinition startRoom;
        [SerializeField] private RoomDefinition exitRoom;
        [SerializeField] private RoomDefinition fallbackRoom;
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
