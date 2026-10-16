using System;
using System.Threading.Tasks;
using Unity.Services.Vivox;
using UnityEngine;

namespace StarPixelSurge.Network.Services
{
    /// <summary>
    /// Vivox 小队语音封装：初始化、登录、加入房间语音频道、静音控制。
    /// 频道名使用 Lobby ID，同房间玩家自动进入同一语音频道。
    /// </summary>
    public static class VivoxVoiceAdapter
    {
        private static bool _initialized;

        /// <summary>Vivox 是否已初始化。</summary>
        public static bool IsInitialized =>
            VivoxService.Instance.InitializationState == VivoxInitializationState.Initialized;

        /// <summary>是否已登录。</summary>
        public static bool IsLoggedIn => VivoxService.Instance.IsLoggedIn;

        /// <summary>当前语音频道名。</summary>
        public static string CurrentChannel { get; private set; }

        /// <summary>麦克风是否静音。</summary>
        public static bool IsMuted { get; private set; }

        /// <summary>频道加入事件。</summary>
        public static event Action<string> OnChannelJoined;

        /// <summary>频道离开事件。</summary>
        public static event Action<string> OnChannelLeft;

        /// <summary>
        /// 初始化 Vivox 服务。需在 Unity Services 登录后调用。
        /// </summary>
        public static async Task InitializeAsync()
        {
            if (_initialized) return;

            await VivoxService.Instance.InitializeAsync();
            Debug.Log("[Vivox] 初始化成功");

            VivoxService.Instance.ChannelJoined += channel =>
            {
                Debug.Log($"[Vivox] 频道已加入: {channel}");
                OnChannelJoined?.Invoke(channel);
            };
            VivoxService.Instance.ChannelLeft += channel =>
            {
                Debug.Log($"[Vivox] 频道已离开: {channel}");
                OnChannelLeft?.Invoke(channel);
            };

            _initialized = true;
        }

        /// <summary>
        /// 登录 Vivox（自动使用 Unity Authentication 的 PlayerId）。
        /// </summary>
        public static async Task LoginAsync()
        {
            if (!IsInitialized) await InitializeAsync();
            if (IsLoggedIn) return;

            var options = new LoginOptions
            {
                DisplayName = $"Player_{Unity.Services.Authentication.AuthenticationService.Instance.PlayerId[..6]}"
            };
            await VivoxService.Instance.LoginAsync(options);
            Debug.Log("[Vivox] 登录成功");
        }

        /// <summary>
        /// 加入语音频道。
        /// </summary>
        /// <param name="channelName">频道名，通常用 Lobby ID。</param>
        public static async Task JoinVoiceChannelAsync(string channelName)
        {
            if (!IsLoggedIn) await LoginAsync();
            if (string.IsNullOrEmpty(channelName)) return;

            await VivoxService.Instance.JoinGroupChannelAsync(
                channelName, ChatCapability.AudioOnly);
            CurrentChannel = channelName;
        }

        /// <summary>
        /// 离开当前语音频道。
        /// </summary>
        public static async Task LeaveVoiceChannelAsync()
        {
            if (string.IsNullOrEmpty(CurrentChannel)) return;

            await VivoxService.Instance.LeaveChannelAsync(CurrentChannel);
            CurrentChannel = null;
        }

        /// <summary>
        /// 切换麦克风静音。
        /// </summary>
        public static void ToggleMute()
        {
            IsMuted = !IsMuted;
            if (IsMuted)
                VivoxService.Instance.MuteInputDevice();
            else
                VivoxService.Instance.UnmuteInputDevice();
        }

        /// <summary>
        /// 登出并清理。
        /// </summary>
        public static async Task LogoutAsync()
        {
            await LeaveVoiceChannelAsync();
            if (IsLoggedIn)
            {
                await VivoxService.Instance.LogoutAsync();
            }
            CurrentChannel = null;
            IsMuted = false;
        }
    }
}
