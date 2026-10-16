using System;
using UnityEngine;

namespace StarPixelSurge.Network
{
    /// <summary>
    /// IP直连联机管理器：不依赖Unity Relay，直接用IP+端口连接。
    /// </summary>
    public class DirectConnectionManager : MonoBehaviour
    {
        public static DirectConnectionManager Instance { get; private set; }

        public event Action OnHostStarted;
        public event Action OnClientConnected;
        public event Action OnDisconnected;

        public bool IsHost { get; private set; }
        public string ServerIP { get; private set; } = "185.242.232.191";
        public int ServerPort { get; private set; } = 7777;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>启动主机（作为服务器）。</summary>
        public void StartHost(int port = 7777)
        {
            IsHost = true;
            ServerPort = port;
            Debug.Log($"[Direct] 主机已启动，端口: {port}");
            OnHostStarted?.Invoke();

            // TODO: 实际的网络监听逻辑
            // 这里是框架，实际用Unity Netcode的Transport层
        }

        /// <summary>作为客户端连接到服务器。</summary>
        public void ConnectToServer(string ip, int port = 7777)
        {
            IsHost = false;
            ServerIP = ip;
            ServerPort = port;
            Debug.Log($"[Direct] 正在连接到 {ip}:{port}...");

            // TODO: 实际的网络连接逻辑
            // 模拟连接成功
            OnClientConnected?.Invoke();
        }

        /// <summary>断开连接。</summary>
        public void Disconnect()
        {
            IsHost = false;
            Debug.Log("[Direct] 已断开连接");
            OnDisconnected?.Invoke();
        }
    }
}
