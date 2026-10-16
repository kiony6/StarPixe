using StarPixelSurge.Framework;
using StarPixelSurge.GameCore;
using StarPixelSurge.Map;
using UnityEngine;

namespace StarPixelSurge.Network
{
    /// <summary>
    /// 房主权威玩家分身。房主为每位接入玩家维护一个分身实体：
    /// 远端分身依据回放输入逐逻辑帧积分位置并纳入快照广播；
    /// 客户端本地分身不自行模拟，而是把本地输入上报房主，并按房主快照插值渲染自身位置，
    /// 从而保证所有玩家看到的画面一致。
    /// </summary>
    [RequireComponent(typeof(PlayerStats))]
    public class NetworkPlayerAvatar : MonoBehaviour, INetworkEntity
    {
        [SerializeField] private float moveSpeedFallback = 5f;

        private PlayerStats _stats;
        private RoomMap _map;
        private Vector2 _input;
        private bool _authoritative;
        private bool _isLocal;

        /// <summary>玩家 ID。同时作为网络实体 ID，保证房主与客户端映射一致。</summary>
        public int PlayerId { get; private set; }

        /// <summary>是否为房主权威分身（房主负责模拟远端玩家）。</summary>
        public bool IsAuthoritative => _authoritative;

        /// <summary>是否为本地玩家分身。</summary>
        public bool IsLocal => _isLocal;

        /// <summary>是否由房主权威模拟（远端玩家分身）。</summary>
        public bool IsSimulated => _authoritative && !_isLocal;

        public PlayerStats Stats => _stats;

        public Vector2 CurrentInput => _input;

        public int NetworkId => PlayerId;

        public EntityType NetworkType => EntityType.Player;

        public float NetworkHealth => _stats != null ? _stats.CurrentHealth : 0f;

        public EntityStateFlags NetworkFlags
        {
            get
            {
                if (_stats == null || _stats.IsDead)
                {
                    return EntityStateFlags.None;
                }

                return EntityStateFlags.Alive;
            }
        }

        public Transform NetworkTransform => transform;

        private void Awake()
        {
            Cache();
        }

        private void OnEnable()
        {
            Cache();
            Register();
        }

        private void OnDisable()
        {
            Unregister();
        }

        private void Cache()
        {
            if (_stats == null)
            {
                _stats = GetComponent<PlayerStats>();
            }
        }

        /// <summary>配置分身。房主自机分身仅注册；远端分身由回放驱动。</summary>
        public void Configure(int playerId, bool authoritative, bool isLocal, RoomMap map)
        {
            PlayerId = playerId;
            _authoritative = authoritative;
            _isLocal = isLocal;
            _map = map;
            _input = Vector2.zero;
            Cache();

            Register();
        }

        public void SetMap(RoomMap map)
        {
            _map = map;
        }

        /// <summary>记录回放输入。房主在逻辑帧推进前由输入回放器调用。</summary>
        public void ApplyInput(Vector2 moveDir)
        {
            _input = moveDir.sqrMagnitude > 1f ? moveDir.normalized : moveDir;
        }

        /// <summary>房主按逻辑帧推进远端分身位置。</summary>
        public void Tick(float deltaTime)
        {
            if (!IsSimulated || _stats == null || _stats.IsDead)
            {
                return;
            }

            if (_input.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            var speed = _stats.MoveSpeed > 0f ? _stats.MoveSpeed : moveSpeedFallback;
            var self = (Vector2)transform.position;
            var next = AvatarMotion.Integrate(self, _input, speed, deltaTime);
            transform.position = ResolveWalkable(self, next);
        }

        /// <summary>按房主快照设置位置（客户端本地分身渲染用）。</summary>
        public void SetAuthoritativePosition(Vector2 position)
        {
            transform.position = new Vector3(position.x, position.y, transform.position.z);
        }

        private void Update()
        {
            if (_stats == null || !FrameSyncService.HasInstance)
            {
                return;
            }

            if (GameManager.HasInstance && GameManager.Instance.State != GameState.Playing)
            {
                return;
            }

            // 仅客户端本地分身需要跟随房主快照；其余分身位置由各自的权威模拟决定。
            if (_isLocal && !_authoritative
                && FrameSyncService.Instance.Role == NetworkRole.Client
                && FrameSyncService.Instance.TryGetInterpolated(PlayerId, out var state))
            {
                SetAuthoritativePosition(state.Position);
            }
        }

        /// <summary>沿可行走轴滑动，避免分身穿墙或卡在墙内。</summary>
        private Vector2 ResolveWalkable(Vector2 self, Vector2 next)
        {
            if (IsWalkable(next))
            {
                return next;
            }

            var slideX = new Vector2(next.x, self.y);
            if (IsWalkable(slideX))
            {
                return slideX;
            }

            var slideY = new Vector2(self.x, next.y);
            if (IsWalkable(slideY))
            {
                return slideY;
            }

            return self;
        }

        private bool IsWalkable(Vector2 world)
        {
            if (_map != null)
            {
                return _map.IsFloor(world);
            }

            if (NavigationService.HasInstance && NavigationService.Instance.IsReady)
            {
                return NavigationService.Instance.IsWalkable(world);
            }

            return true;
        }

        private void Register()
        {
            if (!NetworkEntitySync.HasInstance)
            {
                return;
            }

            NetworkEntitySync.Instance.RegisterLocal(this);
        }

        private void Unregister()
        {
            if (NetworkEntitySync.HasInstance)
            {
                NetworkEntitySync.Instance.UnregisterLocal(PlayerId);
            }
        }
    }
}
