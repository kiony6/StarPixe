using System.Collections.Generic;
using UnityEngine;

namespace StarPixelSurge.Network
{
    /// <summary>
    /// 队友复活追踪器（纯逻辑）。倒地的玩家若附近存在存活队友，则随时间积累复活进度；
    /// 进度达到阈值后复活并按比例恢复生命。房主权威运行该逻辑并广播结果状态，
    /// 离线仿真复用同一实现验证收敛，避免两套实现漂移。
    /// </summary>
    public class ReviveTracker
    {
        private readonly Dictionary<int, float> _progress = new Dictionary<int, float>();
        private readonly List<int> _stale = new List<int>();

        /// <summary>复活所需的队友接近距离。</summary>
        public float ReviveRadius { get; set; } = 2.5f;

        /// <summary>从零进度到复活所需的时间（秒）。</summary>
        public float ReviveDuration { get; set; } = 3f;

        /// <summary>复活后恢复的生命占最大生命的比例。</summary>
        public float ReviveHealthFraction { get; set; } = 0.5f;

        public void Clear()
        {
            _progress.Clear();
        }

        /// <summary>读取某玩家当前复活进度（[0,1]）。存活或未知玩家返回 0。</summary>
        public float GetProgress(int playerId)
        {
            return _progress.TryGetValue(playerId, out var value) ? Mathf.Clamp01(value / Mathf.Max(0.01f, ReviveDuration)) : 0f;
        }

        /// <summary>
        /// 推进一帧。存活玩家清空进度；倒地玩家由附近存活队友累积进度，
        /// 进度满的玩家加入 <paramref name="revivedOut"/> 并从进度表中移除。
        /// </summary>
        public void Tick(IReadOnlyList<TeamPlayerState> players, float deltaTime, List<int> revivedOut)
        {
            revivedOut?.Clear();
            if (players == null || players.Count == 0)
            {
                _progress.Clear();
                return;
            }

            var radiusSqr = Mathf.Max(0.01f, ReviveRadius) * Mathf.Max(0.01f, ReviveRadius);
            var step = Mathf.Max(0f, deltaTime);

            for (var i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player.Alive)
                {
                    _progress.Remove(player.PlayerId);
                    continue;
                }

                if (!HasLivingHelper(players, player.Position, radiusSqr))
                {
                    _progress.Remove(player.PlayerId);
                    continue;
                }

                _progress.TryGetValue(player.PlayerId, out var current);
                current += step;
                if (current >= ReviveDuration)
                {
                    _progress.Remove(player.PlayerId);
                    revivedOut?.Add(player.PlayerId);
                }
                else
                {
                    _progress[player.PlayerId] = current;
                }
            }

            // 清理列表中已不存在玩家的残留进度，避免 ID 复用误判。
            if (_progress.Count > 0)
            {
                PruneMissing(players);
            }
        }

        private bool HasLivingHelper(IReadOnlyList<TeamPlayerState> players, Vector2 origin, float radiusSqr)
        {
            for (var i = 0; i < players.Count; i++)
            {
                if (!players[i].Alive)
                {
                    continue;
                }

                if ((players[i].Position - origin).sqrMagnitude <= radiusSqr)
                {
                    return true;
                }
            }

            return false;
        }

        private void PruneMissing(IReadOnlyList<TeamPlayerState> players)
        {
            _stale.Clear();
            foreach (var pair in _progress)
            {
                var found = false;
                for (var i = 0; i < players.Count; i++)
                {
                    if (players[i].PlayerId == pair.Key && !players[i].Alive)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    _stale.Add(pair.Key);
                }
            }

            for (var i = 0; i < _stale.Count; i++)
            {
                _progress.Remove(_stale[i]);
            }
        }
    }
}
