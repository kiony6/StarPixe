using System.Collections.Generic;
using StarPixelSurge.Framework;
using StarPixelSurge.Map;
using UnityEngine;

namespace StarPixelSurge.GameCore
{
    /// <summary>
    /// 房间事件调度。监听玩家所在房间，首次进入特殊房间时触发对应机制：
    /// 宝箱房治疗并奖励经验、精英房追加精英遭遇、BOSS 房刷新首领遭遇。
    /// </summary>
    public class RoomDirector : MonoSingleton<RoomDirector>
    {
        [SerializeField] private float treasureHealPercent = 0.3f;
        [SerializeField] private float treasureExperience = 12f;

        private readonly HashSet<Vector2Int> _triggered = new HashSet<Vector2Int>();
        private RoomMap _map;
        private PlayerStats _stats;
        private PlayerExperience _experience;

        public void Configure(RoomMap map, PlayerStats stats, PlayerExperience experience)
        {
            _map = map;
            _stats = stats;
            _experience = experience;
            _triggered.Clear();
        }

        public void ClearMap()
        {
            _map = null;
            _triggered.Clear();
        }

        private void Update()
        {
            if (_map == null)
            {
                return;
            }

            if (!GameManager.HasInstance || GameManager.Instance.State != GameState.Playing)
            {
                return;
            }

            var player = PlayerController.Instance;
            if (player == null)
            {
                return;
            }

            var room = _map.GetRoomAt(player.transform.position);
            var key = new Vector2Int(room.TileRect.x, room.TileRect.y);
            if (_triggered.Contains(key))
            {
                return;
            }

            switch (room.Type)
            {
                case RoomType.Start:
                    _triggered.Add(key);
                    break;
                case RoomType.Treasure:
                    _triggered.Add(key);
                    TriggerTreasure(room);
                    break;
                case RoomType.Elite:
                    _triggered.Add(key);
                    TriggerElite(room);
                    break;
                case RoomType.Boss:
                    _triggered.Add(key);
                    TriggerBoss(room);
                    break;
            }
        }

        private void TriggerTreasure(Room room)
        {
            if (_stats != null)
            {
                _stats.Heal(_stats.MaxHealth * treasureHealPercent);
            }

            if (_experience != null)
            {
                _experience.AddExperience(treasureExperience);
            }

            EventBus.Publish(new RoomMessageEvent("宝箱房间：生命恢复，获得经验！", new Color(1f, 0.85f, 0.35f)));
        }

        private void TriggerElite(Room room)
        {
            if (WaveManager.HasInstance)
            {
                WaveManager.Instance.SpawnEliteEncounter();
            }

            EventBus.Publish(new RoomMessageEvent("精英房间：强敌来袭！", new Color(1f, 0.5f, 0.55f)));
        }

        private void TriggerBoss(Room room)
        {
            if (WaveManager.HasInstance)
            {
                WaveManager.Instance.SpawnBossEncounter(room.WorldCenter);
            }

            EventBus.Publish(new RoomMessageEvent("BOSS 房间：首领现身！", new Color(0.85f, 0.4f, 1f)));
        }
    }
}
