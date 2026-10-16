using System.Collections.Generic;
using UnityEngine;

namespace StarPixelSurge.Map
{
    /// <summary>
    /// 瓦片流场寻路。以玩家所在瓦片为源点做 BFS，生成每个瓦片到源点的步数，
    /// 怪物按步数梯度采样移动方向，从而绕开墙体且无需逐个寻路。
    /// </summary>
    public class FlowField
    {
        private static readonly Vector2Int[] Neighbours =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
            new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1)
        };

        private readonly int _width;
        private readonly int _height;
        private readonly int[] _distance;
        private readonly Queue<Vector2Int> _queue = new Queue<Vector2Int>();

        public FlowField(int width, int height)
        {
            _width = Mathf.Max(1, width);
            _height = Mathf.Max(1, height);
            _distance = new int[_width * _height];
        }

        public Vector2Int Source { get; private set; } = new Vector2Int(-1, -1);

        /// <summary>以 sourceTile 为源点重建流场。不可通行瓦片距离为 Unreachable。</summary>
        public void Build(RoomMap map, Vector2Int sourceTile)
        {
            if (map == null)
            {
                return;
            }

            if (sourceTile.x < 0 || sourceTile.y < 0 || sourceTile.x >= _width || sourceTile.y >= _height || !map.IsFloorTile(sourceTile.x, sourceTile.y))
            {
                sourceTile = ClampToFloor(map, sourceTile);
                if (sourceTile.x < 0)
                {
                    return;
                }
            }

            Source = sourceTile;
            for (var i = 0; i < _distance.Length; i++)
            {
                _distance[i] = int.MaxValue;
            }

            _queue.Clear();
            _distance[Index(sourceTile.x, sourceTile.y)] = 0;
            _queue.Enqueue(sourceTile);

            while (_queue.Count > 0)
            {
                var current = _queue.Dequeue();
                var currentDistance = _distance[Index(current.x, current.y)];

                for (var i = 0; i < Neighbours.Length; i++)
                {
                    var offset = Neighbours[i];
                    var next = new Vector2Int(current.x + offset.x, current.y + offset.y);
                    if (next.x < 0 || next.y < 0 || next.x >= _width || next.y >= _height)
                    {
                        continue;
                    }

                    if (!map.IsFloorTile(next.x, next.y))
                    {
                        continue;
                    }

                    // 对角移动时禁止穿越墙角。
                    if (offset.x != 0 && offset.y != 0 &&
                        (!map.IsFloorTile(current.x + offset.x, current.y) || !map.IsFloorTile(current.x, current.y + offset.y)))
                    {
                        continue;
                    }

                    var index = Index(next.x, next.y);
                    if (_distance[index] <= currentDistance + 1)
                    {
                        continue;
                    }

                    _distance[index] = currentDistance + 1;
                    _queue.Enqueue(next);
                }
            }
        }

        /// <summary>取得从 from 位置沿流场靠近源点的方向。不可达或已在源点返回 false。</summary>
        public bool TryGetDirection(Vector2 from, out Vector2 direction)
        {
            direction = Vector2.zero;

            var tile = new Vector2Int(Mathf.FloorToInt(from.x), Mathf.FloorToInt(from.y));
            if (tile.x < 0 || tile.y < 0 || tile.x >= _width || tile.y >= _height)
            {
                return false;
            }

            var current = _distance[Index(tile.x, tile.y)];
            if (current == int.MaxValue || current == 0)
            {
                return false;
            }

            var best = current;
            var bestOffset = Vector2Int.zero;
            for (var i = 0; i < Neighbours.Length; i++)
            {
                var offset = Neighbours[i];
                var next = new Vector2Int(tile.x + offset.x, tile.y + offset.y);
                if (next.x < 0 || next.y < 0 || next.x >= _width || next.y >= _height)
                {
                    continue;
                }

                var value = _distance[Index(next.x, next.y)];
                if (value < best)
                {
                    best = value;
                    bestOffset = offset;
                }
            }

            if (bestOffset == Vector2Int.zero)
            {
                return false;
            }

            var target = new Vector2(tile.x + bestOffset.x + 0.5f, tile.y + bestOffset.y + 0.5f);
            var delta = target - from;
            if (delta.sqrMagnitude < 0.0001f)
            {
                direction = new Vector2(bestOffset.x, bestOffset.y).normalized;
            }
            else
            {
                direction = delta.normalized;
            }

            return true;
        }

        private Vector2Int ClampToFloor(RoomMap map, Vector2Int tile)
        {
            for (var radius = 1; radius <= 4; radius++)
            {
                for (var dx = -radius; dx <= radius; dx++)
                {
                    for (var dy = -radius; dy <= radius; dy++)
                    {
                        var candidate = new Vector2Int(tile.x + dx, tile.y + dy);
                        if (map.IsFloorTile(candidate.x, candidate.y))
                        {
                            return candidate;
                        }
                    }
                }
            }

            return new Vector2Int(-1, -1);
        }

        private int Index(int x, int y)
        {
            return y * _width + x;
        }
    }
}
