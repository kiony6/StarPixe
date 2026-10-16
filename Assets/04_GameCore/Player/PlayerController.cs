using StarPixelSurge.Framework;
using StarPixelSurge.Network;
using UnityEngine;

namespace StarPixelSurge.GameCore
{
    /// <summary>
    /// 玩家移动控制。读取输入并驱动 Rigidbody2D 位移。仅在对局进行中响应输入。
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        public static PlayerController Instance { get; private set; }

        [SerializeField] private PlayerStats stats;

        private Rigidbody2D _rb;
        private Vector2 _moveInput;
        private bool _networkControlled;

        public Vector2 MoveInput => _moveInput;

        /// <summary>位置是否由房主快照驱动（联机客户端本地角色）。</summary>
        public bool NetworkControlled => _networkControlled;

        /// <summary>
        /// 切换网络控制。开启后本控制器不再本地位移，位置交由快照插值决定。
        /// </summary>
        public void SetNetworkControlled(bool value)
        {
            _networkControlled = value;
        }

        private void Awake()
        {
            Instance = this;
            _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = 0f;
            _rb.freezeRotation = true;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            if (stats == null)
            {
                stats = GetComponent<PlayerStats>();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            ReadInput();
        }

        private void FixedUpdate()
        {
            if (stats == null || _networkControlled)
            {
                return;
            }

            var delta = _moveInput * stats.MoveSpeed * Time.fixedDeltaTime;
            _rb.MovePosition(_rb.position + delta);
        }

        private void ReadInput()
        {
            if (GameManager.HasInstance && GameManager.Instance.State != GameState.Playing)
            {
                _moveInput = Vector2.zero;
                return;
            }

            _moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (_moveInput.sqrMagnitude > 1f)
            {
                _moveInput.Normalize();
            }

            // 联机对局中上报本地输入：客户端发送到房主；房主写入自身逻辑帧输入缓冲，
            // 两条路径都经 FrameSyncService.SubmitLocalInput 统一处理。
            if (NetManager.HasInstance
                && NetManager.Instance.State == RoomState.Playing
                && FrameSyncService.HasInstance)
            {
                FrameSyncService.Instance.SubmitLocalInput(_moveInput);
            }
        }
    }
}
