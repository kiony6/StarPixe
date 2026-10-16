using System;
using StarPixelSurge.Framework;
using StarPixelSurge.GameCore;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StarPixelSurge.Network
{
    /// <summary>
    /// IP直连联机管理器：不依赖Unity Relay，直接用IP+端口连接，整合现有NetManager。
    /// </summary>
    public class DirectConnectionManager : MonoSingleton<DirectConnectionManager>
    {
        public event Action OnHostStarted;
        public event Action OnClientConnected;
        public event Action OnDisconnected;

        public bool IsHost { get; private set; }
        public string ServerIP { get; private set; } = "185.242.232.191";
        public int ServerPort { get; private set; } = 7777;

        protected override void OnSingletonAwake()
        {
            // 确保NetManager存在
            if (NetManager.Instance == null)
            {
                var go = new GameObject("NetManager");
                go.AddComponent<NetManager>();
            }
        }

        /// <summary>启动主机（作为服务器）。</summary>
        public void StartHost(int port = 7777)
        {
            IsHost = true;
            ServerPort = port;
            Debug.Log($"[Direct] 主机已启动，端口: {port}");

            // 调用NetManager启动主机
            NetManager.Instance.StartLocalHost();

            OnHostStarted?.Invoke();

            // 跳转到游戏场景
            Debug.Log("[Direct] 跳转到游戏场景...");
            // SceneManager.LoadScene("Game");
        }

        /// <summary>作为客户端连接到服务器。</summary>
        public void ConnectToServer(string ip, int port = 7777)
        {
            IsHost = false;
            ServerIP = ip;
            ServerPort = port;
            Debug.Log($"[Direct] 正在连接到 {ip}:{port}...");

            // 调用NetManager连接服务器
            NetManager.Instance.ConnectToServer(ip, port);

            // 模拟连接成功
            OnClientConnected?.Invoke();

            // 跳转到游戏场景
            Debug.Log("[Direct] 连接成功，跳转到游戏场景...");
            // SceneManager.LoadScene("Game");
        }

        /// <summary>断开连接。</summary>
        public void Disconnect()
        {
            IsHost = false;
            Debug.Log("[Direct] 已断开连接");
            NetManager.Instance.Disconnect();
            OnDisconnected?.Invoke();
        }
    }
}
