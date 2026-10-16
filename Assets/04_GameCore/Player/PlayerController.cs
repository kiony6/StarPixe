using StarPixelSurge.Framework;
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

        public Vector2 MoveInput => _moveInput;

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
            if (stats == null)
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
        }
    }
}
