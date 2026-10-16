#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using StarPixelSurge.Framework;
using StarPixelSurge.GameCore;
using StarPixelSurge.Map;
using StarPixelSurge.Network;
using StarPixelSurge.Audio;
using StarPixelSurge.Effect;

namespace StarPixelSurge.EditorTools
{
    /// <summary>
    /// 一键创建启动场景：自动挂载 GameBootstrap + NetManager + GameManager + 相机 + 特效/音频系统。
    /// 用法：在 Unity 菜单选「Tools/Create Boot Scene」，或命令行 -executeMethod CreateBootScene.Run
    /// </summary>
    public static class CreateBootScene
    {
        private const string ScenePath = "Assets/Scenes/Boot.unity";

        [MenuItem("Tools/StarPixelSurge/Create Boot Scene")]
        public static void Run()
        {
            // 创建新场景
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. 主相机
            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            var cam = cameraGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.03f, 0.08f);
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);
            cameraGo.AddComponent<AudioListener>();
            cameraGo.AddComponent<CameraFollow>();
            cameraGo.AddComponent<CameraFeedback>();

            // 2. 核心管理器
            var gameManagerGo = new GameObject("GameManager");
            gameManagerGo.AddComponent<GameManager>();

            var netManagerGo = new GameObject("NetManager");
            netManagerGo.AddComponent<NetManager>();

            // 3. GameBootstrap（对局启动入口）
            var bootstrapGo = new GameObject("GameBootstrap");
            bootstrapGo.AddComponent<GameBootstrap>();

            // 4. 特效系统
            var effectGo = new GameObject("EffectSystems");
            effectGo.AddComponent<GraphicsTierController>();
            effectGo.AddComponent<HitFeedback>();

            // 5. 音频系统
            var audioGo = new GameObject("AudioSystems");
            audioGo.AddComponent<AudioManager>();
            audioGo.AddComponent<AudioFeedback>();

            // 保存场景
            var dir = System.IO.Path.GetDirectoryName(ScenePath);
            if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
            {
                AssetDatabase.CreateFolder("Assets", "Scenes");
            }

            var saved = EditorSceneManager.SaveScene(scene, ScenePath);
            if (saved)
            {
                // 注册到 Build Settings
                var scenes = EditorBuildSettings.scenes;
                var newScenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(scenes);
                newScenes.Add(new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = newScenes.ToArray();

                Debug.Log($"[CreateBootScene] 启动场景创建成功: {ScenePath}");
                Debug.Log("[CreateBootScene] 已挂载: Main Camera, GameManager, NetManager, GameBootstrap, EffectSystems, AudioSystems");
            }
            else
            {
                Debug.LogError("[CreateBootScene] 场景保存失败");
            }

            AssetDatabase.Refresh();
        }
    }
}
#endif
