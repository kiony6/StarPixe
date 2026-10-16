#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace StarPixelSurge.EditorTools
{
    /// <summary>
    /// 脚本编译校验入口。在编辑器内报告当前编译状态、参与编译的 Player 程序集、
    /// 各程序集源文件数量与其输出 DLL 是否存在，用于快速定位编译/命名空间/序列化问题。
    ///
    /// 更权威的校验方式是 batchmode：Unity 会先编译全部脚本，编译失败则根本不会执行
    /// -executeMethod 并以非零码退出，因此下面的命令既能编译校验又能拿到确定退出码：
    ///
    /// Unity -batchmode -quit -nographics -projectPath &lt;工程&gt; -logFile compile.log
    ///   -executeMethod StarPixelSurge.EditorTools.ScriptCompileVerifier.VerifyFromCommandLine
    /// </summary>
    public static class ScriptCompileVerifier
    {
        [MenuItem("StarPixelSurge/Verify/Check Script Compilation")]
        public static void Check()
        {
            var ok = Verify(out var summary);
            if (ok)
            {
                Debug.Log(summary);
            }
            else
            {
                Debug.LogError(summary);
            }
        }

        /// <summary>batchmode 入口：以退出码反映编译校验结果。</summary>
        public static void VerifyFromCommandLine()
        {
            var ok = Verify(out var summary);
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

        private static bool Verify(out string summary)
        {
            var builder = new StringBuilder();
            var failed = EditorUtility.scriptCompilationFailed;
            builder.Append($"[CompileVerify] scriptCompilationFailed={failed}\n");

            var assemblies = CompilationPipeline.GetAssemblies(AssembliesType.Player);
            var missing = 0;
            var sources = 0;
            for (var i = 0; i < assemblies.Length; i++)
            {
                var assembly = assemblies[i];
                var fileCount = assembly.sourceFiles != null ? assembly.sourceFiles.Length : 0;
                sources += fileCount;

                var outputExists = !string.IsNullOrEmpty(assembly.outputPath) && File.Exists(assembly.outputPath);
                if (!outputExists)
                {
                    missing++;
                }

                builder.Append($"  {assembly.name}: {fileCount} sources, output={(outputExists ? "ok" : "MISSING")}\n");
            }

            builder.Append($"  assemblies={assemblies.Length} sources={sources} missingOutputs={missing}\n");

            var ok = !failed && missing == 0 && assemblies.Length > 0;
            builder.Append($"[CompileVerify] {(ok ? "PASS" : "FAIL")}");
            summary = builder.ToString();
            return ok;
        }
    }
}
#endif
