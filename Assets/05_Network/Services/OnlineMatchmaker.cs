using System;
using System.Threading.Tasks;
using StarPixelSurge.Network.Services;
using UnityEngine;

namespace StarPixelSurge.Network
{
    /// <summary>
    /// 在线联机匹配入口：整合 Relay（NAT 穿透）+ Lobby（房间列表）+ Vivox（语音），
    /// 对接现有 NetManager 帧同步架构。
    ///
    /// 主机流程：匿名登录 → 创建 Relay 分配 → 创建 Lobby（写入 Relay 码）→ NetManager.CreateRoom → 加入语音
    /// 客户端流程：匿名登录 → 输入房间码加入 Lobby → 读取 Relay 码 → 加入 Relay → NetManager.JoinRoom → 加入语音
    /// </summary>
    public static class OnlineMatchmaker
    {
        /// <summary>是否已初始化 Unity Services。</summary>
        public static bool IsInitialized { get; private set; }

        /// <summary>当前联机状态。</summary>
        public enum MatchState
        {
            Idle,
            Initializing,
            InLobby,
            InMatch,
            Error
        }

        public static MatchState State { get; private set; } = MatchState.Idle;

        /// <summary>状态变化事件。</summary>
        public static event Action<MatchState> OnStateChanged;

        /// <summary>错误事件（参数为错误信息）。</summary>
        public static event Action<string> OnError;

        /// <summary>
        /// 初始化 Unity Services（匿名登录 + Vivox 初始化）。必须在任何联机操作前调用。
        /// </summary>
        public static async Task InitializeAsync()
        {
            if (IsInitialized) return;

            State = MatchState.Initializing;
            RaiseStateChanged();

            try
            {
                await RelayRelayService.InitializeAsync();
                try
                {
                    await VivoxVoiceAdapter.InitializeAsync();
                }
                catch (Exception vivoxEx)
                {
                    Debug.LogWarning($"[Matchmaker] Vivox 初始化失败（语音功能暂不可用）: {vivoxEx.Message}");
                }
                IsInitialized = true;
                Debug.Log("[Matchmaker] 初始化完成");
            }
            catch (Exception e)
            {
                State = MatchState.Error;
                RaiseStateChanged();
                OnError?.Invoke($"初始化失败: {e.Message}");
                Debug.LogError($"[Matchmaker] 初始化失败: {e}");
            }
        }

        /// <summary>
        /// 主机：创建在线房间。
        /// </summary>
        /// <param name="roomName">房间名。</param>
        /// <param name="maxPlayers">最大玩家数。</param>
        /// <returns>房间加入码（分享给好友加入）。</returns>
        public static async Task<string> CreateOnlineRoomAsync(string roomName, int maxPlayers = 8)
        {
            await EnsureInitializedAsync();

            try
            {
                // 1. 创建 Relay 分配
                var relayJoinCode = await RelayRelayService.CreateAllocationAsync(maxPlayers);
                Debug.Log($"[Matchmaker] Relay 创建成功: {relayJoinCode}");

                // 2. 创建 Lobby，把 Relay 码存进去
                var lobby = await LobbyServiceAdapter.CreateLobbyAsync(roomName, maxPlayers, relayJoinCode);
                var lobbyCode = lobby.LobbyCode;

                // 3. 配置 NetManager 使用 Relay 传输
                var relayData = RelayRelayService.GetHostRelayData();
                NetManager.Instance.TransportFactory = () =>
                {
                    var transport = new RelayTransport();
                    transport.SetHostData(relayData);
                    return transport;
                };
                NetManager.Instance.UseUdp = true;

                // 4. 启动 NetManager 房间
                NetManager.Instance.CreateRoom();

                // 4. 加入语音频道（用 Lobby ID）
                await VivoxVoiceAdapter.JoinVoiceChannelAsync(lobby.Id);

                State = MatchState.InLobby;
                RaiseStateChanged();

                Debug.Log($"[Matchmaker] 房间创建成功！房间码: {lobbyCode}");
                return lobbyCode;
            }
            catch (Exception e)
            {
                State = MatchState.Error;
                RaiseStateChanged();
                OnError?.Invoke($"创建房间失败: {e.Message}");
                Debug.LogError($"[Matchmaker] 创建房间失败: {e}");
                return null;
            }
        }

        /// <summary>
        /// 客户端：通过房间码加入在线房间。
        /// </summary>
        /// <param name="lobbyCode">房间加入码。</param>
        public static async Task JoinOnlineRoomAsync(string lobbyCode)
        {
            await EnsureInitializedAsync();

            try
            {
                // 1. 加入 Lobby
                var lobby = await LobbyServiceAdapter.JoinLobbyByCodeAsync(lobbyCode);

                // 2. 读取 Relay 加入码
                var relayJoinCode = LobbyServiceAdapter.GetRelayJoinCode();
                if (string.IsNullOrEmpty(relayJoinCode))
                {
                    throw new Exception("房间中没有 Relay 加入码，房主可能还未准备好");
                }

                // 3. 加入 Relay 分配
                await RelayRelayService.JoinAllocationAsync(relayJoinCode);

                // 4. 配置 NetManager 使用 Relay 传输
                var relayData = RelayRelayService.GetClientRelayData();
                NetManager.Instance.TransportFactory = () =>
                {
                    var transport = new RelayTransport();
                    transport.SetClientData(relayData);
                    return transport;
                };
                NetManager.Instance.UseUdp = true;

                // 5. 加入 NetManager 房间
                NetManager.Instance.JoinRoom(lobby.Id);

                // 6. 加入语音频道
                await VivoxVoiceAdapter.JoinVoiceChannelAsync(lobby.Id);

                State = MatchState.InLobby;
                RaiseStateChanged();

                Debug.Log("[Matchmaker] 加入房间成功");
            }
            catch (Exception e)
            {
                State = MatchState.Error;
                RaiseStateChanged();
                OnError?.Invoke($"加入房间失败: {e.Message}");
                Debug.LogError($"[Matchmaker] 加入房间失败: {e}");
            }
        }

        /// <summary>
        /// 离开房间并清理所有服务。
        /// </summary>
        public static async Task LeaveRoomAsync()
        {
            try
            {
                NetManager.Instance.LeaveRoom();
                await LobbyServiceAdapter.LeaveLobbyAsync();
                await VivoxVoiceAdapter.LeaveVoiceChannelAsync();
                RelayRelayService.Disconnect();

                State = MatchState.Idle;
                RaiseStateChanged();
                Debug.Log("[Matchmaker] 已离开房间");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Matchmaker] 离开房间时出错: {e.Message}");
            }
        }

        private static async Task EnsureInitializedAsync()
        {
            if (!IsInitialized)
            {
                await InitializeAsync();
                if (!IsInitialized)
                {
                    throw new Exception("Unity Services 未初始化成功");
                }
            }
        }

        private static void RaiseStateChanged()
        {
            OnStateChanged?.Invoke(State);
        }
    }
}
