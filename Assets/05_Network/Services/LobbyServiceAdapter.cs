using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

namespace StarPixelSurge.Network.Services
{
    /// <summary>
    /// Unity Lobby 服务封装：创建公开房间、通过加入码加入、查询房间列表。
    /// Lobby 中存储 Relay 加入码，实现"进房间 → 自动联机"的流程。
    /// </summary>
    public static class LobbyServiceAdapter
    {
        /// <summary>当前所在的房间。</summary>
        public static Lobby CurrentLobby { get; private set; }

        /// <summary>是否是房主。</summary>
        public static bool IsHost => CurrentLobby != null &&
                                     CurrentLobby.HostId == AuthenticationService.Instance.PlayerId;

        private static ILobbyEvents _lobbyEvents;
        private static float _heartbeatTimer;

        /// <summary>房间数据变化事件。</summary>
        public static event Action<Lobby> OnLobbyUpdated;

        /// <summary>离开房间事件。</summary>
        public static event Action OnLobbyLeft;

        /// <summary>
        /// 创建公开房间。
        /// </summary>
        /// <param name="lobbyName">房间名。</param>
        /// <param name="maxPlayers">最大玩家数。</param>
        /// <param name="relayJoinCode">Relay 加入码（存入房间数据）。</param>
        public static async Task<Lobby> CreateLobbyAsync(string lobbyName, int maxPlayers, string relayJoinCode = null)
        {
            var options = new CreateLobbyOptions
            {
                IsPrivate = false,
                Player = new Player
                {
                    Data = new Dictionary<string, PlayerDataObject>
                    {
                        ["PlayerName"] = new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, "Player")
                    }
                },
                Data = new Dictionary<string, DataObject>()
            };

            if (!string.IsNullOrEmpty(relayJoinCode))
            {
                options.Data["RelayJoinCode"] = new DataObject(
                    DataObject.VisibilityOptions.Member, relayJoinCode);
            }

            CurrentLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, options);
            Debug.Log($"[Lobby] 房间创建成功: {CurrentLobby.Name} (Code: {CurrentLobby.LobbyCode})");

            await SubscribeToEventsAsync();
            OnLobbyUpdated?.Invoke(CurrentLobby);
            return CurrentLobby;
        }

        /// <summary>
        /// 通过房间加入码加入。
        /// </summary>
        public static async Task<Lobby> JoinLobbyByCodeAsync(string lobbyCode)
        {
            var options = new JoinLobbyByCodeOptions
            {
                Player = new Player
                {
                    Data = new Dictionary<string, PlayerDataObject>
                    {
                        ["PlayerName"] = new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, "Player")
                    }
                }
            };

            CurrentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode, options);
            Debug.Log($"[Lobby] 加入房间成功: {CurrentLobby.Name}");

            await SubscribeToEventsAsync();
            OnLobbyUpdated?.Invoke(CurrentLobby);
            return CurrentLobby;
        }

        /// <summary>
        /// 查询公开房间列表。
        /// </summary>
        public static async Task<List<Lobby>> QueryLobbiesAsync()
        {
            var options = new QueryLobbiesOptions { Count = 20 };
            QueryResponse response = await LobbyService.Instance.QueryLobbiesAsync(options);
            return response.Results;
        }

        /// <summary>
        /// 从房间数据中读取 Relay 加入码。
        /// </summary>
        public static string GetRelayJoinCode()
        {
            if (CurrentLobby != null && CurrentLobby.Data != null &&
                CurrentLobby.Data.TryGetValue("RelayJoinCode", out var data))
            {
                return data.Value;
            }
            return null;
        }

        /// <summary>
        /// 离开房间。
        /// </summary>
        public static async Task LeaveLobbyAsync()
        {
            if (CurrentLobby == null) return;

            try
            {
                if (_lobbyEvents != null)
                {
                    await _lobbyEvents.UnsubscribeAsync();
                    _lobbyEvents = null;
                }

                if (IsHost)
                {
                    await LobbyService.Instance.DeleteLobbyAsync(CurrentLobby.Id);
                }
                else
                {
                    await LobbyService.Instance.RemovePlayerAsync(
                        CurrentLobby.Id, AuthenticationService.Instance.PlayerId);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Lobby] 离开房间时出错: {e.Message}");
            }
            finally
            {
                CurrentLobby = null;
                OnLobbyLeft?.Invoke();
            }
        }

        /// <summary>
        /// 房主心跳（每 15 秒调用一次，保持房间活跃）。
        /// </summary>
        public static async Task TickHeartbeatAsync()
        {
            if (!IsHost || CurrentLobby == null) return;

            _heartbeatTimer += Time.deltaTime;
            if (_heartbeatTimer >= 15f)
            {
                _heartbeatTimer = 0f;
                try
                {
                    await LobbyService.Instance.SendHeartbeatPingAsync(CurrentLobby.Id);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Lobby] 心跳失败: {e.Message}");
                }
            }
        }

        private static async Task SubscribeToEventsAsync()
        {
            if (CurrentLobby == null) return;

            var callbacks = new LobbyEventCallbacks();
            callbacks.LobbyChanged += OnLobbyChanged;
            callbacks.PlayerJoined += OnPlayerJoined;
            callbacks.PlayerLeft += OnPlayerLeft;

            _lobbyEvents = await LobbyService.Instance.SubscribeToLobbyEventsAsync(CurrentLobby.Id, callbacks);
        }

        private static void OnLobbyChanged(ILobbyChanges changes)
        {
            if (changes.LobbyDeleted)
            {
                CurrentLobby = null;
                OnLobbyLeft?.Invoke();
                return;
            }
            changes.ApplyToLobby(CurrentLobby);
            OnLobbyUpdated?.Invoke(CurrentLobby);
        }

        private static void OnPlayerJoined(List<LobbyPlayerJoined> players)
        {
            foreach (var p in players)
            {
                Debug.Log($"[Lobby] 玩家加入: 索引 {p.PlayerIndex}");
            }
            OnLobbyUpdated?.Invoke(CurrentLobby);
        }

        private static void OnPlayerLeft(List<int> playerIndices)
        {
            foreach (var idx in playerIndices)
            {
                Debug.Log($"[Lobby] 玩家离开: 索引 {idx}");
            }
            OnLobbyUpdated?.Invoke(CurrentLobby);
        }
    }
}
