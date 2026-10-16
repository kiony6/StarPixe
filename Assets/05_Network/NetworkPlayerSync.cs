using UnityEngine;

namespace StarPixelSurge.Network
{
    /// <summary>
    /// 玩家网络同步组件：同步位置、旋转、血量等数据。
    /// </summary>
    public class NetworkPlayerSync : MonoBehaviour
    {
        [Header("同步设置")]
        [SerializeField] private float syncInterval = 0.1f;
        [SerializeField] private float interpolationSpeed = 5f;

        public int PlayerId { get; set; }
        public bool IsLocalPlayer { get; set; }

        private float _syncTimer;
        private Vector3 _targetPosition;
        private Quaternion _targetRotation;

        private void Start()
        {
            _targetPosition = transform.position;
            _targetRotation = transform.rotation;
        }

        private void Update()
        {
            if (IsLocalPlayer)
            {
                // 本地玩家：定时发送位置
                _syncTimer += Time.deltaTime;
                if (_syncTimer >= syncInterval)
                {
                    SendPosition();
                    _syncTimer = 0f;
                }
            }
            else
            {
                // 远端玩家：插值到目标位置
                transform.position = Vector3.Lerp(transform.position, _targetPosition, Time.deltaTime * interpolationSpeed);
                transform.rotation = Quaternion.Lerp(transform.rotation, _targetRotation, Time.deltaTime * interpolationSpeed);
            }
        }

        /// <summary>发送本地玩家位置。</summary>
        private void SendPosition()
        {
            // TODO: 通过NetManager发送位置数据
            // Debug.Log($"[NetworkSync] 发送位置: {transform.position}");
        }

        /// <summary>接收远端玩家位置。</summary>
        public void OnReceivePosition(Vector3 position, Quaternion rotation)
        {
            _targetPosition = position;
            _targetRotation = rotation;
        }
    }
}
