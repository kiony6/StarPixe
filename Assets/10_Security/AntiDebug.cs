using UnityEngine;

namespace StarPixelSurge.Security
{
    /// <summary>检测到调试器时执行的处理动作。</summary>
    public enum AntiDebugResponse
    {
        None = 0,
        Log = 1,
        Terminate = 2
    }

    /// <summary>
    /// 反调试检测。仅供发布版本启用；编辑器与 Development Build 下不视为调试器，
    /// 避免影响正常开发（需求 13.3）。
    /// </summary>
    public static class AntiDebug
    {
        /// <summary>检测是否有调试器附加。</summary>
        public static bool DetectDebugger()
        {
#if UNITY_EDITOR
            return false;
#else
            if (Debug.isDebugBuild)
            {
                return false;
            }

            return System.Diagnostics.Debugger.IsAttached;
#endif
        }

        /// <summary>按配置执行保护响应，返回是否触发了动作。</summary>
        public static bool ApplyResponse(AntiDebugResponse response)
        {
            if (response == AntiDebugResponse.None)
            {
                return false;
            }

            Debug.LogWarning("[AntiDebug] Debugger attachment detected.");

            if (response == AntiDebugResponse.Terminate)
            {
                Application.Quit(0);
            }

            return true;
        }
    }
}
