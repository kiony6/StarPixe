using System;
using StarPixelSurge.GameCore;
using UnityEngine;

namespace StarPixelSurge.Map
{
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
