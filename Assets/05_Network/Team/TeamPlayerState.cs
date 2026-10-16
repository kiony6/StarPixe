using UnityEngine;

namespace StarPixelSurge.Network
{
    /// <summary>
    /// 队伍机制的纯数据视图：一名玩家在某一逻辑帧的位置与存活状态。
    /// 复活判定与跨玩家增益均基于该结构计算，使房主运行时与离线仿真共用同一套逻辑。
    /// </summary>
    public struct TeamPlayerState
    {
        public int PlayerId;
        public Vector2 Position;
        public bool Alive;

        public TeamPlayerState(int playerId, Vector2 position, bool alive)
        {
            PlayerId = playerId;
            Position = position;
            Alive = alive;
        }
    }
}
