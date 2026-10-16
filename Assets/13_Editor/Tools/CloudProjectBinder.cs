#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Threading.Tasks;

namespace StarPixelSurge.EditorTools
{
    /// <summary>
    /// 自动绑定 Unity Cloud 项目。
    /// 用法：Unity -batchmode -executeMethod StarPixelSurge.EditorTools.CloudProjectBinder.BindFromCommandLine
    /// </summary>
    public static class CloudProjectBinder
    {
        [MenuItem("Tools/StarPixelSurge/Bind Cloud Project")]
        public static void BindFromCommandLine()
        {
            Debug.Log("[CloudBinder] 开始绑定 Unity Cloud 项目...");

            // 检查当前登录状态
            var userId = CloudProjectSettings.userId;
            if (string.IsNullOrEmpty(userId))
            {
                Debug.LogError("[CloudBinder] 未登录 Unity 账号。请先在 Unity Hub 或 Editor 右上角登录。");
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log($"[CloudBinder] 当前登录用户: {userId}");
            Debug.Log($"[CloudBinder] 当前组织: {CloudProjectSettings.organizationKey}");
            Debug.Log($"[CloudBinder] 当前项目 ID: {CloudProjectSettings.projectId}");
            Debug.Log($"[CloudBinder] 当前项目名: {CloudProjectSettings.projectName}");

            if (!string.IsNullOrEmpty(CloudProjectSettings.projectId))
            {
                Debug.Log($"[CloudBinder] 项目已绑定到: {CloudProjectSettings.projectName} (ID: {CloudProjectSettings.projectId})");
                EditorApplication.Exit(0);
                return;
            }

            // 尝试创建新项目
            Debug.Log("[CloudBinder] 项目未绑定，尝试创建新项目...");

            // 注意：创建云项目需要通过 Web 流程，命令行下可能无法自动完成
            // 这里输出指引，让用户手动完成
            Debug.LogWarning("[CloudBinder] ================================================");
            Debug.LogWarning("[CloudBinder] 请在 Unity Editor 中手动完成以下步骤：");
            Debug.LogWarning("[CloudBinder] 1. 菜单栏 Window → General → Services");
            Debug.LogWarning("[CloudBinder] 2. 点击 'Create project ID'");
            Debug.LogWarning("[CloudBinder] 3. 选择组织，项目名填 StarPixelSurge");
            Debug.LogWarning("[CloudBinder] ================================================");

            EditorApplication.Exit(0);
        }
    }
}
#endif
