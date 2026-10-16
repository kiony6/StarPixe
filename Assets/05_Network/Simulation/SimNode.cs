using System.Collections.Generic;
using UnityEngine;

namespace StarPixelSurge.Network.Simulation
{
    /// <summary>
    /// 仿真中的一个对局节点（房主或客户端）。复用真实的房间、输入缓冲、协议序列化与
    /// 快照插值组件，只把 Unity 场景装配替换为纯数据世界，从而在单进程内完整跑通
    /// 加入 → 准备 → 开局 → 输入回放 → 快照广播 → 插值收敛 → 房主迁移的全链路。
    /// </summary>
    public sealed class SimNode
    {
        private sealed class SimPlayer
        {
            public Vector2 Position;
            public Vector2 Input;
            public float Speed;
            public float Health;
            public float MaxHealth;
            public int AuraBonus;
        }

        private const float MoveSpeed = 5f;
        private const float StartHealth = 100f;
        private const float TeamAuraRadius = 4f;
        private const int InputLeadFrames = 2;
        private const int HistoryRetain = 256;
        private const float LobbySyncInterval = 0.25f;

        private readonly int _seed;
        private readonly Dictionary<int, SimPlayer> _world = new Dictionary<int, SimPlayer>();
        private readonly Dictionary<int, string> _classByPlayer = new Dictionary<int, string>();
        private readonly SortedDictionary<int, StateSnapshot> _history = new SortedDictionary<int, StateSnapshot>();
        private readonly ReviveTracker _revive = new ReviveTracker();
        private readonly List<TeamPlayerState> _teamStates = new List<TeamPlayerState>();
        private readonly List<int> _revived = new List<int>();
        private float _accumulator;
        private float _lobbyTimer;
        private bool _ready;

        public SimNode(SimNetwork network, int playerId, NetworkRole role, string classId, int tickRate, int seed)
        {
            PlayerId = playerId;
            Role = role;
            ClassId = classId;
            TickRate = Mathf.Max(1, tickRate);
            _seed = seed;

            Transport = new SimTransport(network, playerId);
            Transport.MessageReceived += OnMessage;

            Room = new RoomService();
            Room.RosterChanged += OnRosterChanged;

            Interp = new SnapshotInterpolator();

            Migration = new HostMigrationService();
            Migration.HostElected += OnHostElected;
        }

        public int PlayerId { get; }

        public NetworkRole Role { get; private set; }

        public string ClassId { get; }

        public int TickRate { get; }

        public bool Playing { get; private set; }

        public int Frame { get; private set; }

        public SimTransport Transport { get; }

        public RoomService Room { get; }

        public InputBuffer Inputs { get; } = new InputBuffer();

        public SnapshotInterpolator Interp { get; }

        public HostMigrationService Migration { get; }

        public int HostWorldCount => _world.Count;

        /// <summary>初始/满额生命值（仿真常量，供断言使用）。</summary>
        public const float MaxPlayerHealth = StartHealth;

        public float ReviveDuration => _revive.ReviveDuration;

        public float ReviveHealthFraction => _revive.ReviveHealthFraction;

        /// <summary>房主记录的各玩家职业 ID（来自准备包）。</summary>
        public string ClassOf(int playerId)
        {
            return _classByPlayer.TryGetValue(playerId, out var classId) ? classId : null;
        }

        /// <summary>房主：创建房间并把自己设为房主。</summary>
        public void CreateRoom()
        {
            Room.Join(PlayerId);
            Room.SetHost(PlayerId);
            Transport.Start(PlayerId);
            Transport.HostId = PlayerId;
            Migration.Configure(Transport, Room, null, PlayerId);
        }

        /// <summary>客户端：以房主身份加入房间并请求名册同步。</summary>
        public void JoinRoom(int hostId)
        {
            Room.Join(hostId);
            Room.SetHost(hostId);
            Room.Join(PlayerId);
            Transport.Start(PlayerId);
            Transport.HostId = hostId;
            Migration.Configure(Transport, Room, null, hostId);
            Transport.SendToHost(NetworkSerializer.SerializeJoinRequest(PlayerId));
        }

        public void SetReady(bool ready)
        {
            _ready = ready;
            Room.SetReady(PlayerId, ready);
            Transport.Broadcast(NetworkSerializer.SerializeReady(PlayerId, ready, ClassId));
        }

        /// <summary>房主尝试开局，成功返回 true。</summary>
        public bool TryStartMatch()
        {
            if (Role != NetworkRole.Host || Playing || !Room.CanStart(PlayerId))
            {
                return false;
            }

            Room.LockRoster();
            Playing = true;
            InitWorld();
            Transport.Broadcast(NetworkSerializer.SerializeMatchStart(_seed, ClassId));
            return true;
        }

        /// <summary>提交本地输入。房主写入自身帧缓冲，客户端发送到房主。</summary>
        public void SubmitLocalInput(Vector2 direction)
        {
            if (Role == NetworkRole.Host)
            {
                Inputs.Submit(new PlayerInputPacket(PlayerId, Frame + InputLeadFrames, direction, -1, false));
                return;
            }

            if (Role == NetworkRole.Client && Transport.IsRunning && Transport.HostId >= 0)
            {
                var frame = Interp.LatestFrame >= 0 ? Interp.LatestFrame + InputLeadFrames : InputLeadFrames;
                Transport.SendToHost(NetworkSerializer.SerializeInput(
                    new PlayerInputPacket(PlayerId, frame, direction, -1, false)));
            }
        }

        public void Tick(float deltaTime)
        {
            Migration.Tick(deltaTime);

            if (!Playing)
            {
                _lobbyTimer += deltaTime;
                if (_lobbyTimer >= LobbySyncInterval)
                {
                    _lobbyTimer = 0f;
                    LobbySync();
                }
            }

            if (Role == NetworkRole.Host)
            {
                HostTick(deltaTime);
            }
            else
            {
                Interp.Advance(deltaTime, TickRate);
            }
        }

        /// <summary>大厅期周期重发加入/准备与名册，保证抖动丢包下握手仍能收敛。</summary>
        private void LobbySync()
        {
            if (!Transport.IsRunning)
            {
                return;
            }

            if (Role == NetworkRole.Host)
            {
                if (_ready)
                {
                    Transport.Broadcast(NetworkSerializer.SerializeReady(PlayerId, true, ClassId));
                }

                BroadcastRoster();
                return;
            }

            Transport.SendToHost(NetworkSerializer.SerializeJoinRequest(PlayerId));
            if (_ready)
            {
                Transport.Broadcast(NetworkSerializer.SerializeReady(PlayerId, true, ClassId));
            }
        }

        /// <summary>该节点是否由本地提交输入（每个仿真节点都只控制自己的玩家）。</summary>
        public bool ControlsPlayer(int playerId)
        {
            return playerId == PlayerId;
        }

        public bool TryGetPlayerPosition(int playerId, out Vector2 position)
        {
            if (_world.TryGetValue(playerId, out var player))
            {
                position = player.Position;
                return true;
            }

            position = default;
            return false;
        }

        public bool TryGetHistory(int frame, out StateSnapshot snapshot)
        {
            return _history.TryGetValue(frame, out snapshot);
        }

        public int HistoryCount => _history.Count;

        /// <summary>仿真测试钩子：读取某玩家当前生命值。</summary>
        public bool TryGetHealth(int playerId, out float health)
        {
            if (_world.TryGetValue(playerId, out var player))
            {
                health = player.Health;
                return true;
            }

            health = 0f;
            return false;
        }

        public bool IsAlive(int playerId)
        {
            return _world.TryGetValue(playerId, out var player) && player.Health > 0f;
        }

        public int GetAuraBonus(int playerId)
        {
            return _world.TryGetValue(playerId, out var player) ? player.AuraBonus : 0;
        }

        /// <summary>仿真测试钩子：对某玩家造成伤害（用于构造倒地场景）。</summary>
        public void ApplyDamage(int playerId, float amount)
        {
            if (_world.TryGetValue(playerId, out var player))
            {
                player.Health = Mathf.Max(0f, player.Health - Mathf.Max(0f, amount));
            }
        }

        /// <summary>仿真测试钩子：直接设置某玩家位置。</summary>
        public void SetPlayerPosition(int playerId, Vector2 position)
        {
            if (_world.TryGetValue(playerId, out var player))
            {
                player.Position = position;
            }
        }

        /// <summary>仿真测试钩子：清空所有玩家输入与输入缓冲，冻结队伍机制测试期间的位移。</summary>
        public void ClearInputs()
        {
            foreach (var player in _world.Values)
            {
                player.Input = Vector2.zero;
            }

            Inputs.Clear();
        }

        private void HostTick(float deltaTime)
        {
            if (!Playing)
            {
                return;
            }

            var interval = 1f / TickRate;
            _accumulator += deltaTime;

            while (_accumulator >= interval)
            {
                _accumulator -= interval;
                Frame++;

                var inputs = Inputs.CollectLatest(Frame);
                for (var i = 0; i < inputs.Count; i++)
                {
                    if (_world.TryGetValue(inputs[i].PlayerId, out var player))
                    {
                        player.Input = AvatarMotion.ClampInput(inputs[i].MoveDir);
                    }
                }

                foreach (var player in _world.Values)
                {
                    if (player.Health <= 0f)
                    {
                        continue;
                    }

                    player.Position = AvatarMotion.Integrate(player.Position, player.Input, player.Speed, interval);
                }

                TickTeamMechanics(interval);

                var snapshot = BuildSnapshot();
                _history[Frame] = snapshot;
                PruneHistory();
                Transport.Broadcast(NetworkSerializer.SerializeSnapshot(snapshot));

                Inputs.PruneBefore(Frame);
            }
        }

        private StateSnapshot BuildSnapshot()
        {
            var entities = new EntityState[_world.Count];
            var index = 0;
            foreach (var pair in _world)
            {
                var player = pair.Value;
                var alive = player.Health > 0f;
                entities[index++] = new EntityState(
                    pair.Key,
                    EntityType.Player,
                    player.Position,
                    player.Health,
                    alive ? EntityStateFlags.Alive : EntityStateFlags.None);
            }

            return new StateSnapshot(Frame, entities, 0, (uint)Frame);
        }

        /// <summary>房主按帧运行队友复活与跨玩家光环增益（复用纯逻辑实现）。</summary>
        private void TickTeamMechanics(float interval)
        {
            _teamStates.Clear();
            foreach (var pair in _world)
            {
                _teamStates.Add(new TeamPlayerState(pair.Key, pair.Value.Position, pair.Value.Health > 0f));
            }

            _revive.Tick(_teamStates, interval, _revived);
            for (var i = 0; i < _revived.Count; i++)
            {
                if (_world.TryGetValue(_revived[i], out var revived))
                {
                    revived.Health = revived.MaxHealth * _revive.ReviveHealthFraction;
                }
            }

            foreach (var pair in _world)
            {
                if (pair.Value.Health <= 0f)
                {
                    pair.Value.AuraBonus = 0;
                    continue;
                }

                pair.Value.AuraBonus = TeamAura.NearbyAllyCount(_teamStates, pair.Key, TeamAuraRadius);
            }
        }

        private void PruneHistory()
        {
            var cutoff = Frame - HistoryRetain;
            var stale = new List<int>();
            foreach (var frame in _history.Keys)
            {
                if (frame < cutoff)
                {
                    stale.Add(frame);
                }
                else
                {
                    break;
                }
            }

            for (var i = 0; i < stale.Count; i++)
            {
                _history.Remove(stale[i]);
            }
        }

        private void InitWorld()
        {
            _world.Clear();
            for (var i = 0; i < Room.Members.Count; i++)
            {
                _world[Room.Members[i].PlayerId] = new SimPlayer
                {
                    Position = Vector2.zero,
                    Input = Vector2.zero,
                    Speed = MoveSpeed,
                    Health = StartHealth,
                    MaxHealth = StartHealth,
                    AuraBonus = 0
                };
            }

            _revive.Clear();
        }

        private void OnMessage(int senderId, byte[] payload)
        {
            if (payload == null || payload.Length == 0)
            {
                return;
            }

            Migration.MarkPeerSeen(senderId);

            switch ((NetworkMessageType)payload[0])
            {
                case NetworkMessageType.PlayerInput:
                    if (Role == NetworkRole.Host && NetworkSerializer.TryDeserializeInput(payload, out var input))
                    {
                        Inputs.Submit(input);
                    }

                    break;

                case NetworkMessageType.StateSnapshot:
                    if (Role == NetworkRole.Client && NetworkSerializer.TryDeserializeSnapshot(payload, out var snapshot))
                    {
                        // 快照只在房主进入对局后广播，收到即视为对局已开始，
                        // 使客户端在 MatchStart 丢包时仍能进入对局。
                        Playing = true;
                        Interp.Apply(snapshot);
                        Migration.CacheSnapshot(snapshot);
                    }

                    break;

                case NetworkMessageType.PlayerReady:
                    if (NetworkSerializer.TryDeserializeReady(payload, out var readyPlayer, out var readyValue, out var readyClass))
                    {
                        Room.SetReady(readyPlayer, readyValue);
                        if (!string.IsNullOrEmpty(readyClass))
                        {
                            _classByPlayer[readyPlayer] = readyClass;
                        }
                    }

                    break;

                case NetworkMessageType.Roster:
                    if (Role == NetworkRole.Client
                        && NetworkSerializer.TryDeserializeRoster(payload, out var rosterHost, out var members))
                    {
                        Room.SyncMembers(rosterHost, members);
                        Migration.SetHost(rosterHost);
                        Transport.HostId = rosterHost;
                    }

                    break;

                case NetworkMessageType.JoinRequest:
                    if (Role == NetworkRole.Host && NetworkSerializer.TryDeserializeJoinRequest(payload, out var joinPlayer))
                    {
                        Room.Join(joinPlayer);
                        // 显式重发名册：客户端加入请求可能因丢包重发，直接回应更可靠。
                        BroadcastRoster();
                    }

                    break;

                case NetworkMessageType.MatchStart:
                    if (Role == NetworkRole.Client && !Playing
                        && NetworkSerializer.TryDeserializeMatchStart(payload, out _, out _))
                    {
                        Playing = true;
                    }

                    break;

                case NetworkMessageType.HostHeartbeat:
                    Migration.HandleHeartbeat(payload);
                    break;

                case NetworkMessageType.HostMigration:
                    Migration.HandleMigration(payload);
                    break;
            }
        }

        private void OnRosterChanged()
        {
            if (Playing)
            {
                return;
            }

            BroadcastRoster();
        }

        private void BroadcastRoster()
        {
            if (Role != NetworkRole.Host || !Transport.IsRunning)
            {
                return;
            }

            var ids = new int[Room.Members.Count];
            for (var i = 0; i < ids.Length; i++)
            {
                ids[i] = Room.Members[i].PlayerId;
            }

            Transport.Broadcast(NetworkSerializer.SerializeRoster(PlayerId, ids));
        }

        private void OnHostElected(int newHostId, int frameIndex)
        {
            if (newHostId == PlayerId)
            {
                Role = NetworkRole.Host;
                Transport.HostId = PlayerId;
                Migration.SetHost(PlayerId);
                ResumeAsHost();
                Transport.Broadcast(NetworkSerializer.SerializeMigration(PlayerId, Frame));
            }
            else
            {
                Role = NetworkRole.Client;
                Transport.HostId = newHostId;
            }
        }

        /// <summary>新房主接管：以最后确认快照恢复权威世界并继续推进。</summary>
        private void ResumeAsHost()
        {
            Playing = true;

            if (Migration.HasConfirmedSnapshot)
            {
                var snapshot = Migration.LastConfirmedSnapshot;
                _world.Clear();
                for (var i = 0; i < snapshot.Entities.Length; i++)
                {
                    var entity = snapshot.Entities[i];
                    if (entity.Type != EntityType.Player)
                    {
                        continue;
                    }

                    _world[entity.EntityId] = new SimPlayer
                    {
                        Position = entity.Position,
                        Input = Vector2.zero,
                        Speed = MoveSpeed,
                        Health = entity.Health > 0f ? entity.Health : StartHealth,
                        MaxHealth = StartHealth,
                        AuraBonus = 0
                    };
                }

                Frame = snapshot.FrameIndex;
            }
            else if (_world.Count == 0)
            {
                InitWorld();
            }
        }
    }
}
