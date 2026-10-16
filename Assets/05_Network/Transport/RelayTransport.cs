using System;
using Unity.Networking.Transport.Relay;
using UnityEngine;

namespace StarPixelSurge.Network
{
    /// <summary>
    /// 基于 Unity Relay 的传输层实现，用于互联网联机（NAT 穿透）。
    /// 房主创建 Relay 分配后，客户端通过加入码连接到同一 Relay 服务器。
    ///
    /// 注意：本类为框架占位，实际传输逻辑待 Cloud 项目绑定后调试。
    /// </summary>
    public class RelayTransport : ITransport
    {
        private RelayServerData _relayData;
        private bool _isHost;
        private bool _running;

        public int LocalPlayerId { get; private set; } = -1;

        public int HostId { get; set; } = -1;

        public bool IsRunning => _running;

        public event Action<int, byte[]> MessageReceived;

        /// <summary>房主：设置 Relay 服务器数据。</summary>
        public void SetHostData(RelayServerData serverData)
        {
            _relayData = serverData;
            _isHost = true;
        }

        /// <summary>客户端：设置 Relay 服务器数据。</summary>
        public void SetClientData(RelayServerData serverData)
        {
            _relayData = serverData;
            _isHost = false;
        }

        public void Start(int localPlayerId)
        {
            LocalPlayerId = localPlayerId;
            _running = true;
            Debug.Log($"[RelayTransport] 启动 {( _isHost ? "房主" : "客户端")} 模式 (框架占位，待调试)");
        }

        public void Stop()
        {
            _running = false;
            Debug.Log("[RelayTransport] 已停止");
        }

        public void SendToHost(byte[] payload)
        {
            // 实际发送逻辑待 Cloud 项目绑定后调试
        }

        public void Broadcast(byte[] payload)
        {
            // 实际广播逻辑待 Cloud 项目绑定后调试
        }

        public void Pump(float deltaTime)
        {
            // 实际消息接收逻辑待 Cloud 项目绑定后调试
        }
    }
}
