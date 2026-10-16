using System;
using System.Collections.Generic;
using UnityEngine;

namespace StarPixelSurge.Network.Simulation
{
    /// <summary>
    /// 确定性进程内消息总线。以可注入的单向延迟、抖动与丢包率转发消息，
    /// 语义与真实传输一致，用于在单进程内验证联机协议与状态收敛。
    /// </summary>
    public sealed class SimNetwork
    {
        private sealed class PendingMessage
        {
            public SimTransport Target;
            public int SenderId;
            public byte[] Payload;
            public float DueAt;
        }

        private readonly Dictionary<int, SimTransport> _peers = new Dictionary<int, SimTransport>();
        private readonly List<PendingMessage> _pending = new List<PendingMessage>();
        private readonly List<int> _othersScratch = new List<int>();

        private SimRng _rng;
        private float _clock;

        /// <summary>基础单向延迟（秒）。</summary>
        public float Latency = 0.05f;

        /// <summary>延迟抖动幅度（秒）。</summary>
        public float Jitter;

        /// <summary>丢包概率 [0,1]。</summary>
        public float LossRate;

        /// <summary>被丢弃的消息计数，便于观察注入的丢包。</summary>
        public int DroppedCount { get; private set; }

        public SimNetwork(ulong seed)
        {
            _rng = new SimRng(seed);
        }

        public float Clock => _clock;

        public int PeerCount => _peers.Count;

        internal void Register(SimTransport transport)
        {
            if (transport != null)
            {
                _peers[transport.LocalPlayerId] = transport;
            }
        }

        internal void Unregister(int playerId)
        {
            _peers.Remove(playerId);
        }

        internal bool TryGetPeer(int playerId, out SimTransport transport)
        {
            return _peers.TryGetValue(playerId, out transport);
        }

        internal IReadOnlyList<int> OthersExcept(int playerId)
        {
            _othersScratch.Clear();
            foreach (var id in _peers.Keys)
            {
                if (id != playerId)
                {
                    _othersScratch.Add(id);
                }
            }

            // 固定广播顺序，确保随机抖动/丢包的抽取顺序可复现。
            _othersScratch.Sort();
            return _othersScratch;
        }

        internal void Enqueue(SimTransport target, int senderId, byte[] payload)
        {
            if (target == null || payload == null)
            {
                return;
            }

            if (LossRate > 0f && _rng.NextFloat() < LossRate)
            {
                DroppedCount++;
                return;
            }

            var jitter = Jitter > 0f ? _rng.Range(0f, Jitter) : 0f;
            _pending.Add(new PendingMessage
            {
                Target = target,
                SenderId = senderId,
                Payload = payload,
                DueAt = _clock + Mathf.Max(0f, Latency) + jitter
            });
        }

        /// <summary>推进虚拟时钟并投递到期消息。由仿真编排器统一调用。</summary>
        public void Step(float deltaTime)
        {
            _clock += Mathf.Max(0f, deltaTime);

            for (var i = _pending.Count - 1; i >= 0; i--)
            {
                var message = _pending[i];
                if (message.DueAt > _clock)
                {
                    continue;
                }

                _pending.RemoveAt(i);
                if (message.Target != null && message.Target.IsRunning)
                {
                    message.Target.Deliver(message.SenderId, message.Payload);
                }
            }
        }
    }

    /// <summary>
    /// 仿真传输端点。接入 <see cref="SimNetwork"/> 后可按真实传输语义收发消息，
    /// 但由仿真统一推进时钟，从而获得完全确定性的执行顺序。
    /// </summary>
    public sealed class SimTransport : ITransport
    {
        private readonly SimNetwork _network;

        public SimTransport(SimNetwork network, int localPlayerId = -1)
        {
            _network = network;
            LocalPlayerId = localPlayerId;
        }

        public int LocalPlayerId { get; private set; }

        public bool IsRunning { get; private set; }

        public int HostId { get; set; } = -1;

        public event Action<int, byte[]> MessageReceived;

        public void Start(int localPlayerId)
        {
            if (IsRunning)
            {
                Stop();
            }

            LocalPlayerId = localPlayerId;
            IsRunning = true;
            _network.Register(this);
        }

        public void Stop()
        {
            if (!IsRunning)
            {
                return;
            }

            IsRunning = false;
            _network.Unregister(LocalPlayerId);
        }

        public void SendToHost(byte[] payload)
        {
            if (!IsRunning || HostId < 0 || HostId == LocalPlayerId)
            {
                return;
            }

            if (_network.TryGetPeer(HostId, out var host))
            {
                _network.Enqueue(host, LocalPlayerId, payload);
            }
        }

        public void Broadcast(byte[] payload)
        {
            if (!IsRunning)
            {
                return;
            }

            var peers = _network.OthersExcept(LocalPlayerId);
            for (var i = 0; i < peers.Count; i++)
            {
                if (_network.TryGetPeer(peers[i], out var peer))
                {
                    _network.Enqueue(peer, LocalPlayerId, payload);
                }
            }
        }

        /// <summary>时钟由仿真统一推进，端点自身不推进。</summary>
        public void Pump(float deltaTime)
        {
        }

        internal void Deliver(int senderId, byte[] payload)
        {
            MessageReceived?.Invoke(senderId, payload);
        }
    }
}
