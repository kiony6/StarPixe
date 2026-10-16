using System;
using UnityEngine;

namespace StarPixelSurge.Network
{
    /// <summary>网络实体类别，用于快照中区分玩家、怪物、召唤物与弹道。</summary>
    public enum EntityType : byte
    {
        Player = 0,
        Monster = 1,
        Summon = 2,
        Projectile = 3
    }

    /// <summary>快照中实体状态位标记。</summary>
    [Flags]
    public enum EntityStateFlags : ushort
    {
        None = 0,
        Alive = 1 << 0,
        Boss = 1 << 1,
        Enraged = 1 << 2
    }

    /// <summary>网络消息类型，作为载荷首字节用于分发。</summary>
    public enum NetworkMessageType : byte
    {
        PlayerInput = 1,
        StateSnapshot = 2,
        HostHeartbeat = 3,
        HostMigration = 4,
        MatchStart = 5,
        PlayerReady = 6,
        Roster = 7,
        JoinRequest = 8
    }

    /// <summary>
    /// 成员的传输层端点。名册随包下发各成员端点，使任一成员在房主迁移后
    /// 都能直接寻址其余成员，而无需依赖旧的房主地址。
    /// </summary>
    [Serializable]
    public struct PeerEndPoint
    {
        public int PlayerId;
        public string Address;
        public int Port;

        public PeerEndPoint(int playerId, string address, int port)
        {
            PlayerId = playerId;
            Address = address ?? string.Empty;
            Port = port;
        }

        public bool IsValid => !string.IsNullOrEmpty(Address) && Port > 0;
    }

    /// <summary>客户端上报的单帧玩家输入。</summary>
    [Serializable]
    public struct PlayerInputPacket
    {
        public int PlayerId;
        public int FrameIndex;
        public Vector2 MoveDir;
        public int SkillSlot;
        public bool Cast;

        public PlayerInputPacket(int playerId, int frameIndex, Vector2 moveDir, int skillSlot, bool cast)
        {
            PlayerId = playerId;
            FrameIndex = frameIndex;
            MoveDir = moveDir;
            SkillSlot = skillSlot;
            Cast = cast;
        }

        public static PlayerInputPacket Empty(int playerId, int frameIndex)
        {
            return new PlayerInputPacket(playerId, frameIndex, Vector2.zero, -1, false);
        }
    }

    /// <summary>快照中单个实体的最小状态，用于广播与插值渲染。</summary>
    [Serializable]
    public struct EntityState
    {
        public int EntityId;
        public EntityType Type;
        public Vector2 Position;
        public float Health;
        public EntityStateFlags Flags;

        public EntityState(int entityId, EntityType type, Vector2 position, float health, EntityStateFlags flags)
        {
            EntityId = entityId;
            Type = type;
            Position = position;
            Health = health;
            Flags = flags;
        }

        public bool IsAlive => (Flags & EntityStateFlags.Alive) != 0;
    }

    /// <summary>房主在某逻辑帧广播的权威状态快照。</summary>
    [Serializable]
    public struct StateSnapshot
    {
        public int FrameIndex;
        public EntityState[] Entities;
        public int WaveLayer;
        public uint RngState;

        public StateSnapshot(int frameIndex, EntityState[] entities, int waveLayer, uint rngState)
        {
            FrameIndex = frameIndex;
            Entities = entities ?? Array.Empty<EntityState>();
            WaveLayer = waveLayer;
            RngState = rngState;
        }

        /// <summary>在两个快照之间按 [0,1] 因子插值，用于客户端远端实体平滑渲染。</summary>
        public static EntityState[] Interpolate(StateSnapshot from, StateSnapshot to, float t)
        {
            if (from.Entities == null || from.Entities.Length == 0)
            {
                return to.Entities ?? Array.Empty<EntityState>();
            }

            if (to.Entities == null || to.Entities.Length == 0)
            {
                return from.Entities;
            }

            t = Mathf.Clamp01(t);
            var result = new EntityState[to.Entities.Length];
            for (var i = 0; i < to.Entities.Length; i++)
            {
                var target = to.Entities[i];
                var origin = target;
                for (var j = 0; j < from.Entities.Length; j++)
                {
                    if (from.Entities[j].EntityId == target.EntityId)
                    {
                        origin = from.Entities[j];
                        break;
                    }
                }

                target.Position = Vector2.Lerp(origin.Position, target.Position, t);
                result[i] = target;
            }

            return result;
        }
    }
}
