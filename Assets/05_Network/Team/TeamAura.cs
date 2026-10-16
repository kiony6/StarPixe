using System.Collections.Generic;
using UnityEngine;

namespace StarPixelSurge.Network
{
    /// <summary>
    /// 跨玩家增益（纯逻辑）。存活队友彼此靠近时形成增益光环，
    /// 每有一名范围内的存活队友，玩家获得一份固定增益。房主按帧计算并写入玩家数值，
    /// 离线仿真复用同一实现验证。
    /// </summary>
    public static class TeamAura
    {
        /// <summary>统计指定玩家光环范围内的存活队友数量（不含自身）。</summary>
        public static int NearbyAllyCount(IReadOnlyList<TeamPlayerState> players, int playerId, float radius)
        {
            if (players == null)
            {
                return 0;
            }

            var radiusSqr = Mathf.Max(0.01f, radius) * Mathf.Max(0.01f, radius);
            var origin = Vector2.zero;
            var found = false;
            for (var i = 0; i < players.Count; i++)
            {
                if (players[i].PlayerId == playerId)
                {
                    origin = players[i].Position;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < players.Count; i++)
            {
                var other = players[i];
                if (other.PlayerId == playerId || !other.Alive)
                {
                    continue;
                }

                if ((other.Position - origin).sqrMagnitude <= radiusSqr)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>计算指定玩家应得的增益值（数量 × 单位增益）。</summary>
        public static float BonusFor(IReadOnlyList<TeamPlayerState> players, int playerId, float radius, float perAllyBonus)
        {
            return NearbyAllyCount(players, playerId, radius) * perAllyBonus;
        }
    }
}
