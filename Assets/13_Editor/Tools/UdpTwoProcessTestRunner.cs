#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace StarPixelSurge.EditorTools
{
    /// <summary>
    /// 真实 UDP 多进程联机测试的编辑器入口。负责：构建一个 Player 可执行文件、
    /// 以房主与若干客户端身份启动多个进程（真实 UDP 套接字跨进程通信）、
    /// 收集各进程写入的结果文件并汇总判定。
    ///
    /// 可在菜单 StarPixelSurge/Network 下直接运行，也可通过 batchmode 的
    /// -executeMethod 在 CI 中执行，并以退出码反映结果。
    ///
    /// 断言流程：创建房间 → 客户端加入 → 全员准备 → 开局 → 战斗采样
    /// （快照推进与实体复制） → 房主退出 → 一名客户端当选新房主、其余客户端
    /// 跟随新房主并继续收到快照。后者是“UDP 房主迁移端点修复”的端到端验证：
    /// 需要至少 3 个进程（房主 + 2 客户端）才会真正触发端点重新发现。
    /// </summary>
    public static class UdpTwoProcessTestRunner
    {
        private const string ScenePath = "Assets/Scenes/Playtest.unity";
        private const string PlayerName = "StarPixelSurgePlaytest";

        private sealed class Options
        {
            public int Port = 7799;
            public string Room = "PLAYTEST";
            public string ClassId = "thunder_mage";
            public int ClientCount = 1;
            public float Duration = 20f;
            public float MigrationWait = 10f;
            public float ProcessTimeout = 180f;
            public float Latency;
            public float Jitter;
            public float LossRate;
            public float DuplicateRate;
            public bool Windowed;
            public bool SkipBuild;
        }

        private sealed class ProcessResult
        {
            public int PlayerId;
            public string Role;
            public bool FileFound;
            public bool Passed;
            public int PassedCount;
            public int Total;
            public int ExitCode;
            public string ResultPath;
            public string LogPath;
            public readonly List<string> Lines = new List<string>();
        }

        private sealed class LaunchHandle
        {
            public Process Process;
            public ProcessResult Result;
        }

        [MenuItem("StarPixelSurge/Network/Run UDP Two-Process Test (Headless)")]
        public static void RunHeadless()
        {
            Run(new Options());
        }

        [MenuItem("StarPixelSurge/Network/Run UDP Two-Process Test (Windowed)")]
        public static void RunWindowed()
        {
            Run(new Options { Windowed = true });
        }

        [MenuItem("StarPixelSurge/Network/Run UDP Migration Test (3 Process, Headless)")]
        public static void RunMigrationHeadless()
        {
            Run(new Options { ClientCount = 2, Duration = 12f });
        }

        [MenuItem("StarPixelSurge/Network/Run UDP Weak-Network Migration Test (Headless)")]
        public static void RunWeakNetworkHeadless()
        {
            Run(new Options { ClientCount = 2, Duration = 12f, Latency = 0.08f, Jitter = 0.03f, LossRate = 0.05f });
        }

        /// <summary>
        /// batchmode 入口，用法：
        /// Unity -batchmode -quit -projectPath ... -executeMethod
        /// StarPixelSurge.EditorTools.UdpTwoProcessTestRunner.RunFromCommandLine
        /// [-playtestPort N] [-playtestClients N] [-playtestDuration S] [-playtestMigration S]
        /// [-playtestLatency S] [-playtestJitter S] [-playtestLossRate F] [-playtestDuplicateRate F]
        /// [-playtestWindowed] [-playtestSkipBuild]
        /// </summary>
        public static void RunFromCommandLine()
        {
            var options = ParseOptions(Environment.GetCommandLineArgs());
            var passed = Run(options);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        private static bool Run(Options options)
        {
            Debug.Log($"[UdpPlaytest] 开始：端口={options.Port} 房间={options.Room} 客户端数={options.ClientCount} " +
                      $"时长={options.Duration}s 迁移等待={options.MigrationWait}s " +
                      $"弱网(延迟={options.Latency} 抖动={options.Jitter} 丢包={options.LossRate} 重复={options.DuplicateRate}) 窗口模式={options.Windowed}");

            string exePath;
            if (options.SkipBuild)
            {
                exePath = ResolveExistingBuild();
                if (exePath == null)
                {
                    Debug.LogError("[UdpPlaytest] 未找到既有构建，且已指定 -playtestSkipBuild。请先执行一次构建。");
                    return false;
                }
            }
            else
            {
                exePath = BuildPlayer();
                if (exePath == null)
                {
                    return false;
                }
            }

            var runDir = Path.Combine(Path.GetTempPath(), $"sps_udp_{DateTime.Now:yyyyMMdd_HHmmss}");
            Directory.CreateDirectory(runDir);

            var expectedPlayers = options.ClientCount + 1;
            var handles = new List<LaunchHandle>();

            var host = Launch(exePath, options, "host", 0, expectedPlayers, false, false, runDir);
            if (host == null)
            {
                return false;
            }

            handles.Add(host);

            // 让房主先完成套接字绑定，降低首个加入请求的丢失概率。
            System.Threading.Thread.Sleep(2500);

            for (var i = 1; i <= options.ClientCount; i++)
            {
                // 加入顺序最小的客户端（id=1）预期当选新房主，其余客户端预期跟随它。
                var client = Launch(exePath, options, "client", i, expectedPlayers, i == 1, i > 1, runDir);
                if (client == null)
                {
                    KillAll(handles);
                    return false;
                }

                handles.Add(client);
                System.Threading.Thread.Sleep(500);
            }

            // 等待全部进程退出或超时，再统一读取结果文件。
            for (var i = 0; i < handles.Count; i++)
            {
                var exited = WaitForExit(handles[i].Process, options.ProcessTimeout);
                ReadResult(handles[i].Result, handles[i].Process, exited);
            }

            var passed = true;
            for (var i = 0; i < handles.Count; i++)
            {
                passed &= handles[i].Result.Passed;
            }

            var results = new List<ProcessResult>(handles.Count);
            for (var i = 0; i < handles.Count; i++)
            {
                results.Add(handles[i].Result);
            }

            Report(results, passed);
            Debug.Log($"[UdpPlaytest] 运行目录：{runDir}");
            return passed;
        }

        private static string BuildPlayer()
        {
            var scenePath = EnsureScene();
            if (scenePath == null)
            {
                return null;
            }

            var buildDir = Path.Combine(Path.GetTempPath(), "sps_udp_build");
            Directory.CreateDirectory(buildDir);

            var target = EditorUserBuildSettings.activeBuildTarget;
            var exeName = target == BuildTarget.StandaloneWindows64 || target == BuildTarget.StandaloneWindows
                ? PlayerName + ".exe"
                : PlayerName;
            var exePath = Path.Combine(buildDir, exeName);

            var buildOptions = new BuildPlayerOptions
            {
                scenes = new[] { scenePath },
                locationPathName = exePath,
                target = target,
                options = BuildOptions.Development
            };

            Debug.Log($"[UdpPlaytest] 构建 Player：target={target} -> {exePath}");
            var report = BuildPipeline.BuildPlayer(buildOptions);
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[UdpPlaytest] 构建失败：{report.summary.result} 错误数={report.summary.totalErrors}");
                return null;
            }

            Debug.Log($"[UdpPlaytest] 构建完成：耗时 {report.summary.totalTime.TotalSeconds:F1}s，大小 {report.summary.totalSize} 字节");
            return exePath;
        }

        /// <summary>确保存在一个供构建使用的空场景（AutoBootstrap 会在运行期自建世界）。</summary>
        private static string EnsureScene()
        {
            if (File.Exists(ScenePath))
            {
                return ScenePath;
            }

            var directory = Path.GetDirectoryName(ScenePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                AssetDatabase.Refresh();
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var saved = EditorSceneManager.SaveScene(scene, ScenePath);
            EditorSceneManager.CloseScene(scene, true);
            if (!saved)
            {
                Debug.LogError($"[UdpPlaytest] 场景保存失败：{ScenePath}");
                return null;
            }

            AssetDatabase.Refresh();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log($"[UdpPlaytest] 已创建并登记构建场景：{ScenePath}");
            return ScenePath;
        }

        private static LaunchHandle Launch(string exePath, Options options, string role, int playerId, int expectedPlayers, bool expectMigration, bool expectFollow, string runDir)
        {
            var resultPath = Path.Combine(runDir, $"{role}{playerId}_result.txt");
            var logPath = Path.Combine(runDir, $"{role}{playerId}.log");

            var args = new StringBuilder();
            args.Append("-batchmode ");
            if (!options.Windowed)
            {
                args.Append("-nographics ");
            }

            var culture = System.Globalization.CultureInfo.InvariantCulture;
            args.Append($"-netRole {role} ");
            args.Append($"-netPlayerId {playerId} ");
            args.Append($"-netExpectedPlayers {expectedPlayers} ");
            args.Append($"-netPort {options.Port} ");
            args.Append("-netHost 127.0.0.1 ");
            args.Append($"-netRoom {options.Room} ");
            args.Append($"-netClass {options.ClassId} ");
            args.Append($"-netDuration {options.Duration.ToString(culture)} ");
            args.Append($"-netLatency {options.Latency.ToString(culture)} ");
            args.Append($"-netJitter {options.Jitter.ToString(culture)} ");
            args.Append($"-netLossRate {options.LossRate.ToString(culture)} ");
            args.Append($"-netDuplicateRate {options.DuplicateRate.ToString(culture)} ");

            if (expectMigration || expectFollow)
            {
                args.Append($"-netWaitMigration {options.MigrationWait.ToString(culture)} ");
            }

            if (expectMigration)
            {
                args.Append("-netExpectMigration ");
            }

            if (expectFollow)
            {
                args.Append("-netExpectFollow ");
            }

            args.Append($"-netResult \"{resultPath}\"");
            args.Append($" -logFile \"{logPath}\"");

            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = args.ToString(),
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(exePath) ?? runDir
            };

            try
            {
                var process = Process.Start(startInfo);
                Debug.Log($"[UdpPlaytest] 已启动 {role}#{playerId}，PID={process?.Id}，参数：{args}");
                return new LaunchHandle
                {
                    Process = process,
                    Result = new ProcessResult { Role = role, PlayerId = playerId, ResultPath = resultPath, LogPath = logPath }
                };
            }
            catch (Exception e)
            {
                Debug.LogError($"[UdpPlaytest] 启动 {role}#{playerId} 失败：{e.Message}");
                return null;
            }
        }

        private static bool WaitForExit(Process process, float timeoutSeconds)
        {
            try
            {
                if (process.WaitForExit((int)(timeoutSeconds * 1000f)))
                {
                    return true;
                }

                Debug.LogWarning($"[UdpPlaytest] 进程 {process.Id} 超时 {timeoutSeconds:F0}s，强制结束");
                TryKill(process);
                process.WaitForExit(5000);
            }
            catch (Exception e)
            {
                Debug.LogError($"[UdpPlaytest] 等待进程退出异常：{e.Message}");
            }

            return false;
        }

        private static void KillAll(List<LaunchHandle> handles)
        {
            for (var i = 0; i < handles.Count; i++)
            {
                TryKill(handles[i].Process);
            }
        }

        private static void TryKill(Process process)
        {
            try
            {
                if (process != null && !process.HasExited)
                {
                    process.Kill();
                }
            }
            catch (Exception)
            {
                // 进程可能已自然退出，忽略。
            }
        }

        private static void ReadResult(ProcessResult result, Process process, bool exited)
        {
            result.ExitCode = TryExitCode(process);

            if (string.IsNullOrEmpty(result.ResultPath) || !File.Exists(result.ResultPath))
            {
                return;
            }

            result.FileFound = true;
            foreach (var line in File.ReadAllLines(result.ResultPath))
            {
                result.Lines.Add(line);
                if (line.StartsWith("STATUS="))
                {
                    result.Passed = line.Substring("STATUS=".Length).Trim() == "PASS";
                }
                else if (line.StartsWith("PASSED="))
                {
                    int.TryParse(line.Substring("PASSED=".Length), out result.PassedCount);
                }
                else if (line.StartsWith("TOTAL="))
                {
                    int.TryParse(line.Substring("TOTAL=".Length), out result.Total);
                }
            }

            if (!exited)
            {
                result.Passed = false;
            }
        }

        private static int TryExitCode(Process process)
        {
            try
            {
                return process != null && process.HasExited ? process.ExitCode : -1;
            }
            catch (Exception)
            {
                return -1;
            }
        }

        private static void Report(List<ProcessResult> results, bool passed)
        {
            var builder = new StringBuilder();
            builder.Append($"[UdpPlaytest] {(passed ? "全部通过" : "存在失败")}\n");
            for (var i = 0; i < results.Count; i++)
            {
                AppendProcess(builder, results[i]);
            }

            if (passed)
            {
                Debug.Log(builder.ToString());
            }
            else
            {
                Debug.LogError(builder.ToString());
            }
        }

        private static void AppendProcess(StringBuilder builder, ProcessResult result)
        {
            builder.Append($"  {result.Role}#{result.PlayerId}: {(result.FileFound ? "有结果" : "缺少结果文件")}, ")
                .Append($"{(result.Passed ? "PASS" : "FAIL")}, ")
                .Append($"{result.PassedCount}/{result.Total}, exit={result.ExitCode}\n");

            for (var i = 0; i < result.Lines.Count; i++)
            {
                if (result.Lines[i].StartsWith("CHECK|"))
                {
                    builder.Append("    ").Append(result.Lines[i].Replace('|', ' ')).Append('\n');
                }
            }

            if (!result.FileFound)
            {
                builder.Append($"    (日志：{result.LogPath})\n");
            }
        }

        private static string ResolveExistingBuild()
        {
            var buildDir = Path.Combine(Path.GetTempPath(), "sps_udp_build");
            if (!Directory.Exists(buildDir))
            {
                return null;
            }

            var candidates = Directory.GetFiles(buildDir, PlayerName + "*");
            for (var i = 0; i < candidates.Length; i++)
            {
                if (!candidates[i].EndsWith(".pdb") && !candidates[i].EndsWith(".txt"))
                {
                    return candidates[i];
                }
            }

            return null;
        }

        private static Options ParseOptions(string[] args)
        {
            var options = new Options();
            if (args == null)
            {
                return options;
            }

            for (var i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-playtestPort":
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out var port))
                        {
                            options.Port = Mathf.Clamp(port, 1025, 65500);
                            i++;
                        }

                        break;
                    case "-playtestClients":
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out var clients))
                        {
                            options.ClientCount = Mathf.Clamp(clients, 1, 3);
                            i++;
                        }

                        break;
                    case "-playtestDuration":
                        if (i + 1 < args.Length && float.TryParse(args[i + 1], out var duration))
                        {
                            options.Duration = Mathf.Max(3f, duration);
                            i++;
                        }

                        break;
                    case "-playtestMigration":
                        if (i + 1 < args.Length && float.TryParse(args[i + 1], out var migration))
                        {
                            options.MigrationWait = Mathf.Max(3f, migration);
                            i++;
                        }

                        break;
                    case "-playtestLatency":
                        if (i + 1 < args.Length && float.TryParse(args[i + 1], out var latency))
                        {
                            options.Latency = Mathf.Max(0f, latency);
                            i++;
                        }

                        break;
                    case "-playtestJitter":
                        if (i + 1 < args.Length && float.TryParse(args[i + 1], out var jitter))
                        {
                            options.Jitter = Mathf.Max(0f, jitter);
                            i++;
                        }

                        break;
                    case "-playtestLossRate":
                        if (i + 1 < args.Length && float.TryParse(args[i + 1], out var loss))
                        {
                            options.LossRate = Mathf.Clamp01(loss);
                            i++;
                        }

                        break;
                    case "-playtestDuplicateRate":
                        if (i + 1 < args.Length && float.TryParse(args[i + 1], out var duplicate))
                        {
                            options.DuplicateRate = Mathf.Clamp01(duplicate);
                            i++;
                        }

                        break;
                    case "-playtestWindowed":
                        options.Windowed = true;
                        break;
                    case "-playtestSkipBuild":
                        options.SkipBuild = true;
                        break;
                }
            }

            return options;
        }
    }
}
#endif
