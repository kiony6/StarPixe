using System;
using System.Collections.Generic;

namespace StarPixelSurge.Network
{
    /// <summary>
    /// 房主迁移服务。跟踪成员心跳，检测房主失效，在选举窗口内按加入顺序
    /// 选取可达成员为新任房主，并以最后确认快照恢复对局（不重开）。
    /// </summary>
    public class HostMigrationService
    {
        private ITransport _transport;
        private RoomService _room;
        private FrameSyncService _frame;

        private readonly Dictionary<int, float> _lastSeen = new Dictionary<int, float>();
        private float _clock;
        private float _heartbeatTimer;
        private float _hostSilence;
        private float _electionTimer;
        private bool _electing;

        /// <summary>心跳广播间隔（秒）。</summary>
        public float HeartbeatInterval { get; set; } = 0.5f;

        /// <summary>判定房主失效的静默阈值（秒）。</summary>
        public float HeartbeatTimeout { get; set; } = 2f;

        /// <summary>选举收敛窗口（秒）。</summary>
        public float ElectionWindow { get; set; } = 1.5f;

        public int CurrentHostId { get; private set; } = -1;

        public bool IsElecting => _electing;

        public bool HasConfirmedSnapshot { get; private set; }

        public StateSnapshot LastConfirmedSnapshot { get; private set; }

        public int RestoreFrame { get; private set; } = -1;

        /// <summary>产生新房主时触发，携带新房主 ID 与恢复帧序号。</summary>
        public event Action<int, int> HostElected;

        public void Configure(ITransport transport, RoomService room, FrameSyncService frame, int hostId)
        {
            _transport = transport;
            _room = room;
            _frame = frame;
            CurrentHostId = hostId;
            _clock = 0f;
            _heartbeatTimer = 0f;
            _hostSilence = 0f;
            _electionTimer = 0f;
            _electing = false;
            HasConfirmedSnapshot = false;
            RestoreFrame = -1;
            _lastSeen.Clear();
        }

        public void Shutdown()
        {
            _transport = null;
            _room = null;
            _frame = null;
            _lastSeen.Clear();
            _electing = false;
        }

        public void SetHost(int hostId)
        {
            CurrentHostId = hostId;
            _hostSilence = 0f;
        }

        public void MarkPeerSeen(int playerId)
        {
            _lastSeen[playerId] = _clock;
        }

        /// <summary>缓存最后确认快照，供迁移恢复使用。</summary>
        public void CacheSnapshot(StateSnapshot snapshot)
        {
            if (HasConfirmedSnapshot && snapshot.FrameIndex < LastConfirmedSnapshot.FrameIndex)
            {
                return;
            }

            LastConfirmedSnapshot = snapshot;
            HasConfirmedSnapshot = true;
        }

        public void Tick(float deltaTime)
        {
            if (_transport == null || !_transport.IsRunning)
            {
                return;
            }

            _clock += deltaTime;
            _heartbeatTimer += deltaTime;

            if (_heartbeatTimer >= HeartbeatInterval)
            {
                _heartbeatTimer = 0f;
                var frame = _frame != null ? _frame.CurrentFrameIndex : 0;
                _transport.Broadcast(NetworkSerializer.SerializeHeartbeat(_transport.LocalPlayerId, frame));
            }

            MarkPeerSeen(_transport.LocalPlayerId);

            if (_electing)
            {
                _electionTimer += deltaTime;
                if (_electionTimer >= ElectionWindow)
                {
                    ConcludeElection();
                }

                return;
            }

            if (CurrentHostId >= 0 && CurrentHostId != _transport.LocalPlayerId)
            {
                if (IsReachable(CurrentHostId))
                {
                    _hostSilence = 0f;
                }
                else
                {
                    _hostSilence += deltaTime;
                    if (_hostSilence >= HeartbeatTimeout)
                    {
                        OnHostTimeout();
                    }
                }
            }
        }

        public void HandleHeartbeat(byte[] payload)
        {
            if (!NetworkSerializer.TryDeserializeHeartbeat(payload, out var playerId, out _))
            {
                return;
            }

            MarkPeerSeen(playerId);
            if (playerId == CurrentHostId)
            {
                _hostSilence = 0f;
            }
        }

        public void HandleMigration(byte[] payload)
        {
            if (!NetworkSerializer.TryDeserializeMigration(payload, out var newHostId, out var frameIndex))
            {
                return;
            }

            CurrentHostId = newHostId;
            RestoreFrame = frameIndex;
            _electing = false;
            _hostSilence = 0f;

            if (_transport != null)
            {
                _transport.HostId = newHostId;
            }

            HostElected?.Invoke(newHostId, frameIndex);
        }

        /// <summary>房主心跳超时，开启选举窗口。</summary>
        public void OnHostTimeout()
        {
            if (_electing)
            {
                return;
            }

            _electing = true;
            _electionTimer = 0f;
        }

        /// <summary>按加入顺序选取可达成员为新房主。无候选返回 -1。</summary>
        public int ElectNewHost()
        {
            if (_room == null)
            {
                return -1;
            }

            var ordered = _room.MembersByJoinOrder();
            var fallback = -1;

            for (var i = 0; i < ordered.Count; i++)
            {
                var candidate = ordered[i];
                if (candidate == CurrentHostId)
                {
                    continue;
                }

                if (fallback < 0)
                {
                    fallback = candidate;
                }

                if (IsReachable(candidate))
                {
                    return candidate;
                }
            }

            return fallback;
        }

        /// <summary>以最后确认快照恢复本地模拟状态，返回是否恢复成功。</summary>
        public bool RestoreFromSnapshot()
        {
            if (!HasConfirmedSnapshot)
            {
                return false;
            }

            RestoreFrame = LastConfirmedSnapshot.FrameIndex;
            return true;
        }

        private void ConcludeElection()
        {
            _electing = false;
            var newHost = ElectNewHost();
            if (newHost < 0 || newHost == CurrentHostId)
            {
                return;
            }

            CurrentHostId = newHost;
            if (_transport != null)
            {
                _transport.HostId = newHost;
            }

            if (newHost == _transport?.LocalPlayerId)
            {
                RestoreFromSnapshot();
            }

            HostElected?.Invoke(newHost, RestoreFrame);
        }

        private bool IsReachable(int playerId)
        {
            if (_transport != null && playerId == _transport.LocalPlayerId)
            {
                return true;
            }

            if (!_lastSeen.TryGetValue(playerId, out var last))
            {
                return false;
            }

            return _clock - last <= HeartbeatTimeout;
        }
    }
}
