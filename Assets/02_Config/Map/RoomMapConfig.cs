using UnityEngine;

namespace StarPixelSurge.Config
{
    /// <summary>
    /// 随机房间地图生成参数。控制房间数量、尺寸、走廊宽度与配色。
    /// </summary>
    [CreateAssetMenu(menuName = "StarPixelSurge/Config/RoomMap", fileName = "RoomMap_")]
    public class RoomMapConfig : ScriptableObject
    {
        public string Id = "neon_city";
        public string DisplayName = "霓虹都市";

        [Header("Layout")]
        [Tooltip("网格可容纳的最大房间列数/行数")]
        public int GridColumns = 7;
        public int GridRows = 7;

        [Tooltip("每局生成的房间数量区间（含起点房间）")]
        public int MinRooms = 8;
        public int MaxRooms = 13;

        [Tooltip("单个房间的瓦片宽高")]
        public int RoomWidth = 16;
        public int RoomHeight = 12;

        [Tooltip("相邻房间之间的走廊长度（瓦片）")]
        public int CorridorLength = 7;

        [Tooltip("走廊宽度（瓦片）")]
        public int CorridorWidth = 3;

        [Header("Rendering")]
        [Tooltip("每个瓦片的像素尺寸")]
        public int TilePixels = 8;

        public Color FloorColor = new Color(0.11f, 0.12f, 0.20f);
        public Color CorridorColor = new Color(0.08f, 0.09f, 0.15f);
        public Color WallColor = new Color(0.30f, 0.22f, 0.45f);
        public Color StartRoomColor = new Color(0.12f, 0.20f, 0.24f);
        public Color TreasureRoomColor = new Color(0.26f, 0.21f, 0.10f);
        public Color EliteRoomColor = new Color(0.24f, 0.11f, 0.16f);
        public Color BossRoomColor = new Color(0.28f, 0.09f, 0.28f);
        public Color VoidColor = new Color(0.02f, 0.02f, 0.04f, 1f);

        [Header("Walls")]
        [Tooltip("是否生成阻挡玩家的墙体碰撞")]
        public bool GenerateWallColliders = true;
    }
}
