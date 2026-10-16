using System;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace StarPixelSurge.Security
{
    /// <summary>完整性校验结果。</summary>
    public readonly struct IntegrityReport
    {
        public readonly bool AssembliesOk;
        public readonly bool ResourcesOk;
        public readonly string Detail;

        public IntegrityReport(bool assembliesOk, bool resourcesOk, string detail)
        {
            AssembliesOk = assembliesOk;
            ResourcesOk = resourcesOk;
            Detail = detail;
        }

        public bool Passed => AssembliesOk && ResourcesOk;
    }

    /// <summary>
    /// 完整性校验器。对程序集与关键资源计算 SHA256 并与发布基线比对（需求 13.5）。
    /// 基线未启用（开发期）时直接通过，避免阻塞迭代。
    /// </summary>
    public static class IntegrityChecker
    {
        /// <summary>比对基线的全部条目。</summary>
        public static IntegrityReport VerifyAll(SecurityBaseline baseline)
        {
            if (baseline == null || !baseline.Enforced)
            {
                return new IntegrityReport(true, true, "baseline disabled");
            }

            var assembliesOk = VerifyAssemblies(baseline);
            var resourcesOk = VerifyResources(baseline);
            var detail = assembliesOk && resourcesOk
                ? "ok"
                : $"assembly={(assembliesOk ? "ok" : "mismatch")}, resources={(resourcesOk ? "ok" : "mismatch")}";

            return new IntegrityReport(assembliesOk, resourcesOk, detail);
        }

        /// <summary>比对入口程序集哈希。</summary>
        public static bool VerifyAssemblies(SecurityBaseline baseline)
        {
            if (baseline == null || string.IsNullOrEmpty(baseline.EntryAssemblyHash))
            {
                return true;
            }

            var path = baseline.EntryAssemblyPath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                // 发布包内程序集通常不可按路径读取，缺失时不做误判。
                return true;
            }

            return Matches(path, baseline.EntryAssemblyHash);
        }

        /// <summary>比对关键资源哈希。</summary>
        public static bool VerifyResources(SecurityBaseline baseline)
        {
            if (baseline == null || baseline.Resources == null)
            {
                return true;
            }

            for (var i = 0; i < baseline.Resources.Count; i++)
            {
                var entry = baseline.Resources[i];
                if (entry == null || string.IsNullOrEmpty(entry.Path))
                {
                    continue;
                }

                if (File.Exists(entry.Path) && !Matches(entry.Path, entry.Hash))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>计算文件 SHA256 十六进制摘要。</summary>
        public static string ComputeFileHash(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return null;
            }

            try
            {
                using (var stream = File.OpenRead(path))
                using (var sha = SHA256.Create())
                {
                    var hash = sha.ComputeHash(stream);
                    var builder = new System.Text.StringBuilder(hash.Length * 2);
                    foreach (var b in hash)
                    {
                        builder.Append(b.ToString("x2"));
                    }

                    return builder.ToString();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[IntegrityChecker] Hash failed ({path}): {e.Message}");
                return null;
            }
        }

        private static bool Matches(string path, string expected)
        {
            var actual = ComputeFileHash(path);
            return actual != null && string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
        }
    }
}
