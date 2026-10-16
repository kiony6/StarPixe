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
    /// 传输层默认使用进程内回环（<see cref="LocalTransport"/>）；将 <see cref="UseUdp"/> 置为 true
    /// 后改用真实 UDP（<see cref="UdpTransport"/>），或通过 <see cref="TransportFactory"/> 注入自定义实现。
    /// </summary>
    public class NetManager : MonoSingleton<NetManager>
    {
        private const float LobbyResendInterval = 1f;
        private const float MatchStartResendInterval = 0.5f;
        private const float MatchStartResendWindow = 3f;

        private readonly RoomService _room = new RoomService();
        private readonly HostMigrationService _migration = new HostMigrationService();
        private readonly List<int> _playersCache = new List<int>();
        private readonly Dictionary<int, string> _classByPlayer = new Dictionary<int, string>();
        private readonly Dictionary<int, PeerEndPoint> _peerEndPoints = new Dictionary<int, PeerEndPoint>();

        private ITransport _transport;
        private IPeerDirectory _directory;
        private float _lobbyResendTimer;
        private float _matchStartResendTimer;
        private float _matchStartResendElapsed;
        private int _matchSeed;
        private bool _resendingMatchStart;

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

        /// <summary>传输层工厂，供测试或替换实现。默认为进程内回环传输。</summary>
        public Func<ITransport> TransportFactory { get; set; }

        /// <summary>是否使用真实 UDP 传输。为 false 时使用进程内回环，便于本地联调。</summary>
        public bool UseUdp { get; set; }

        /// <summary>房主绑定的 UDP 端口。</summary>
        public int HostPort { get; set; } = 7777;

        /// <summary>客户端连接的目标地址。</summary>
        public string RemoteHost { get; set; } = "127.0.0.1";

        /// <summary>客户端连接的目标端口。</summary>
        public int RemotePort { get; set; } = 7777;

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
            TickLobbyResilience(Time.deltaTime);
        }

        /// <summary>
        /// 真实 UDP 下控制类消息可能丢失。客户端在大厅内周期重发加入请求与准备态，
        /// 房主在开局后短时间内重复广播开局消息，保证双方最终收敛。
        /// </summary>
        private void TickLobbyResilience(float deltaTime)
        {
            if (!IsHost && State == RoomState.Lobby && LocalPlayerId >= 0)
            {
                _lobbyResendTimer += deltaTime;
                if (_lobbyResendTimer >= LobbyResendInterval)
                {
                    _lobbyResendTimer = 0f;
                    _transport.SendToHost(NetworkSerializer.SerializeJoinRequest(LocalPlayerId));
                    var member = _room.Get(LocalPlayerId);
                    SetReady(member != null && member.Ready);
                }
            }

            if (_resendingMatchStart)
            {
                _matchStartResendTimer += deltaTime;
                _matchStartResendElapsed += deltaTime;
                if (_matchStartResendElapsed >= MatchStartResendWindow)
                {
                    _resendingMatchStart = false;
                }
                else if (_matchStartResendTimer >= MatchStartResendInterval)
                {
                    _matchStartResendTimer = 0f;
                    _transport.Broadcast(NetworkSerializer.SerializeMatchStart(_matchSeed, LastClassId));
                }
            }
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
            _classByPlayer.Clear();
            RoomId = null;
            IsHost = false;
            LocalPlayerId = -1;
            State = RoomState.Idle;
            ResetNetworkPlayers();
            OnRosterChanged();
        }

        public void SetReady(bool ready)
        {
            if (LocalPlayerId < 0)
            {
                return;
            }

            _room.SetReady(LocalPlayerId, ready);
            _transport?.Broadcast(NetworkSerializer.SerializeReady(LocalPlayerId, ready, GetClassId(LocalPlayerId)));
        }

        /// <summary>选择本机职业，开局时随对局参数下发到各客户端。</summary>
        public void SelectClass(string classId)
        {
            LastClassId = classId;
            if (LocalPlayerId >= 0)
            {
                _classByPlayer[LocalPlayerId] = classId;
            }
        }

        /// <summary>取指定玩家所选职业 ID，供房主建立对应分身。</summary>
        public string GetClassId(int playerId)
        {
            return _classByPlayer.TryGetValue(playerId, out var classId) ? classId : null;
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
            _matchSeed = seed;
            _resendingMatchStart = true;
            _matchStartResendTimer = 0f;
            _matchStartResendElapsed = 0f;
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
            _classByPlayer.Clear();
            State = RoomState.Idle;
            IsHost = false;
            LocalPlayerId = -1;
            RoomId = null;
            ResetNetworkPlayers();
        }

        private void ResetNetworkPlayers()
        {
            if (NetworkPlayerRegistry.HasInstance)
            {
                NetworkPlayerRegistry.Instance.Clear();
            }
        }

        private void SetupTransport()
        {
            _transport = CreateTransport();
            _transport.MessageReceived += OnMessage;
            _transport.Start(LocalPlayerId);
            _transport.HostId = IsHost ? LocalPlayerId : _transport.HostId;

            _directory = _transport as IPeerDirectory;
            if (_directory != null)
            {
                _directory.PeerObserved += OnPeerObserved;
            }
        }

        /// <summary>观测到成员来源端点：刷新房主地址，跨过房主迁移。</summary>
        private void OnPeerObserved(int playerId, string address, int port)
        {
            if (string.IsNullOrEmpty(address) || port <= 0)
            {
                return;
            }

            _peerEndPoints[playerId] = new PeerEndPoint(playerId, address, port);

            // 当前房主换了地址（迁移或 NAT 重绑定）时立即重定向上报目标。
            if (_directory != null && !IsHost && playerId == _migration.CurrentHostId)
            {
                _directory.TrySetHost(address, port);
            }
        }

        /// <summary>取某成员已知端点，优先使用名册下发的端点，回退到观测值。</summary>
        private bool TryGetPeerEndPoint(int playerId, out PeerEndPoint endpoint)
        {
            return _peerEndPoints.TryGetValue(playerId, out endpoint) && endpoint.IsValid;
        }

        private ITransport CreateTransport()
        {
            if (UseUdp)
            {
                var udp = new UdpTransport();
                if (IsHost)
                {
                    udp.SetLocalPort(HostPort);
                    udp.HostId = LocalPlayerId;
                }
                else
                {
                    udp.SetHostEndPoint(RemoteHost, RemotePort);
                }

                return udp;
            }

            var factory = TransportFactory ?? (() => new LocalTransport());
            return factory();
        }

        private void TearDownTransport()
        {
            _lobbyResendTimer = 0f;
            _resendingMatchStart = false;

            if (_directory != null)
            {
                _directory.PeerObserved -= OnPeerObserved;
                _directory = null;
            }

            _peerEndPoints.Clear();

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
                    if (NetworkSerializer.TryDeserializeReady(payload, out var readyPlayer, out var readyValue, out var readyClass))
                    {
                        _room.SetReady(readyPlayer, readyValue);
                        if (!string.IsNullOrEmpty(readyClass))
                        {
                            _classByPlayer[readyPlayer] = readyClass;
                        }
                    }

                    break;
                case NetworkMessageType.Roster:
                    if (NetworkSerializer.TryDeserializeRoster(payload, out var rosterHost, out var rosterMembers, out var rosterEndPoints))
                    {
                        _room.SyncMembers(rosterHost, rosterMembers);
                        _migration.SetHost(rosterHost);
                        ApplyRosterEndPoints(rosterEndPoints);
                        if (rosterHost != LocalPlayerId)
                        {
                            _transport.HostId = rosterHost;
                            if (TryGetPeerEndPoint(rosterHost, out var hostEndPoint))
                            {
                                _directory?.TrySetHost(hostEndPoint.Address, hostEndPoint.Port);
                            }
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

        /// <summary>缓存名册下发的成员端点，并让新人成为房主后可据此寻址其余成员。</summary>
        private void ApplyRosterEndPoints(PeerEndPoint[] rosterEndPoints)
        {
            if (rosterEndPoints == null)
            {
                return;
            }

            for (var i = 0; i < rosterEndPoints.Length; i++)
            {
                var endpoint = rosterEndPoints[i];
                if (endpoint.IsValid)
                {
                    _peerEndPoints[endpoint.PlayerId] = endpoint;
                }
            }
        }

        /// <summary>把已知成员端点灌入传输层寻址表，使新房主可立即广播给其余成员。</summary>
        private void RegisterKnownPeers()
        {
            if (_directory == null)
            {
                return;
            }

            foreach (var pair in _peerEndPoints)
            {
                if (pair.Key != LocalPlayerId && pair.Value.IsValid)
                {
                    _directory.RegisterPeer(pair.Key, pair.Value.Address, pair.Value.Port);
                }
            }
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

                // 关键修复：新房主原为客户端，寻址表为空。先把名册下发的成员端点灌入传输层，
                // 否则迁移公告无法抵达其余成员，整局会在此断裂。
                RegisterKnownPeers();

                // 作为新任房主向其余成员公告迁移结果，携带自身端点帮助对方重定向上报目标。
                var advertised = TryGetPeerEndPoint(LocalPlayerId, out var own) ? own : default;
                _transport.Broadcast(NetworkSerializer.SerializeMigration(
                    LocalPlayerId, frame.CurrentFrameIndex, advertised.Address, advertised.Port));
            }
            else
            {
                IsHost = false;
                _transport.HostId = newHostId;
                frame.Configure(_transport, NetworkRole.Client, Mathf.Max(0, frameIndex));

                // 把上报目标切到新房主，使本地输入能继续送达。
                if (TryGetPeerEndPoint(newHostId, out var newHostEndPoint))
                {
                    _directory?.TrySetHost(newHostEndPoint.Address, newHostEndPoint.Port);
                }

                if (_migration.HasConfirmedSnapshot)
                {
                    frame.ApplySnapshot(_migration.LastConfirmedSnapshot);
                }
            }

            if (GameBootstrap.Instance != null)
            {
                GameBootstrap.Instance.RefreshNetworkAuthority();
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

            // 房主对外发布权威名册，客户端据此对齐成员集合，并缓存各成员端点
            // 以便任一客户端在房主迁移后都能直接寻址其余成员。
            if (IsHost && _transport != null && State != RoomState.Idle)
            {
                var count = _room.Members.Count;
                var ids = new int[count];
                var endpoints = new List<PeerEndPoint>(count);
                for (var i = 0; i < count; i++)
                {
                    var memberId = _room.Members[i].PlayerId;
                    ids[i] = memberId;

                    if (_directory != null && _directory.TryGetPeer(memberId, out var address, out var port))
                    {
                        endpoints.Add(new PeerEndPoint(memberId, address, port));
                    }
                    else if (TryGetPeerEndPoint(memberId, out var known))
                    {
                        endpoints.Add(known);
                    }
                }

                _transport.Broadcast(NetworkSerializer.SerializeRoster(LocalPlayerId, ids, endpoints.ToArray()));
            }

            // 对局进行中房主补齐新加入玩家的分身。
            if (IsHost && State == RoomState.Playing && GameBootstrap.Instance != null)
            {
                GameBootstrap.Instance.EnsureRemoteAvatars();
            }
        }
    }
}
