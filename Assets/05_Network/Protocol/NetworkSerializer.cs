using System;
using System.Collections.Generic;
using System.IO;

namespace StarPixelSurge.Network
{
    /// <summary>
    /// 网络载荷序列化。传输层只搬运字节，编码格式在此集中定义，
    /// 便于后续替换为更紧凑的二进制协议。
    /// </summary>
    public static class NetworkSerializer
    {
        /// <summary>序列化玩家输入包，首字节为消息类型。</summary>
        public static byte[] SerializeInput(PlayerInputPacket packet)
        {
            using (var stream = new MemoryStream(24))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((byte)NetworkMessageType.PlayerInput);
                writer.Write(packet.PlayerId);
                writer.Write(packet.FrameIndex);
                writer.Write(packet.MoveDir.x);
                writer.Write(packet.MoveDir.y);
                writer.Write(packet.SkillSlot);
                writer.Write(packet.Cast);
                return stream.ToArray();
            }
        }

        /// <summary>反序列化玩家输入包。返回 false 表示类型不匹配或数据损坏。</summary>
        public static bool TryDeserializeInput(byte[] data, out PlayerInputPacket packet)
        {
            packet = default;
            if (data == null || data.Length < 21 || data[0] != (byte)NetworkMessageType.PlayerInput)
            {
                return false;
            }

            try
            {
                using (var stream = new MemoryStream(data))
                using (var reader = new BinaryReader(stream))
                {
                    reader.ReadByte();
                    packet.PlayerId = reader.ReadInt32();
                    packet.FrameIndex = reader.ReadInt32();
                    packet.MoveDir = new UnityEngine.Vector2(reader.ReadSingle(), reader.ReadSingle());
                    packet.SkillSlot = reader.ReadInt32();
                    packet.Cast = reader.ReadBoolean();
                    return true;
                }
            }
            catch (EndOfStreamException)
            {
                return false;
            }
        }

        /// <summary>序列化状态快照。</summary>
        public static byte[] SerializeSnapshot(StateSnapshot snapshot)
        {
            var entities = snapshot.Entities ?? Array.Empty<EntityState>();
            using (var stream = new MemoryStream(13 + entities.Length * 19))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((byte)NetworkMessageType.StateSnapshot);
                writer.Write(snapshot.FrameIndex);
                writer.Write(snapshot.WaveLayer);
                writer.Write(snapshot.RngState);
                writer.Write(entities.Length);

                for (var i = 0; i < entities.Length; i++)
                {
                    var entity = entities[i];
                    writer.Write(entity.EntityId);
                    writer.Write((byte)entity.Type);
                    writer.Write(entity.Position.x);
                    writer.Write(entity.Position.y);
                    writer.Write(entity.Health);
                    writer.Write((ushort)entity.Flags);
                }

                return stream.ToArray();
            }
        }

        /// <summary>反序列化状态快照。返回 false 表示类型不匹配或数据损坏。</summary>
        public static bool TryDeserializeSnapshot(byte[] data, out StateSnapshot snapshot)
        {
            snapshot = default;
            if (data == null || data.Length < 13 || data[0] != (byte)NetworkMessageType.StateSnapshot)
            {
                return false;
            }

            try
            {
                using (var stream = new MemoryStream(data))
                using (var reader = new BinaryReader(stream))
                {
                    reader.ReadByte();
                    var frameIndex = reader.ReadInt32();
                    var waveLayer = reader.ReadInt32();
                    var rngState = reader.ReadUInt32();
                    var count = reader.ReadInt32();
                    if (count < 0)
                    {
                        return false;
                    }

                    var entities = new EntityState[count];
                    for (var i = 0; i < count; i++)
                    {
                        entities[i] = new EntityState
                        {
                            EntityId = reader.ReadInt32(),
                            Type = (EntityType)reader.ReadByte(),
                            Position = new UnityEngine.Vector2(reader.ReadSingle(), reader.ReadSingle()),
                            Health = reader.ReadSingle(),
                            Flags = (EntityStateFlags)reader.ReadUInt16()
                        };
                    }

                    snapshot = new StateSnapshot(frameIndex, entities, waveLayer, rngState);
                    return true;
                }
            }
            catch (EndOfStreamException)
            {
                return false;
            }
        }

        /// <summary>序列化房主心跳。</summary>
        public static byte[] SerializeHeartbeat(int playerId, int frameIndex)
        {
            using (var stream = new MemoryStream(9))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((byte)NetworkMessageType.HostHeartbeat);
                writer.Write(playerId);
                writer.Write(frameIndex);
                return stream.ToArray();
            }
        }

        /// <summary>反序列化房主心跳。</summary>
        public static bool TryDeserializeHeartbeat(byte[] data, out int playerId, out int frameIndex)
        {
            playerId = 0;
            frameIndex = 0;
            if (data == null || data.Length < 9 || data[0] != (byte)NetworkMessageType.HostHeartbeat)
            {
                return false;
            }

            try
            {
                using (var stream = new MemoryStream(data))
                using (var reader = new BinaryReader(stream))
                {
                    reader.ReadByte();
                    playerId = reader.ReadInt32();
                    frameIndex = reader.ReadInt32();
                    return true;
                }
            }
            catch (EndOfStreamException)
            {
                return false;
            }
        }

        /// <summary>序列化房主迁移公告，携带新房主端点以便其余成员重定向上报目标。</summary>
        public static byte[] SerializeMigration(int newHostId, int frameIndex, string hostAddress = null, int hostPort = 0)
        {
            using (var stream = new MemoryStream(32))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((byte)NetworkMessageType.HostMigration);
                writer.Write(newHostId);
                writer.Write(frameIndex);
                writer.Write(hostAddress ?? string.Empty);
                writer.Write(hostPort);
                return stream.ToArray();
            }
        }

        /// <summary>反序列化房主迁移公告。</summary>
        public static bool TryDeserializeMigration(byte[] data, out int newHostId, out int frameIndex)
        {
            return TryDeserializeMigration(data, out newHostId, out frameIndex, out _, out _);
        }

        /// <summary>反序列化房主迁移公告，并输出新房主端点（旧格式无端点时返回空地址）。</summary>
        public static bool TryDeserializeMigration(byte[] data, out int newHostId, out int frameIndex, out string hostAddress, out int hostPort)
        {
            newHostId = 0;
            frameIndex = 0;
            hostAddress = null;
            hostPort = 0;
            if (data == null || data.Length < 9 || data[0] != (byte)NetworkMessageType.HostMigration)
            {
                return false;
            }

            try
            {
                using (var stream = new MemoryStream(data))
                using (var reader = new BinaryReader(stream))
                {
                    reader.ReadByte();
                    newHostId = reader.ReadInt32();
                    frameIndex = reader.ReadInt32();
                    if (reader.BaseStream.Length - reader.BaseStream.Position >= 1)
                    {
                        hostAddress = reader.ReadString();
                        hostPort = reader.ReadInt32();
                    }

                    return true;
                }
            }
            catch (EndOfStreamException)
            {
                return false;
            }
        }

        /// <summary>序列化对局开始参数（随机种子与职业）。</summary>
        public static byte[] SerializeMatchStart(int seed, string classId)
        {
            using (var stream = new MemoryStream(16))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((byte)NetworkMessageType.MatchStart);
                writer.Write(seed);
                writer.Write(classId ?? string.Empty);
                return stream.ToArray();
            }
        }

        /// <summary>反序列化对局开始参数。</summary>
        public static bool TryDeserializeMatchStart(byte[] data, out int seed, out string classId)
        {
            seed = 0;
            classId = null;
            if (data == null || data.Length < 5 || data[0] != (byte)NetworkMessageType.MatchStart)
            {
                return false;
            }

            try
            {
                using (var stream = new MemoryStream(data))
                using (var reader = new BinaryReader(stream))
                {
                    reader.ReadByte();
                    seed = reader.ReadInt32();
                    classId = reader.ReadString();
                    return true;
                }
            }
            catch (EndOfStreamException)
            {
                return false;
            }
        }

        /// <summary>序列化玩家准备态，携带所选职业以便房主建立对应分身。</summary>
        public static byte[] SerializeReady(int playerId, bool ready, string classId)
        {
            using (var stream = new MemoryStream(32))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((byte)NetworkMessageType.PlayerReady);
                writer.Write(playerId);
                writer.Write(ready);
                writer.Write(classId ?? string.Empty);
                return stream.ToArray();
            }
        }

        /// <summary>反序列化玩家准备态。</summary>
        public static bool TryDeserializeReady(byte[] data, out int playerId, out bool ready, out string classId)
        {
            playerId = 0;
            ready = false;
            classId = null;
            if (data == null || data.Length < 6 || data[0] != (byte)NetworkMessageType.PlayerReady)
            {
                return false;
            }

            try
            {
                using (var stream = new MemoryStream(data))
                using (var reader = new BinaryReader(stream))
                {
                    reader.ReadByte();
                    playerId = reader.ReadInt32();
                    ready = reader.ReadBoolean();
                    classId = data.Length > 6 ? reader.ReadString() : null;
                    return true;
                }
            }
            catch (EndOfStreamException)
            {
                return false;
            }
        }

        /// <summary>序列化房主权威名册（不含端点，供本地回环等无端点传输使用）。</summary>
        public static byte[] SerializeRoster(int hostId, int[] members)
        {
            return SerializeRoster(hostId, members, null);
        }

        /// <summary>
        /// 序列化房主权威名册，并随包下发各成员端点。迁移后的新房主据此直接寻址其余成员，
        /// 无需再依赖已失效的旧房主地址。
        /// </summary>
        public static byte[] SerializeRoster(int hostId, int[] members, PeerEndPoint[] endpoints)
        {
            members ??= System.Array.Empty<int>();
            using (var stream = new MemoryStream(13 + members.Length * 16))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((byte)NetworkMessageType.Roster);
                writer.Write(hostId);
                writer.Write(members.Length);
                for (var i = 0; i < members.Length; i++)
                {
                    writer.Write(members[i]);
                    var endpoint = FindEndPoint(endpoints, members[i]);
                    writer.Write(endpoint.HasValue ? endpoint.Value.Address : string.Empty);
                    writer.Write(endpoint.HasValue ? endpoint.Value.Port : 0);
                }

                return stream.ToArray();
            }
        }

        /// <summary>反序列化房主权威名册。</summary>
        public static bool TryDeserializeRoster(byte[] data, out int hostId, out int[] members)
        {
            return TryDeserializeRoster(data, out hostId, out members, out _);
        }

        /// <summary>反序列化房主权威名册，并输出各成员端点。</summary>
        public static bool TryDeserializeRoster(byte[] data, out int hostId, out int[] members, out PeerEndPoint[] endpoints)
        {
            hostId = -1;
            members = null;
            endpoints = null;
            if (data == null || data.Length < 9 || data[0] != (byte)NetworkMessageType.Roster)
            {
                return false;
            }

            try
            {
                using (var stream = new MemoryStream(data))
                using (var reader = new BinaryReader(stream))
                {
                    reader.ReadByte();
                    hostId = reader.ReadInt32();
                    var count = reader.ReadInt32();
                    if (count < 0)
                    {
                        return false;
                    }

                    members = new int[count];
                    var records = new List<PeerEndPoint>(count);
                    for (var i = 0; i < count; i++)
                    {
                        members[i] = reader.ReadInt32();

                        // 端点字段可能缺省（旧包或本机回环传输），按剩余长度容错读取。
                        if (reader.BaseStream.Length - reader.BaseStream.Position < 1)
                        {
                            continue;
                        }

                        var address = reader.ReadString();
                        var port = reader.ReadInt32();
                        records.Add(new PeerEndPoint(members[i], address, port));
                    }

                    endpoints = records.ToArray();
                    return true;
                }
            }
            catch (EndOfStreamException)
            {
                return false;
            }
        }

        private static PeerEndPoint? FindEndPoint(PeerEndPoint[] endpoints, int playerId)
        {
            if (endpoints == null)
            {
                return null;
            }

            for (var i = 0; i < endpoints.Length; i++)
            {
                if (endpoints[i].PlayerId == playerId)
                {
                    return endpoints[i];
                }
            }

            return null;
        }

        /// <summary>序列化加入房间请求。</summary>
        public static byte[] SerializeJoinRequest(int playerId)
        {
            using (var stream = new MemoryStream(5))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((byte)NetworkMessageType.JoinRequest);
                writer.Write(playerId);
                return stream.ToArray();
            }
        }

        /// <summary>反序列化加入房间请求。</summary>
        public static bool TryDeserializeJoinRequest(byte[] data, out int playerId)
        {
            playerId = -1;
            if (data == null || data.Length < 5 || data[0] != (byte)NetworkMessageType.JoinRequest)
            {
                return false;
            }

            try
            {
                using (var stream = new MemoryStream(data))
                using (var reader = new BinaryReader(stream))
                {
                    reader.ReadByte();
                    playerId = reader.ReadInt32();
                    return true;
                }
            }
            catch (EndOfStreamException)
            {
                return false;
            }
        }
    }
}
