#if UNITY_EDITOR
using StarPixelSurge.Security;
using UnityEditor;
using UnityEngine;

namespace StarPixelSurge.EditorTools
{
    /// <summary>
    /// 安全相关工具：生成发布完整性基线、生成测试用合法卡密。
    /// </summary>
    public static class SecurityTools
    {
        private const string BaselineFolder = "Assets/10_Security/Integrity";
        private const string BaselinePath = BaselineFolder + "/SecurityBaseline.asset";

        [MenuItem("StarPixelSurge/Security/Generate Integrity Baseline")]
        public static void GenerateBaseline()
        {
            var baseline = AssetDatabase.LoadAssetAtPath<SecurityBaseline>(BaselinePath);
            if (baseline == null)
            {
                if (!AssetDatabase.IsValidFolder(BaselineFolder))
                {
                    AssetDatabase.CreateFolder("Assets/10_Security", "Integrity");
                }

                baseline = ScriptableObject.CreateInstance<SecurityBaseline>();
                AssetDatabase.CreateAsset(baseline, BaselinePath);
            }

            var assemblyPath = typeof(SecurityManager).Assembly.Location;
            baseline.EntryAssemblyPath = assemblyPath;
            baseline.EntryAssemblyHash = IntegrityChecker.ComputeFileHash(assemblyPath);
            baseline.Enforced = false;

            EditorUtility.SetDirty(baseline);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[SecurityTools] Baseline written to {BaselinePath} (assembly hash: {baseline.EntryAssemblyHash}).");
        }

        [MenuItem("StarPixelSurge/Security/Generate Demo License Key")]
        public static void GenerateDemoKey()
        {
            // 演示用载荷，实际发行应由授权服务签发。
            var payload = Random.Range(0, 99999999).ToString("D8") + "DEMO";
            var key = LocalLicenseService.BuildKey(payload);
            Debug.Log($"[SecurityTools] Demo license key: {key}");
        }
    }
}
#endif
