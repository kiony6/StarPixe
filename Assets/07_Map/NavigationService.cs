using StarPixelSurge.Framework;
using StarPixelSurge.GameCore;
using UnityEngine;

namespace StarPixelSurge.Map
{
    /// <summary>
    /// 导航服务。持有当前地图并维护以玩家为源点的流场，怪物通过它取得绕墙移动方向。
    /// 玩家跨瓦片或地图变更时重建流场，并对重建做最小时间间隔节流。
    /// </summary>
    public class NavigationService : MonoSingleton<NavigationService>
    {
        [SerializeField] private float rebuildInterval = 0.1f;

        private RoomMap _map;
        private FlowField _flowField;
        private Vector2Int _lastSource = new Vector2Int(int.MinValue, int.MinValue);
        private bool _dirty = true;
        private float _timer;

        public bool IsReady => _flowField != null && _map != null;

        public void SetMap(RoomMap map)
        {
            _map = map;
            _flowField = map != null ? new FlowField(map.Width, map.Height) : null;
            _lastSource = new Vector2Int(int.MinValue, int.MinValue);
            _dirty = true;
            _timer = 0f;
        }

        public void ClearMap()
        {
            SetMap(null);
        }

        /// <summary>标记流场需要重建（如地图结构发生变化）。</summary>
        public void Invalidate()
        {
            _dirty = true;
        }

        private void Update()
        {
            if (_map == null || _flowField == null)
            {
                return;
            }

            var source = new Vector2Int(
                Mathf.FloorToInt(_map.StartPosition.x),
                Mathf.FloorToInt(_map.StartPosition.y));

            // 以玩家所在瓦片作为源点。
            var player = PlayerController.Instance;
            if (player != null)
            {
                var position = player.transform.position;
                source = new Vector2Int(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y));
            }

            _timer += Time.deltaTime;

            if (source != _lastSource)
            {
                _dirty = true;
                _lastSource = source;
            }

            if (!_dirty || _timer < rebuildInterval)
            {
                return;
            }

            _timer = 0f;
            _dirty = false;
            _flowField.Build(_map, source);
        }

        /// <summary>取得绕墙靠近玩家的移动方向。流场不可用时返回 false。</summary>
        public bool TryGetDirection(Vector2 from, out Vector2 direction)
        {
            direction = Vector2.zero;
            if (_flowField == null)
            {
                return false;
            }

            return _flowField.TryGetDirection(from, out direction);
        }

        /// <summary>世界坐标是否位于可通行地板。无地图时视为可通行。</summary>
        public bool IsWalkable(Vector2 world)
        {
            return _map == null || _map.IsFloor(world);
        }
    }
}
