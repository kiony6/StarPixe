using System;
using UnityEngine;

namespace StarPixelSurge.Map
{
    /// <summary>
    /// 房间类型。
    /// </summary>
    public enum RoomType
    {
        Normal,      // 普通房间
        Elite,       // 精英房间
        Boss,        // Boss房间
        Treasure,    // 宝箱房间
        Shop,        // 商店房间
        Start,       // 起始房间
        Secret,      // 秘密房间
    }

    /// <summary>
    /// 房间配置。
    /// </summary>
    [Serializable]
    public class RoomConfig
    {
        public RoomType Type;
        public Vector2Int Position;
        public int Width = 10;
        public int Height = 8;
        public bool IsCleared;
    }

    /// <summary>
    /// 房间管理器：管理地图中所有房间的状态和类型。
    /// </summary>
    public class RoomManager : MonoBehaviour
    {
        [SerializeField] private RoomConfig[] _rooms;
        [SerializeField] private RoomType[] _roomTypeWeights = new[]
        {
            RoomType.Normal,
            RoomType.Normal,
            RoomType.Normal,
            RoomType.Elite,
            RoomType.Treasure,
            RoomType.Shop,
        };

        public RoomConfig CurrentRoom { get; private set; }

        /// <summary>设置当前所在房间。</summary>
        public void SetCurrentRoom(RoomConfig room)
        {
            CurrentRoom = room;
            if (room != null && !room.IsCleared)
            {
                OnRoomEntered(room);
            }
        }

        /// <summary>房间进入事件。</summary>
        public event Action<RoomConfig> OnRoomEnteredEvent;

        private void OnRoomEntered(RoomConfig room)
        {
            OnRoomEnteredEvent?.Invoke(room);

            switch (room.Type)
            {
                case RoomType.Elite:
                    // 生成精英怪
                    if (WaveManager.HasInstance)
                    {
                        WaveManager.Instance.SpawnEliteEncounter(5);
                    }
                    break;
                case RoomType.Boss:
                    // 生成 Boss
                    if (WaveManager.HasInstance)
                    {
                        WaveManager.Instance.SpawnBossEncounter(transform.position, 6);
                    }
                    break;
            }
        }

        /// <summary>标记房间已清理。</summary>
        public void MarkRoomCleared(RoomConfig room)
        {
            if (room != null)
            {
                room.IsCleared = true;
            }
        }
    }
}
