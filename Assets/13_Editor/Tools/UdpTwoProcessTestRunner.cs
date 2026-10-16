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
    /// 真实 UDP 双进程联机测试的编辑器入口。负责：构建一个 Player 可执行文件、
    /// 分别以房主与客户端身份启动两个进程（真实 UDP 套接字跨进程通信）、
    /// 收集二者写入的结果文件并汇总判定。
    ///
    /// 可在菜单 StarPixelSurge/Network 下直接运行，也可通过 batchmode 的
    /// -executeMethod 在 CI 中执行，并以退出码反映结果。
    ///
    /// 运行流程断言：创建房间 → 客户端加入 → 全员准备 → 开局 → 战斗采样
    /// （快照推进与实体复制） → 房主退出 → 客户端完成房主迁移并恢复广播。
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
            public float Duration = 20f;
            public float MigrationWait = 10f;
            public float ProcessTimeout = 150f;
            public bool Windowed;
            public bool SkipBuild;
        }

        private sealed class ProcessResult
        {
            public string Role;
            public bool FileFound;
            public bool Passed;
            public int PassedCount;
            public int Total;
            public int ExitCode;
            public string LogPath;
            public readonly List<string> Lines = new List<string>();
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

        /// <summary>
        /// batchmode 入口，用法：
        /// Unity -batchmode -quit -projectPath ... -executeMethod
        /// StarPixelSurge.EditorTools.UdpTwoProcessTestRunner.RunFromCommandLine
        /// [-playtestPort N] [-playtestDuration S] [-playtestMigration S]
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
            Debug.Log($"[UdpPlaytest] 开始：端口={options.Port} 房间={options.Room} 时长={options.Duration}s 迁移等待={options.MigrationWait}s 窗口模式={options.Windowed}");

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
            var hostResult = Path.Combine(runDir, "host_result.txt");
            var clientResult = Path.Combine(runDir, "client_result.txt");
            var hostLog = Path.Combine(runDir, "host.log");
            var clientLog = Path.Combine(runDir, "client.log");

            var host = Launch(exePath, options, "host", hostResult, hostLog, runDir, false);
            if (host == null)
            {
                return false;
            }

            // 让房主先完成套接字绑定，降低首个加入请求的丢失概率。
            System.Threading.Thread.Sleep(2500);

            var client = Launch(exePath, options, "client", clientResult, clientLog, runDir, true);
            if (client == null)
            {
                TryKill(host);
                return false;
            }

            var hostDone = WaitForExit(host, options.ProcessTimeout);
            var clientDone = WaitForExit(client, options.ProcessTimeout);
            var hostResultData = ReadResult(hostResult, hostLog, "host", host, hostDone);
            var clientResultData = ReadResult(clientResult, clientLog, "client", client, clientDone);

            var passed = hostResultData.Passed && clientResultData.Passed;
            Report(hostResultData, clientResultData, passed);
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
            if (Directory.Exists(buildDir))
            {
                Directory.CreateDirectory(buildDir);
            }

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

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
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

        private static Process Launch(string exePath, Options options, string role, string resultPath, string logPath, string runDir, bool expectMigration)
        {
            var args = new StringBuilder();
            args.Append("-batchmode ");
            if (!options.Windowed)
            {
                args.Append("-nographics ");
            }

            args.Append($"-netRole {role} ");
            args.Append($"-netPort {options.Port} ");
            args.Append($"-netHost 127.0.0.1 ");
            args.Append($"-netRoom {options.Room} ");
            args.Append($"-netClass {options.ClassId} ");
            args.Append($"-netDuration {options.Duration.ToString(System.Globalization.CultureInfo.InvariantCulture)} ");
            if (expectMigration)
            {
                args.Append("-netExpectMigration ");
                args.Append($"-netWaitMigration {options.MigrationWait.ToString(System.Globalization.CultureInfo.InvariantCulture)} ");
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
                Debug.Log($"[UdpPlaytest] 已启动 {role}，PID={process?.Id}，参数：{args}");
                return process;
            }
            catch (Exception e)
            {
                Debug.LogError($"[UdpPlaytest] 启动 {role} 失败：{e.Message}");
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

        private static ProcessResult ReadResult(string resultPath, string logPath, string role, Process process, bool exited)
        {
            var result = new ProcessResult
            {
                Role = role,
                LogPath = logPath,
                ExitCode = TryExitCode(process)
            };

            if (!File.Exists(resultPath))
            {
                return result;
            }

            result.FileFound = true;
            foreach (var line in File.ReadAllLines(resultPath))
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

            return result;
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

        private static void Report(ProcessResult host, ProcessResult client, bool passed)
        {
            var builder = new StringBuilder();
            builder.Append($"[UdpPlaytest] {(passed ? "全部通过" : "存在失败")}\n");
            AppendProcess(builder, host);
            AppendProcess(builder, client);

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
            builder.Append($"  {result.Role}: {(result.FileFound ? "有结果" : "缺少结果文件")}, ")
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
