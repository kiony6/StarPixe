using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace StarPixelSurge.Network
{
    /// <summary>
    /// UDP传输层实现：基于System.Net.Sockets.UdpClient。
    /// </summary>
    public class UdpTransport : ITransport
    {
        private UdpClient _udpClient;
        private IPEndPoint _hostEndPoint;
        private bool _isRunning;
        private int _localPlayerId;

        public event Action<int, byte[]> MessageReceived;

        public int LocalPlayerId => _localPlayerId;
        public bool IsRunning => _isRunning;
        public int HostId { get; set; } = -1;

        public void Start(int localPlayerId)
        {
            _localPlayerId = localPlayerId;
            _isRunning = true;

            _udpClient = new UdpClient(7777 + localPlayerId);
            _udpClient.BeginReceive(OnReceive, null);

            Debug.Log($"[UdpTransport] 已启动，PlayerId: {localPlayerId}, 端口: {7777 + localPlayerId}");
        }

        public void Stop()
        {
            _isRunning = false;
            _udpClient?.Close();
            Debug.Log("[UdpTransport] 已停止");
        }

        public void SetHostEndPoint(string ip, int port)
        {
            _hostEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
            Debug.Log($"[UdpTransport] 主机地址: {ip}:{port}");
        }

        public void SendToHost(byte[] payload)
        {
            if (_hostEndPoint == null || !_isRunning) return;
            _udpClient.Send(payload, payload.Length, _hostEndPoint);
        }

        public void Broadcast(byte[] payload)
        {
            // TODO: 实现广播给所有客户端
            // 现在是框架，实际需要维护客户端列表
        }

        public void Pump(float deltaTime)
        {
            // UDP是异步的，不需要手动Pump
        }

        private void OnReceive(IAsyncResult ar)
        {
            if (!_isRunning) return;

            try
            {
                var endpoint = new IPEndPoint(IPAddress.Any, 0);
                var data = _udpClient.EndReceive(ar, ref endpoint);

                // 解析发送者ID（前4字节是PlayerId）
                if (data.Length >= 4)
                {
                    var senderId = BitConverter.ToInt32(data, 0);
                    var payload = new byte[data.Length - 4];
                    Array.Copy(data, 4, payload, 0, payload.Length);

                    MessageReceived?.Invoke(senderId, payload);
                }

                // 继续接收
                _udpClient.BeginReceive(OnReceive, null);
            }
            catch (Exception e)
            {
                Debug.LogError($"[UdpTransport] 接收错误: {e.Message}");
            }
        }

        /// <summary>发送消息时带上自己的PlayerId。</summary>
        private byte[] WrapWithPlayerId(byte[] payload)
        {
            var result = new byte[4 + payload.Length];
            BitConverter.GetBytes(_localPlayerId).CopyTo(result, 0);
            payload.CopyTo(result, 4);
            return result;
        }
    }
}
