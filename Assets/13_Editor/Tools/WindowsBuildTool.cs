#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace StarPixelSurge.EditorTools
{
    /// <summary>
    /// Windows 独立包构建入口（需求 19.1）。
    /// 生成 <c>Builds/Windows/StarPixelSurge/StarPixelSurge.exe</c> 与其数据目录，
    /// 可直接双击运行，无需 Unity 编辑器。
    ///
    /// 编辑器内：菜单 <c>StarPixelSurge/Build/Build Windows Player</c>。
    /// 命令行：
    /// Unity -batchmode -quit -nographics -projectPath &lt;工程&gt; -logFile build.log
    ///   -executeMethod StarPixelSurge.EditorTools.WindowsBuildTool.BuildFromCommandLine
    ///   [-buildDev] [-buildPath &lt;相对或绝对路径&gt;]
    /// </summary>
    public static class WindowsBuildTool
    {
        private const string DefaultOutputDir = "Builds/Windows/StarPixelSurge";
        private const string ProductName = "StarPixelSurge";
        private const string PlayScenePath = "Assets/Scenes/Playtest.unity";
        private const string PlaySceneDir = "Assets/Scenes";

        [MenuItem("StarPixelSurge/Build/Build Windows Player")]
        public static void BuildWindowsPlayer()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var ok = Build(DefaultOutputDir, false, out var summary);
            if (ok)
            {
                Debug.Log(summary);
            }
            else
            {
                Debug.LogError(summary);
            }
        }

        /// <summary>batchmode 入口：构建失败时以非零码退出。</summary>
        public static void BuildFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            var dev = args.Contains("-buildDev");
            var outputDir = ReadArg(args, "-buildPath") ?? DefaultOutputDir;

            var ok = Build(outputDir, dev, out var summary);
            if (ok)
            {
                Debug.Log(summary);
            }
            else
            {
                Debug.LogError(summary);
            }

            EditorApplication.Exit(ok ? 0 : 1);
        }

        private static bool Build(string outputDir, bool development, out string summary)
        {
            var scenePath = EnsureScene();
            if (scenePath == null)
            {
                summary = "[BuildWindows] 无法准备构建场景。";
                return false;
            }

            var fullOutputDir = Path.IsPathRooted(outputDir)
                ? outputDir
                : Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", outputDir);
            Directory.CreateDirectory(fullOutputDir);

            var exePath = Path.Combine(fullOutputDir, ProductName + ".exe");
            var target = BuildTarget.StandaloneWindows64;

            var options = new BuildPlayerOptions
            {
                scenes = new[] { scenePath },
                locationPathName = exePath,
                target = target,
                options = development ? BuildOptions.Development : BuildOptions.None
            };

            Debug.Log($"[BuildWindows] 开始构建 target={target} development={development} -> {exePath}");
            var report = BuildPipeline.BuildPlayer(options);
            var result = report.summary;

            if (result.result != BuildResult.Succeeded)
            {
                summary = $"[BuildWindows] 构建失败：{result.result} 错误数={result.totalErrors}";
                return false;
            }

            summary = $"[BuildWindows] 构建成功：{exePath} 耗时={result.totalTime.TotalSeconds:F1}s 大小={result.totalSize} 字节";
            return true;
        }

        /// <summary>确保存在一个供构建使用的场景（AutoBootstrap 在运行期自建世界）。</summary>
        private static string EnsureScene()
        {
            if (File.Exists(PlayScenePath))
            {
                AddSceneToBuildSettings(PlayScenePath);
                return PlayScenePath;
            }

            if (!Directory.Exists(PlaySceneDir))
            {
                Directory.CreateDirectory(PlaySceneDir);
                AssetDatabase.Refresh();
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var saved = EditorSceneManager.SaveScene(scene, PlayScenePath);
            EditorSceneManager.CloseScene(scene, true);
            if (!saved)
            {
                Debug.LogError($"[BuildWindows] 场景保存失败：{PlayScenePath}");
                return null;
            }

            AddSceneToBuildSettings(PlayScenePath);
            Debug.Log($"[BuildWindows] 已创建并登记构建场景：{PlayScenePath}");
            return PlayScenePath;
        }

        private static void AddSceneToBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes;
            if (scenes.Any(s => s.path == path))
            {
                return;
            }

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(path, true) };
        }

        private static string ReadArg(string[] args, string name)
        {
            for (var i = 0; i < args.Length; i++)
            {
                if (args[i] == name && i + 1 < args.Length)
                {
                    return args[i + 1];
                }
            }

            return null;
        }
    }
}
#endif
