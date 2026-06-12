using System;
using System.Collections.Generic;
using UnityEngine;

namespace Week14.Map
{
    public sealed class RoomGridManager : MonoBehaviour
    {
        private const string SlotNamePrefix = "RoomSlot_";

        [SerializeField] private RoomGridConfig config;
        [SerializeField] private Transform player;
        [SerializeField] private Transform slotsRoot;
        [SerializeField] private bool buildOnStart = true;
        [SerializeField] private bool shuffleOnTimer = true;

        private readonly List<RoomSlot> slots = new List<RoomSlot>();
        private readonly Dictionary<Vector2Int, RoomSlot> slotByPosition = new Dictionary<Vector2Int, RoomSlot>();
        private System.Random random;
        private Vector2Int currentRoom;
        private float nextShuffleAt;
        private bool initialized;

        public event Action<Vector2Int> CurrentRoomChanged;
        public event Action<RoomSlot> CurrentRoomSlotChanged;
        public event Action<RoomSlot> ExitRoomEntered;
        public event Action RoomsShuffled;

        public Vector2Int CurrentRoom => currentRoom;
        public RoomSlot CurrentSlot => GetSlot(currentRoom);
        public float ShuffleIntervalSeconds => config != null ? config.ShuffleIntervalSeconds : 0f;
        public float SecondsUntilShuffle => Mathf.Max(0f, nextShuffleAt - Time.time);

        public void ConfigureRuntime(RoomGridConfig gridConfig, Transform playerTransform, Transform root)
        {
            config = gridConfig;
            player = playerTransform;
            slotsRoot = root;
        }

        private void Start()
        {
            if (slotsRoot == null)
            {
                slotsRoot = transform;
            }

            random = config != null && config.UseFixedSeed
                ? new System.Random(config.FixedSeed)
                : new System.Random();

            if (buildOnStart)
            {
                BuildGrid();
            }
        }

        private void Update()
        {
            if (!initialized)
            {
                return;
            }

            ResolvePlayer();
            UpdateCurrentRoom();

            if (shuffleOnTimer && config.ShuffleIntervalSeconds > 0f && Time.time >= nextShuffleAt)
            {
                ShuffleRooms();
            }
        }

        public void BuildGrid()
        {
            if (slotsRoot == null)
            {
                slotsRoot = transform;
            }

            if (config == null)
            {
                Debug.LogWarning($"{nameof(RoomGridManager)} requires {nameof(RoomGridConfig)}.", this);
                return;
            }

            ClearRuntimeSlots();
            slots.Clear();
            slotByPosition.Clear();

            int startX = -(config.Width / 2);
            int startY = -(config.Height / 2);
            Vector2Int exitPosition = new Vector2Int(startX + config.Width - 1, startY + config.Height - 1);

            for (int y = 0; y < config.Height; y++)
            {
                for (int x = 0; x < config.Width; x++)
                {
                    Vector2Int gridPosition = new Vector2Int(startX + x, startY + y);
                    RoomSlot slot = CreateSlot(gridPosition);
                    slot.SetDefinition(PickRoomDefinition(gridPosition, exitPosition));
                    slots.Add(slot);
                    slotByPosition.Add(gridPosition, slot);
                }
            }

            ResolvePlayer();
            currentRoom = FindCurrentRoom();
            UpdateVisibility();
            NotifyCurrentRoomEntered();
            CurrentRoomChanged?.Invoke(currentRoom);
            CurrentRoomSlotChanged?.Invoke(CurrentSlot);
            nextShuffleAt = Time.time + config.ShuffleIntervalSeconds;
            initialized = true;
        }

        public void ShuffleRooms()
        {
            if (!initialized || slots.Count <= 1)
            {
                return;
            }

            List<RoomSlot> movableSlots = new List<RoomSlot>();
            List<RoomDefinition> movableDefinitions = new List<RoomDefinition>();

            for (int i = 0; i < slots.Count; i++)
            {
                RoomSlot slot = slots[i];
                if (slot.GridPosition == currentRoom)
                {
                    continue;
                }

                movableSlots.Add(slot);
                movableDefinitions.Add(slot.Definition);
            }

            ShuffleList(movableDefinitions);

            for (int i = 0; i < movableSlots.Count; i++)
            {
                movableSlots[i].SetDefinition(movableDefinitions[i]);
            }

            UpdateVisibility();
            nextShuffleAt = Time.time + config.ShuffleIntervalSeconds;
            RoomsShuffled?.Invoke();
        }

        private RoomSlot CreateSlot(Vector2Int gridPosition)
        {
            GameObject slotObject = new GameObject($"{SlotNamePrefix}{gridPosition.x}_{gridPosition.y}");
            slotObject.transform.SetParent(slotsRoot, false);
            slotObject.transform.position = GridToWorld(gridPosition);

            RoomSlot slot = slotObject.AddComponent<RoomSlot>();
            slot.Initialize(gridPosition);
            return slot;
        }

        private void ClearRuntimeSlots()
        {
            for (int i = slotsRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = slotsRoot.GetChild(i);
                if (!child.name.StartsWith(SlotNamePrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                Destroy(child.gameObject);
            }
        }

        private void ResolvePlayer()
        {
            if (player != null)
            {
                return;
            }

            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                player = playerObject.transform;
            }
        }

        private void UpdateCurrentRoom()
        {
            Vector2Int nextRoom = FindCurrentRoom();
            if (nextRoom == currentRoom)
            {
                return;
            }

            currentRoom = nextRoom;
            UpdateVisibility();
            NotifyCurrentRoomEntered();
            CurrentRoomChanged?.Invoke(currentRoom);
            CurrentRoomSlotChanged?.Invoke(CurrentSlot);
        }

        private void NotifyCurrentRoomEntered()
        {
            RoomSlot slot = CurrentSlot;
            if (slot == null || slot.SpawnedRoom == null)
            {
                return;
            }

            MonoBehaviour[] behaviours = slot.SpawnedRoom.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IRoomEnterHandler enterHandler)
                {
                    enterHandler.OnPlayerEnteredRoom(slot);
                }
            }

            if (slot.Definition != null && slot.Definition.RoomType == RoomType.Exit)
            {
                ExitRoomEntered?.Invoke(slot);
                Debug.Log($"Exit room entered: {slot.GridPosition}", this);
            }
        }

        private RoomSlot GetSlot(Vector2Int gridPosition)
        {
            return slotByPosition.TryGetValue(gridPosition, out RoomSlot slot) ? slot : null;
        }

        private Vector2Int FindCurrentRoom()
        {
            if (player == null)
            {
                return currentRoom;
            }

            Vector2 roomSize = config.RoomSize;
            Vector2Int roomPosition = new Vector2Int(
                Mathf.RoundToInt(player.position.x / roomSize.x),
                Mathf.RoundToInt(player.position.y / roomSize.y));

            if (slotByPosition.ContainsKey(roomPosition))
            {
                return roomPosition;
            }

            return FindNearestRoom(player.position);
        }

        private Vector2Int FindNearestRoom(Vector3 worldPosition)
        {
            float bestDistance = float.MaxValue;
            Vector2Int bestPosition = currentRoom;

            for (int i = 0; i < slots.Count; i++)
            {
                float distance = Vector2.SqrMagnitude((Vector2)slots[i].transform.position - (Vector2)worldPosition);
                if (distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                bestPosition = slots[i].GridPosition;
            }

            return bestPosition;
        }

        private void UpdateVisibility()
        {
            for (int i = 0; i < slots.Count; i++)
            {
                RoomSlot slot = slots[i];
                int dx = Mathf.Abs(slot.GridPosition.x - currentRoom.x);
                int dy = Mathf.Abs(slot.GridPosition.y - currentRoom.y);
                slot.SetVisible(dx <= config.VisibleRadius && dy <= config.VisibleRadius);
            }
        }

        private Vector3 GridToWorld(Vector2Int gridPosition)
        {
            Vector2 roomSize = config.RoomSize;
            return new Vector3(gridPosition.x * roomSize.x, gridPosition.y * roomSize.y, 0f);
        }

        private RoomDefinition PickRoomDefinition(Vector2Int gridPosition, Vector2Int exitPosition)
        {
            if (gridPosition == Vector2Int.zero && config.StartRoom != null)
            {
                return config.StartRoom;
            }

            if (gridPosition == exitPosition && config.ExitRoom != null)
            {
                return config.ExitRoom;
            }

            IReadOnlyList<RoomDefinition> roomPool = config.RoomPool;
            int totalWeight = 0;

            for (int i = 0; i < roomPool.Count; i++)
            {
                if (roomPool[i] != null)
                {
                    totalWeight += Mathf.Max(0, roomPool[i].Weight);
                }
            }

            if (totalWeight <= 0)
            {
                return config.FallbackRoom;
            }

            int roll = random.Next(0, totalWeight);
            int cursor = 0;

            for (int i = 0; i < roomPool.Count; i++)
            {
                RoomDefinition room = roomPool[i];
                if (room == null)
                {
                    continue;
                }

                cursor += Mathf.Max(0, room.Weight);
                if (roll < cursor)
                {
                    return room;
                }
            }

            return config.FallbackRoom;
        }

        private void ShuffleList<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int swapIndex = random.Next(0, i + 1);
                (list[i], list[swapIndex]) = (list[swapIndex], list[i]);
            }
        }
    }
}
