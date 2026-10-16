#if UNITY_EDITOR
using StarPixelSurge.Network.Simulation;
using UnityEditor;
using UnityEngine;

namespace StarPixelSurge.EditorTools
{
    /// <summary>
    /// 本地多实例确定性仿真的编辑器入口。可在编辑器菜单直接运行，也可通过
    /// batchmode 的 -executeMethod 在 CI 中执行，并以退出码反映断言结果。
    /// </summary>
    public static class NetworkSimulationRunner
    {
        [MenuItem("StarPixelSurge/Network/Run Simulation (Default)")]
        public static void RunDefault()
        {
            RunAndLog(new NetworkSimulationOptions());
        }

        [MenuItem("StarPixelSurge/Network/Run Simulation (Deterministic Zero-Delay)")]
        public static void RunDeterministic()
        {
            RunAndLog(new NetworkSimulationOptions
            {
                Latency = 0f,
                InterpolationDelayFrames = 0,
                KillHostAtEnd = true
            });
        }

        [MenuItem("StarPixelSurge/Network/Run Simulation (Lossy Network)")]
        public static void RunLossy()
        {
            RunAndLog(new NetworkSimulationOptions
            {
                PlayerCount = 4,
                Latency = 0.12f,
                Jitter = 0.03f,
                LossRate = 0.05f
            });
        }

        private static void RunAndLog(NetworkSimulationOptions options)
        {
            var report = new NetworkSimulation(options).Run();
            if (report.AllPassed)
            {
                Debug.Log(report.BuildSummary());
            }
            else
            {
                Debug.LogError(report.BuildSummary());
            }
        }

        /// <summary>
        /// batchmode 入口，用法：
        /// Unity -batchmode -quit -projectPath ... -executeMethod
        /// StarPixelSurge.EditorTools.NetworkSimulationRunner.RunFromCommandLine [-simLossy] [-simPlayers N] [-simFrames N] [-simLatency S]
        /// </summary>
        public static void RunFromCommandLine()
        {
            var options = ParseOptions(System.Environment.GetCommandLineArgs());
            var report = new NetworkSimulation(options).Run();
            var summary = report.BuildSummary();

            if (report.AllPassed)
            {
                Debug.Log(summary);
            }
            else
            {
                Debug.LogError(summary);
            }

            EditorApplication.Exit(report.AllPassed ? 0 : 1);
        }

        private static NetworkSimulationOptions ParseOptions(string[] args)
        {
            var options = new NetworkSimulationOptions();
            if (args == null)
            {
                return options;
            }

            for (var i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-simLossy":
                        options.PlayerCount = 4;
                        options.Latency = 0.12f;
                        options.Jitter = 0.03f;
                        options.LossRate = 0.05f;
                        break;
                    case "-simDeterministic":
                        options.Latency = 0f;
                        options.InterpolationDelayFrames = 0;
                        break;
                    case "-simPlayers":
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out var players))
                        {
                            options.PlayerCount = Mathf.Clamp(players, 2, 4);
                            i++;
                        }

                        break;
                    case "-simFrames":
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out var frames))
                        {
                            options.RunFrames = Mathf.Max(30, frames);
                            i++;
                        }

                        break;
                    case "-simLatency":
                        if (i + 1 < args.Length && float.TryParse(args[i + 1], out var latency))
                        {
                            options.Latency = Mathf.Max(0f, latency);
                            i++;
                        }

                        break;
                }
            }

            return options;
        }
    }
}
#endif
