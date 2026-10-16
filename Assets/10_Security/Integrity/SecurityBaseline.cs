using System.Collections.Generic;
using UnityEngine;

namespace StarPixelSurge.Security
{
    /// <summary>
    /// 发布完整性基线。记录程序集与关键资源在打包时的哈希快照，
    /// 运行时由 <see cref="IntegrityChecker"/> 比对（需求 13.5）。
    /// 由菜单 <c>StarPixelSurge/Security/Generate Integrity Baseline</c> 生成。
    /// </summary>
    [CreateAssetMenu(fileName = "SecurityBaseline", menuName = "StarPixelSurge/Security Baseline")]
    public class SecurityBaseline : ScriptableObject
    {
        [System.Serializable]
        public class FileHash
        {
            public string Path;
            public string Hash;
        }

        /// <summary>是否强制校验。开发期为 false，正式构建置 true。</summary>
        public bool Enforced;

        /// <summary>入口程序集文件绝对路径（打包时写入，运行时可能因平台不同为空）。</summary>
        public string EntryAssemblyPath;

        public string EntryAssemblyHash;

        public List<FileHash> Resources = new List<FileHash>();
    }
}
