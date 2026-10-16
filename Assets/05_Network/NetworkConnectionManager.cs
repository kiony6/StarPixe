using System;
using StarPixelSurge.Framework;
using UnityEngine;

namespace StarPixelSurge.Network
{
    /// <summary>
    /// 网络连接状态。
    /// </summary>
    public enum ConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        Reconnecting,
        HostMigration,
    }

    /// <summary>
    /// 网络连接管理器：处理断线重连、主机迁移。
    /// </summary>
    public class NetworkConnectionManager : MonoSingleton<NetworkConnectionManager>
    {
        public ConnectionState State { get; private set; } = ConnectionState.Disconnected;

        public event Action<ConnectionState> OnStateChanged;
        public event OnPlayerDisconnectedEvent OnPlayerDisconnected;
        public event OnHostMigratedEvent OnHostMigrated;

        private float _reconnectTimer;
        private const float ReconnectDelay = 3f;
        private int _reconnectAttempts;
        private const int MaxReconnectAttempts = 3;

        protected override void OnSingletonAwake()
        {
            State = ConnectionState.Disconnected;
        }

        /// <summary>开始连接。</summary>
        public void StartConnecting()
        {
            ChangeState(ConnectionState.Connecting);
            _reconnectAttempts = 0;
        }

        /// <summary>连接成功。</summary>
        public void OnConnected()
        {
            ChangeState(ConnectionState.Connected);
            _reconnectAttempts = 0;
        }

        /// <summary>玩家断线。</summary>
        public void OnPlayerDisconnectedFromNetwork(int playerId)
        {
            OnPlayerDisconnected?.Invoke(playerId);

            // 如果是房主断线，触发主机迁移
            if (NetManager.HasInstance)
            {
                var hostId = NetManager.Instance.LocalPlayerId; // 简化：用本地玩家ID判断
                // TODO: 实际判断房主是否断线
                StartHostMigration();
            }
        }

        /// <summary>开始主机迁移。</summary>
        public void StartHostMigration()
        {
            ChangeState(ConnectionState.HostMigration);
            Debug.Log("[Network] 开始主机迁移...");

            // TODO: 实际的主机迁移逻辑
            // 1. 选举新房主
            // 2. 同步状态
            // 3. 通知所有客户端

            OnHostMigrated?.Invoke();
            ChangeState(ConnectionState.Connected);
        }

        /// <summary>开始重连。</summary>
        public void StartReconnecting()
        {
            if (_reconnectAttempts >= MaxReconnectAttempts)
            {
                Debug.Log("[Network] 重连次数过多，放弃重连");
                ChangeState(ConnectionState.Disconnected);
                return;
            }

            _reconnectAttempts++;
            ChangeState(ConnectionState.Reconnecting);
            _reconnectTimer = ReconnectDelay;
            Debug.Log($"[Network] 第 {_reconnectAttempts} 次重连...");
        }

        private void Update()
        {
            if (State == ConnectionState.Reconnecting)
            {
                _reconnectTimer -= Time.deltaTime;
                if (_reconnectTimer <= 0f)
                {
                    // TODO: 实际的重连逻辑
                    Debug.Log("[Network] 尝试重连...");
                    // 模拟重连成功
                    OnConnected();
                }
            }
        }

        private void ChangeState(ConnectionState newState)
        {
            if (State == newState) return;
            State = newState;
            OnStateChanged?.Invoke(newState);
            Debug.Log($"[Network] 状态变更: {newState}");
        }
    }

    public delegate void OnPlayerDisconnectedEvent(int playerId);
    public delegate void OnHostMigratedEvent();
}
