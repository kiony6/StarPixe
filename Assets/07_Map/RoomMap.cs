using System.Collections.Generic;
using UnityEngine;

namespace StarPixelSurge.Map
{
    public enum RoomType
    {
        Start = 0,
        Normal = 1,
        Treasure = 2,
        Elite = 3,
        Boss = 4
    }

    /// <summary>
    /// 一个已生成的房间：记录瓦片矩形、世界中心与类型。
    /// </summary>
    public readonly struct Room
    {
        public readonly RectInt TileRect;
        public readonly RoomType Type;

        public Room(RectInt tileRect, RoomType type)
        {
            TileRect = tileRect;
            Type = type;
        }

        /// <summary>房间中心的世界坐标（瓦片中心对齐）。</summary>
        public Vector2 WorldCenter => new Vector2(TileRect.x + TileRect.width * 0.5f, TileRect.y + TileRect.height * 0.5f);
    }

    /// <summary>
    /// 随机房间地图。持有瓦片地板网格、房间列表与世界边界，供玩家落点、怪物生成与查询使用。
    /// </summary>
    public class RoomMap : MonoBehaviour
    {
        public const float TileSize = 1f;

        private bool[,] _floor;
        private int _width;
        private int _height;
        private readonly List<Room> _rooms = new List<Room>();

        public IReadOnlyList<Room> Rooms => _rooms;

        public int Width => _width;

        public int Height => _height;

        public Vector2 WorldMin => Vector2.zero;

        public Vector2 WorldMax => new Vector2(_width, _height);

        public Vector2 StartPosition { get; private set; }

        public Room StartRoom { get; private set; }

        public Room BossRoom { get; private set; }

        /// <summary>由 MapGenerator 在构建完成后注入数据。</summary>
        public void Initialize(bool[,] floor, int width, int height, List<Room> rooms, Vector2 startPosition)
        {
            _floor = floor;
            _width = width;
            _height = height;
            _rooms.Clear();
            _rooms.AddRange(rooms);
            StartPosition = startPosition;

            foreach (var room in rooms)
            {
                if (room.Type == RoomType.Start)
                {
                    StartRoom = room;
                }
                else if (room.Type == RoomType.Boss)
                {
                    BossRoom = room;
                }
            }
        }

        public bool IsFloor(Vector2 world)
        {
            var x = Mathf.FloorToInt(world.x / TileSize);
            var y = Mathf.FloorToInt(world.y / TileSize);
            return IsFloorTile(x, y);
        }

        public bool IsFloorTile(int x, int y)
        {
            return _floor != null && x >= 0 && y >= 0 && x < _width && y < _height && _floor[x, y];
        }

        public Vector2 TileToWorld(int x, int y)
        {
            return new Vector2(x + 0.5f, y + 0.5f);
        }

        /// <summary>
        /// 在距地心半径范围内的可通行位置取得一个生成点。找不到时回退到范围内的随机点。
        /// </summary>
        public Vector2 GetSpawnPosition(Vector2 center, float radius)
        {
            for (var attempt = 0; attempt < 24; attempt++)
            {
                var angle = Random.Range(0f, Mathf.PI * 2f);
                var distance = Random.Range(radius * 0.55f, radius);
                var candidate = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                if (IsFloor(candidate))
                {
                    return candidate;
                }
            }

            // 回退：在玩家所在房间与其邻域内找可通行瓦片。
            for (var attempt = 0; attempt < 24; attempt++)
            {
                var tileX = Mathf.FloorToInt(center.x) + Random.Range(-6, 7);
                var tileY = Mathf.FloorToInt(center.y) + Random.Range(-6, 7);
                if (IsFloorTile(tileX, tileY))
                {
                    return TileToWorld(tileX, tileY);
                }
            }

            return center;
        }

        public Room GetRoomAt(Vector2 world)
        {
            var tile = new Vector2Int(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.y));
            foreach (var room in _rooms)
            {
                if (room.TileRect.Contains(tile))
                {
                    return room;
                }
            }

            return default;
        }
    }
}
