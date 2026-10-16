using System.Collections.Generic;
using UnityEngine;

namespace StarPixelSurge.GameCore
{
    /// <summary>
    /// 房间类型。
    /// </summary>
    public enum RoomType
    {
        Combat,      // 战斗房：刷怪物
        Shop,        // 商店房：买道具
        Treasure,    // 宝箱房：捡宝箱
        Rest,        // 休息房：恢复生命
        Boss,        // Boss房：打Boss
    }

    /// <summary>
    /// 房间数据。
    /// </summary>
    [System.Serializable]
    public class RoomData
    {
        public string Id;
        public RoomType Type;
        public Vector2Int Position;
        public bool IsCleared;
        public List<string> ConnectedRooms = new List<string>();
    }

    /// <summary>
    /// 房间管理器：管理地图上的所有房间。
    /// </summary>
    public class RoomManager : MonoBehaviour
    {
        public static RoomManager Instance { get; private set; }

        public List<RoomData> Rooms = new List<RoomData>();
        public event System.Action<RoomData> OnRoomEntered;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>生成随机房间布局。</summary>
        public void GenerateRandomMap(int roomCount)
        {
            Rooms.Clear();

            // 从中心开始
            var centerRoom = new RoomData
            {
                Id = "room_0",
                Type = RoomType.Combat,
                Position = Vector2Int.zero,
                IsCleared = false
            };
            Rooms.Add(centerRoom);

            // 随机生成其他房间
            for (var i = 1; i < roomCount; i++)
            {
                var randomType = GetRandomRoomType(i);
                var randomPos = GetRandomAdjacentPosition();

                var room = new RoomData
                {
                    Id = $"room_{i}",
                    Type = randomType,
                    Position = randomPos,
                    IsCleared = false
                };
                Rooms.Add(room);

                // 连接到相邻房间
                ConnectToNearestRoom(room);
            }

            Debug.Log($"[Room] 生成了 {Rooms.Count} 个房间");
        }

        private RoomType GetRandomRoomType(int index)
        {
            // 最后一个房间是Boss房
            if (index >= 14) return RoomType.Boss;

            var roll = Random.value;
            if (roll < 0.6f) return RoomType.Combat;      // 60% 战斗房
            if (roll < 0.75f) return RoomType.Shop;       // 15% 商店房
            if (roll < 0.9f) return RoomType.Treasure;     // 15% 宝箱房
            return RoomType.Rest;                          // 10% 休息房
        }

        private Vector2Int GetRandomAdjacentPosition()
        {
            var directions = new[]
            {
                Vector2Int.up,
                Vector2Int.down,
                Vector2Int.left,
                Vector2Int.right
            };
            return directions[Random.Range(0, directions.Length)];
        }

        private void ConnectToNearestRoom(RoomData newRoom)
        {
            foreach (var room in Rooms)
            {
                if (room.Id == newRoom.Id) continue;

                var distance = Vector2Int.Distance(room.Position, newRoom.Position);
                if (distance <= 1.5f)
                {
                    room.ConnectedRooms.Add(newRoom.Id);
                    newRoom.ConnectedRooms.Add(room.Id);
                }
            }
        }

        /// <summary>进入房间。</summary>
        public void EnterRoom(RoomData room)
        {
            OnRoomEntered?.Invoke(room);
            Debug.Log($"[Room] 进入房间: {room.Type}");
        }
    }
}
