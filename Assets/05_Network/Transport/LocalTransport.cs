using System;
using System.Collections.Generic;
using UnityEngine;

namespace StarPixelSurge.Network
{
    /// <summary>
    /// 进程内回环传输中枢。为本地多实例联机测试与无真实网络环境提供支撑，
    /// 支持注入固定延迟、抖动与丢包率以验证快照插值与房主迁移逻辑。
    /// </summary>
    public static class LocalTransportHub
    {
        private class PendingMessage
        {
            public LocalTransport Target;
            public int SenderId;
            public byte[] Payload;
            public float DueAt;
        }

        private static readonly Dictionary<int, LocalTransport> Peers = new Dictionary<int, LocalTransport>();
        private static readonly List<PendingMessage> Pending = new List<PendingMessage>();
        private static float _clock;
        private static int _lastAdvancedFrame = -1;

        /// <summary>基础单向延迟（秒）。</summary>
        public static float LatencySeconds = 0.05f;

        /// <summary>延迟抖动幅度（秒），在 [0, Jitter] 内随机叠加。</summary>
        public static float JitterSeconds;

        /// <summary>丢包概率 [0,1]，用于验证插值与过期快照丢弃。</summary>
        public static float LossRate;

        internal static void Register(LocalTransport transport)
        {
            if (transport != null)
            {
                Peers[transport.LocalPlayerId] = transport;
            }
        }

        internal static void Unregister(int playerId)
        {
            Peers.Remove(playerId);
        }

        internal static bool TryGetPeer(int playerId, out LocalTransport transport)
        {
            return Peers.TryGetValue(playerId, out transport);
        }

        internal static List<int> PeerIdsExcept(int playerId)
        {
            var result = new List<int>(Peers.Count);
            foreach (var id in Peers.Keys)
            {
                if (id != playerId)
                {
                    result.Add(id);
                }
            }

            return result;
        }

        internal static void Enqueue(LocalTransport target, int senderId, byte[] payload)
        {
            if (target == null || payload == null)
            {
                return;
            }

            if (LossRate > 0f && UnityEngine.Random.value < LossRate)
            {
                return;
            }

            var jitter = JitterSeconds > 0f ? UnityEngine.Random.Range(0f, JitterSeconds) : 0f;
            Pending.Add(new PendingMessage
            {
                Target = target,
                SenderId = senderId,
                Payload = payload,
                DueAt = _clock + Mathf.Max(0f, LatencySeconds) + jitter
            });
        }

        internal static void AdvanceOnce(float deltaTime)
        {
            if (Time.frameCount == _lastAdvancedFrame)
            {
                return;
            }

            _lastAdvancedFrame = Time.frameCount;
            _clock += Mathf.Max(0f, deltaTime);

            for (var i = Pending.Count - 1; i >= 0; i--)
            {
                var message = Pending[i];
                if (message.DueAt > _clock)
                {
                    continue;
                }

                Pending.RemoveAt(i);
                if (message.Target != null && message.Target.IsRunning)
                {
                    message.Target.Deliver(message.SenderId, message.Payload);
                }
            }
        }

        /// <summary>重置中枢，便于测试之间隔离状态。</summary>
        public static void Reset()
        {
            Peers.Clear();
            Pending.Clear();
            _clock = 0f;
            _lastAdvancedFrame = -1;
            LatencySeconds = 0.05f;
            JitterSeconds = 0f;
            LossRate = 0f;
        }
    }

    /// <summary>
    /// 本地回环传输实现。多实例通过 <see cref="LocalTransportHub"/> 互联，
    /// 行为语义与真实传输一致，便于在单进程内验证联机逻辑。
    /// </summary>
    public class LocalTransport : ITransport
    {
        private bool _running;

        public LocalTransport(int localPlayerId = -1)
        {
            LocalPlayerId = localPlayerId;
        }

        public int LocalPlayerId { get; private set; }

        public bool IsRunning => _running;

        public int HostId { get; set; } = -1;

        public event Action<int, byte[]> MessageReceived;

        public void Start(int localPlayerId)
        {
            if (_running)
            {
                Stop();
            }

            LocalPlayerId = localPlayerId;
            _running = true;
            LocalTransportHub.Register(this);
        }

        public void Stop()
        {
            if (!_running)
            {
                return;
            }

            _running = false;
            LocalTransportHub.Unregister(LocalPlayerId);
        }

        public void SendToHost(byte[] payload)
        {
            if (!_running || HostId < 0 || HostId == LocalPlayerId)
            {
                return;
            }

            if (LocalTransportHub.TryGetPeer(HostId, out var host))
            {
                LocalTransportHub.Enqueue(host, LocalPlayerId, payload);
            }
        }

        public void Broadcast(byte[] payload)
        {
            if (!_running)
            {
                return;
            }

            var peers = LocalTransportHub.PeerIdsExcept(LocalPlayerId);
            for (var i = 0; i < peers.Count; i++)
            {
                if (LocalTransportHub.TryGetPeer(peers[i], out var peer))
                {
                    LocalTransportHub.Enqueue(peer, LocalPlayerId, payload);
                }
            }
        }

        public void Pump(float deltaTime)
        {
            LocalTransportHub.AdvanceOnce(deltaTime);
        }

        internal void Deliver(int senderId, byte[] payload)
        {
            MessageReceived?.Invoke(senderId, payload);
        }
    }
}
