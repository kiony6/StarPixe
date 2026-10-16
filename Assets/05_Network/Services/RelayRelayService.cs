using System;
using System.Threading.Tasks;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace StarPixelSurge.Network.Services
{
    /// <summary>
    /// Unity Relay 服务封装：主机创建分配并生成加入码，客户端凭码加入。
    /// 作为互联网联机的底层传输通道，替代局域网直连 UDP。
    /// </summary>
    public static class RelayRelayService
    {
        private static bool _initialized;

        /// <summary>当前主机分配的 Relay 数据。</summary>
        public static Allocation HostAllocation { get; private set; }

        /// <summary>当前客户端加入的 Relay 数据。</summary>
        public static JoinAllocation ClientAllocation { get; private set; }

        /// <summary>当前房间的 Relay 加入码。</summary>
        public static string JoinCode { get; private set; }

        /// <summary>是否已连接到 Relay。</summary>
        public static bool IsConnected => HostAllocation != null || ClientAllocation != null;

        /// <summary>
        /// 初始化 Unity Services 并匿名登录。必须在任何 Relay 操作前调用。
        /// </summary>
        public static async Task InitializeAsync()
        {
            if (_initialized) return;

            await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            _initialized = true;
            Debug.Log($"[Relay] 初始化完成，PlayerId = {AuthenticationService.Instance.PlayerId}");
        }

        /// <summary>
        /// 主机：创建 Relay 分配。
        /// </summary>
        /// <param name="maxPlayers">最大玩家数（含主机）。</param>
        /// <returns>加入码，用于分享给其他玩家。</returns>
        public static async Task<string> CreateAllocationAsync(int maxPlayers = 8)
        {
            await InitializeAsync();

            HostAllocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers);
            JoinCode = await RelayService.Instance.GetJoinCodeAsync(HostAllocation.AllocationId);

            Debug.Log($"[Relay] 主机分配创建成功，JoinCode = {JoinCode}");
            return JoinCode;
        }

        /// <summary>
        /// 客户端：凭加入码加入 Relay 分配。
        /// </summary>
        public static async Task JoinAllocationAsync(string joinCode)
        {
            await InitializeAsync();

            ClientAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            JoinCode = joinCode;

            Debug.Log($"[Relay] 客户端加入成功，AllocationId = {ClientAllocation.AllocationId}");
        }

        /// <summary>
        /// 释放当前 Relay 连接。
        /// </summary>
        public static void Disconnect()
        {
            HostAllocation = null;
            ClientAllocation = null;
            JoinCode = null;
            Debug.Log("[Relay] 已断开");
        }

        /// <summary>
        /// 获取主机的连接数据（供 UnityTransport 使用）。
        /// </summary>
        public static RelayServerData GetHostRelayData()
        {
            if (HostAllocation == null)
                throw new InvalidOperationException("主机分配不存在");

            return HostAllocation.ToRelayServerData("dtls");
        }

        /// <summary>
        /// 获取客户端的连接数据（供 UnityTransport 使用）。
        /// </summary>
        public static RelayServerData GetClientRelayData()
        {
            if (ClientAllocation == null)
                throw new InvalidOperationException("客户端分配不存在");

            return ClientAllocation.ToRelayServerData("dtls");
        }
    }
}
