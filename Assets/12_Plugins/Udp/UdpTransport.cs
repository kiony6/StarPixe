using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace StarPixelSurge.Network
{
    /// <summary>
    /// 基于 UDP 套接字的真实网络传输实现，用于替换进程内回环传输。
    /// 每个数据报格式为 [4 字节发送者 ID][负载]，负载与回环传输一致，
    /// 因此帧同步、输入回放、快照插值与房主迁移逻辑无需改动。
    ///
    /// 房主绑定固定端口，从客户端首个数据报学习其端点后向全部已知端点广播；
    /// 客户端绑定临时端口并直接向配置的房主端点发送。UDP 不保证可靠与有序，
    /// 因此控制类消息（准备/名册/开局）依赖上层重发与名册周期同步容忍丢包。
    /// 单数据报超过 <see cref="MaxDatagramBytes"/> 会被拒绝，快照分片不在本期范围。
    /// </summary>
    public class UdpTransport : ITransport, IPeerDirectory
    {
        private const int HeaderBytes = 4;
        private const int MaxDatagramBytes = 1200;

        private readonly Dictionary<int, IPEndPoint> _endpoints = new Dictionary<int, IPEndPoint>();
        private readonly List<IPEndPoint> _broadcastScratch = new List<IPEndPoint>();
        private readonly byte[] _sendScratch = new byte[MaxDatagramBytes];

        private UdpClient _client;
        private IPEndPoint _hostEndPoint;

        public int LocalPlayerId { get; private set; } = -1;

        public int HostId { get; set; } = -1;

        public bool IsRunning => _client != null;

        /// <summary>本机绑定的端口（房主为固定端口，客户端为临时端口）。</summary>
        public int LocalPort { get; private set; }

        public event Action<int, byte[]> MessageReceived;

        /// <summary>观测到某成员的来源端点（每收到一个数据报触发一次）。</summary>
        public event Action<int, string, int> PeerObserved;

        /// <summary>房主：指定绑定端口。</summary>
        public void SetLocalPort(int port)
        {
            LocalPort = Mathf.Max(0, port);
        }

        /// <summary>客户端：指定房主的地址与端口。</summary>
        public void SetHostEndPoint(string host, int port)
        {
            _hostEndPoint = ResolveEndPoint(host, port);
        }

        /// <summary>登记/更新某成员的端点，供广播与迁移后寻址。</summary>
        public void RegisterPeer(int playerId, string address, int port)
        {
            if (playerId < 0 || string.IsNullOrEmpty(address) || port <= 0)
            {
                return;
            }

            _endpoints[playerId] = ResolveEndPoint(address, port);
        }

        public bool TryGetPeer(int playerId, out string address, out int port)
        {
            address = null;
            port = 0;
            if (!_endpoints.TryGetValue(playerId, out var endpoint) || endpoint == null)
            {
                return false;
            }

            address = endpoint.Address.ToString();
            port = endpoint.Port;
            return true;
        }

        /// <summary>重定向上报目标端点（房主迁移后由客户端调用）。</summary>
        public bool TrySetHost(string address, int port)
        {
            if (string.IsNullOrEmpty(address) || port <= 0)
            {
                return false;
            }

            _hostEndPoint = ResolveEndPoint(address, port);
            return true;
        }

        public void Start(int localPlayerId)
        {
            if (IsRunning)
            {
                Stop();
            }

            LocalPlayerId = localPlayerId;
            _client = new UdpClient(LocalPort);
            _client.Client.Blocking = false;
        }

        public void Stop()
        {
            if (_client == null)
            {
                return;
            }

            try
            {
                _client.Close();
            }
            catch (Exception)
            {
                // 关闭期间的套接字异常可忽略。
            }

            _client = null;
            _endpoints.Clear();
        }

        public void SendToHost(byte[] payload)
        {
            if (!IsRunning || _hostEndPoint == null || payload == null)
            {
                return;
            }

            Send(payload, _hostEndPoint);
        }

        public void Broadcast(byte[] payload)
        {
            if (!IsRunning || payload == null)
            {
                return;
            }

            // 客户端无对等表，准备等广播消息统一上报房主；房主则发给全部已知客户端。
            if (_hostEndPoint != null && !IsHost())
            {
                Send(payload, _hostEndPoint);
                return;
            }

            _broadcastScratch.Clear();
            foreach (var endpoint in _endpoints.Values)
            {
                _broadcastScratch.Add(endpoint);
            }

            for (var i = 0; i < _broadcastScratch.Count; i++)
            {
                Send(payload, _broadcastScratch[i]);
            }
        }

        public void Pump(float deltaTime)
        {
            if (!IsRunning)
            {
                return;
            }

            try
            {
                while (_client.Available > 0)
                {
                    IPEndPoint remote = null;
                    var data = _client.Receive(ref remote);
                    if (data == null || data.Length <= HeaderBytes)
                    {
                        continue;
                    }

                    var senderId = ReadInt(data, 0);
                    Observe(remote, senderId);

                    var payload = new byte[data.Length - HeaderBytes];
                    Buffer.BlockCopy(data, HeaderBytes, payload, 0, payload.Length);
                    MessageReceived?.Invoke(senderId, payload);
                }
            }
            catch (SocketException)
            {
                // 非阻塞套接字在无数据或暂时不可读时可能抛出，忽略并等待下一帧。
            }
            catch (ObjectDisposedException)
            {
                // 套接字在 Pump 期间被关闭，忽略。
            }
        }

        private void Send(byte[] payload, IPEndPoint target)
        {
            if (_client == null || target == null)
            {
                return;
            }

            var total = HeaderBytes + payload.Length;
            if (total > MaxDatagramBytes)
            {
                Debug.LogWarning($"[UdpTransport] 数据报 {total} 字节超过上限 {MaxDatagramBytes}，已丢弃。");
                return;
            }

            if (total <= _sendScratch.Length)
            {
                WriteInt(_sendScratch, 0, LocalPlayerId);
                Buffer.BlockCopy(payload, 0, _sendScratch, HeaderBytes, payload.Length);
                TrySend(_sendScratch, total, target);
                return;
            }

            var buffer = new byte[total];
            WriteInt(buffer, 0, LocalPlayerId);
            Buffer.BlockCopy(payload, 0, buffer, HeaderBytes, payload.Length);
            TrySend(buffer, total, target);
        }

        private void TrySend(byte[] buffer, int length, IPEndPoint target)
        {
            try
            {
                _client.Send(buffer, length, target);
            }
            catch (SocketException)
            {
                // 目标暂不可达时丢弃该数据报；由上层重发机制兜底。
            }
        }

        private void Observe(IPEndPoint remote, int senderId)
        {
            if (remote == null || senderId < 0)
            {
                return;
            }

            // 房主记录来源端点即等价于登记客户端，便于快照广播定向发送。
            if (IsHost())
            {
                _endpoints[senderId] = remote;
            }

            // 无论角色都向订阅者暴露来源端点，供上层刷新房主地址、跨过迁移。
            PeerObserved?.Invoke(senderId, remote.Address.ToString(), remote.Port);
        }

        private bool IsHost()
        {
            return HostId == LocalPlayerId && LocalPlayerId >= 0;
        }

        private static IPEndPoint ResolveEndPoint(string host, int port)
        {
            port = Mathf.Clamp(port, 1, 65535);
            if (string.IsNullOrEmpty(host))
            {
                host = "127.0.0.1";
            }

            if (IPAddress.TryParse(host, out var parsed))
            {
                return new IPEndPoint(parsed, port);
            }

            try
            {
                var addresses = Dns.GetHostAddresses(host);
                if (addresses != null && addresses.Length > 0)
                {
                    return new IPEndPoint(addresses[0], port);
                }
            }
            catch (SocketException)
            {
                // 解析失败回退本地回环，便于同机联调。
            }

            return new IPEndPoint(IPAddress.Loopback, port);
        }

        private static int ReadInt(byte[] data, int offset)
        {
            return data[offset]
                   | (data[offset + 1] << 8)
                   | (data[offset + 2] << 16)
                   | (data[offset + 3] << 24);
        }

        private static void WriteInt(byte[] data, int offset, int value)
        {
            data[offset] = (byte)(value & 0xFF);
            data[offset + 1] = (byte)((value >> 8) & 0xFF);
            data[offset + 2] = (byte)((value >> 16) & 0xFF);
            data[offset + 3] = (byte)((value >> 24) & 0xFF);
        }
    }
}
