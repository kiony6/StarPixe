using System;
using System.Collections.Generic;
using StarPixelSurge.Framework;
using StarPixelSurge.GameCore;
using UnityEngine;

namespace StarPixelSurge.Network
{
    public enum RoomState
    {
        Idle = 0,
        Lobby = 1,
        Playing = 2
    }

    /// <summary>
    /// 联机管理器。组装房间名册、帧同步、房主迁移与实体同步：
    /// 房主权威推进模拟并广播快照，客户端上报输入并按插值渲染远端实体。
    /// 传输层通过 <see cref="TransportFactory"/> 可替换（默认进程内回环，后续接入 UDP 插件）。
    /// </summary>
    public class NetManager : MonoSingleton<NetManager>
    {
        private readonly RoomService _room = new RoomService();
        private readonly HostMigrationService _migration = new HostMigrationService();
        private readonly List<int> _playersCache = new List<int>();

        private ITransport _transport;

        public RoomState State { get; private set; } = RoomState.Idle;

        public bool IsHost { get; private set; }

        public string RoomId { get; private set; }

        public int LocalPlayerId { get; private set; } = -1;

        /// <summary>本机所选职业 ID，开局时随对局参数下发。</summary>
        public string LastClassId { get; private set; }

        public IReadOnlyList<int> Players => _playersCache;

        public RoomService Room => _room;

        public HostMigrationService Migration => _migration;

        public FrameSyncService Frame => FrameSyncService.HasInstance ? FrameSyncService.Instance : null;

        /// <summary>传输层工厂，供测试或替换 UDP 实现。默认为进程内回环传输。</summary>
        public Func<ITransport> TransportFactory { get; set; }

        public event Action RoomUpdated;

        protected override void OnSingletonAwake()
        {
            _room.RosterChanged += OnRosterChanged;
        }

        protected override void OnDestroy()
        {
            Shutdown();
            _room.RosterChanged -= OnRosterChanged;
            base.OnDestroy();
        }

        private void Update()
        {
            if (_transport == null)
            {
                return;
            }

            _transport.Pump(Time.deltaTime);
            _migration.Tick(Time.deltaTime);
        }

        public void CreateRoom()
        {
            TearDownTransport();

            RoomId = Guid.NewGuid().ToString("N").Substring(0, 8);
            IsHost = true;
            LocalPlayerId = 0;
            State = RoomState.Lobby;

            _room.Reset();
            _room.Join(LocalPlayerId);
            _room.SetHost(LocalPlayerId);

            SetupTransport();
            FrameSyncService.Instance.Configure(_transport, NetworkRole.Host);
            FrameSyncService.Instance.RngState = (uint)UnityEngine.Random.Range(1, int.MaxValue);
            NetworkEntitySync.Instance.BindAsProvider(
                FrameSyncService.Instance,
                () => GameManager.HasInstance ? GameManager.Instance.WaveLayer : 0,
                () => FrameSyncService.Instance.RngState);

            _migration.Configure(_transport, _room, FrameSyncService.Instance, LocalPlayerId);
            _migration.HostElected += OnHostElected;

            OnRosterChanged();
        }

        public bool JoinRoom(string roomId, int hostId = 0)
        {
            if (string.IsNullOrEmpty(roomId))
            {
                return false;
            }

            TearDownTransport();

            RoomId = roomId;
            IsHost = false;
            State = RoomState.Lobby;

            _room.Reset();
            LocalPlayerId = hostId == 1 ? 2 : 1;

            _room.Join(hostId);
            _room.SetHost(hostId);
            _room.Join(LocalPlayerId);

            SetupTransport();
            _transport.HostId = hostId;
            FrameSyncService.Instance.Configure(_transport, NetworkRole.Client);

            _migration.Configure(_transport, _room, FrameSyncService.Instance, hostId);
            _migration.HostElected += OnHostElected;

            _transport.SendToHost(NetworkSerializer.SerializeJoinRequest(LocalPlayerId));

            OnRosterChanged();
            return true;
        }

        public void LeaveRoom()
        {
            TearDownTransport();
            _room.Reset();
            RoomId = null;
            IsHost = false;
            LocalPlayerId = -1;
            State = RoomState.Idle;
            OnRosterChanged();
        }

        public void SetReady(bool ready)
        {
            if (LocalPlayerId < 0)
            {
                return;
            }

            _room.SetReady(LocalPlayerId, ready);
            _transport?.Broadcast(NetworkSerializer.SerializeReady(LocalPlayerId, ready));
        }

        /// <summary>选择本机职业，开局时随对局参数下发到各客户端。</summary>
        public void SelectClass(string classId)
        {
            LastClassId = classId;
        }

        public bool CanStartMatch()
        {
            return State == RoomState.Lobby && _room.CanStart(LocalPlayerId);
        }

        public void StartMatch()
        {
            if (!CanStartMatch())
            {
                return;
            }

            State = RoomState.Playing;
            _room.LockRoster();
            OnRosterChanged();

            var seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            _transport.Broadcast(NetworkSerializer.SerializeMatchStart(seed, LastClassId));

            BeginLocalMatch(seed, true, LastClassId);
        }

        /// <summary>在本地按同一随机种子构建对局。拥有 GameBootstrap 时由其接管地图与实体装配。</summary>
        private void BeginLocalMatch(int seed, bool isHost, string classId)
        {
            if (GameBootstrap.Instance != null)
            {
                GameBootstrap.Instance.StartMultiplayer(seed, isHost, classId);
                return;
            }

            if (GameManager.HasInstance)
            {
                GameManager.Instance.StartMatch(new MatchConfig(MatchMode.Multiplayer, classId, seed));
            }
        }

        public void Shutdown()
        {
            TearDownTransport();
            _room.Reset();
            State = RoomState.Idle;
            IsHost = false;
            LocalPlayerId = -1;
            RoomId = null;
        }

        private void SetupTransport()
        {
            var factory = TransportFactory ?? (() => new LocalTransport());
            _transport = factory();
            _transport.MessageReceived += OnMessage;
            _transport.Start(LocalPlayerId);
            _transport.HostId = IsHost ? LocalPlayerId : _transport.HostId;
        }

        private void TearDownTransport()
        {
            if (_transport == null)
            {
                return;
            }

            _transport.MessageReceived -= OnMessage;
            _transport.Stop();
            _transport = null;

            if (FrameSyncService.HasInstance)
            {
                FrameSyncService.Instance.Shutdown();
            }

            _migration.HostElected -= OnHostElected;
            _migration.Shutdown();
        }

        private void OnMessage(int senderId, byte[] payload)
        {
            if (payload == null || payload.Length == 0)
            {
                return;
            }

            _migration.MarkPeerSeen(senderId);

            switch ((NetworkMessageType)payload[0])
            {
                case NetworkMessageType.PlayerInput:
                    FrameSyncService.Instance.OnInputReceived(payload);
                    break;
                case NetworkMessageType.StateSnapshot:
                    if (FrameSyncService.Instance.Role == NetworkRole.Client
                        && NetworkSerializer.TryDeserializeSnapshot(payload, out var snapshot))
                    {
                        FrameSyncService.Instance.ApplySnapshot(snapshot);
                        _migration.CacheSnapshot(snapshot);
                    }

                    break;
                case NetworkMessageType.HostHeartbeat:
                    _migration.HandleHeartbeat(payload);
                    break;
                case NetworkMessageType.HostMigration:
                    _migration.HandleMigration(payload);
                    break;
                case NetworkMessageType.MatchStart:
                    OnMatchStart(payload);
                    break;
                case NetworkMessageType.PlayerReady:
                    if (NetworkSerializer.TryDeserializeReady(payload, out var readyPlayer, out var readyValue))
                    {
                        _room.SetReady(readyPlayer, readyValue);
                    }

                    break;
                case NetworkMessageType.Roster:
                    if (NetworkSerializer.TryDeserializeRoster(payload, out var rosterHost, out var rosterMembers))
                    {
                        _room.SyncMembers(rosterHost, rosterMembers);
                        _migration.SetHost(rosterHost);
                        if (rosterHost != LocalPlayerId)
                        {
                            _transport.HostId = rosterHost;
                        }
                    }

                    break;
                case NetworkMessageType.JoinRequest:
                    if (IsHost && NetworkSerializer.TryDeserializeJoinRequest(payload, out var joinPlayer))
                    {
                        _room.Join(joinPlayer);
                    }

                    break;
            }
        }

        private void OnMatchStart(byte[] payload)
        {
            if (IsHost || State == RoomState.Playing)
            {
                return;
            }

            if (!NetworkSerializer.TryDeserializeMatchStart(payload, out var seed, out var classId))
            {
                return;
            }

            State = RoomState.Playing;
            _room.LockRoster();
            OnRosterChanged();

            BeginLocalMatch(seed, false, classId);
        }

        private void OnHostElected(int newHostId, int frameIndex)
        {
            var frame = FrameSyncService.Instance;
            if (newHostId == LocalPlayerId)
            {
                IsHost = true;
                _room.SetHost(LocalPlayerId);
                _transport.HostId = LocalPlayerId;

                frame.Configure(_transport, NetworkRole.Host, Mathf.Max(0, frameIndex));
                NetworkEntitySync.Instance.BindAsProvider(
                    frame,
                    () => GameManager.HasInstance ? GameManager.Instance.WaveLayer : 0,
                    () => frame.RngState);

                // 作为新任房主向其余成员公告迁移结果，使其收敛到同一时间轴。
                _transport.Broadcast(NetworkSerializer.SerializeMigration(LocalPlayerId, frame.CurrentFrameIndex));
            }
            else
            {
                IsHost = false;
                _transport.HostId = newHostId;
                frame.Configure(_transport, NetworkRole.Client, Mathf.Max(0, frameIndex));

                if (_migration.HasConfirmedSnapshot)
                {
                    frame.ApplySnapshot(_migration.LastConfirmedSnapshot);
                }
            }

            OnRosterChanged();
        }

        private void OnRosterChanged()
        {
            _playersCache.Clear();
            for (var i = 0; i < _room.Members.Count; i++)
            {
                _playersCache.Add(_room.Members[i].PlayerId);
            }

            RoomUpdated?.Invoke();

            // 房主对外发布权威名册，客户端据此对齐成员集合。
            if (IsHost && _transport != null && State != RoomState.Idle)
            {
                var ids = new int[_room.Members.Count];
                for (var i = 0; i < ids.Length; i++)
                {
                    ids[i] = _room.Members[i].PlayerId;
                }

                _transport.Broadcast(NetworkSerializer.SerializeRoster(LocalPlayerId, ids));
            }
        }
    }
}
