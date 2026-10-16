#if UNITY_EDITOR
using System.Collections.Generic;
using StarPixelSurge.Config;
using StarPixelSurge.Effect;
using StarPixelSurge.GameCore;
using StarPixelSurge.Map;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StarPixelSurge.EditorTools
{
    /// <summary>
    /// V1.0 开发辅助工具：生成可玩场景、校验配置数据一致性。
    /// </summary>
    public static class StarPixelSurgeSetup
    {
        private const string DatabasePath = "Assets/02_Config/Resources/Config/ConfigDatabase.asset";
        private const string SceneFolder = "Assets/07_Map/Scenes";
        private const string ScenePath = SceneFolder + "/Main.unity";

        [MenuItem("StarPixelSurge/Setup/Create Playable Scene")]
        public static void CreatePlayableScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.03f, 0.08f);
            cameraGo.AddComponent<AudioListener>();
            cameraGo.AddComponent<CameraFollow>();
            cameraGo.AddComponent<CameraFeedback>();
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);

            var effectsGo = new GameObject("EffectSystems");
            effectsGo.AddComponent<GraphicsTierController>();
            effectsGo.AddComponent<HitFeedback>();

            var bootstrapGo = new GameObject("GameBootstrap");
            bootstrapGo.AddComponent<GameBootstrap>();

            if (!AssetDatabase.IsValidFolder(SceneFolder))
            {
                AssetDatabase.CreateFolder("Assets/07_Map", "Scenes");
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.Refresh();
            Debug.Log($"[StarPixelSurge] Playable scene created: {ScenePath}");
        }

        [MenuItem("StarPixelSurge/Setup/Validate Configs")]
        public static void ValidateConfigs()
        {
            var database = AssetDatabase.LoadAssetAtPath<ConfigDatabase>(DatabasePath);
            if (database == null)
            {
                Debug.LogError($"[StarPixelSurge] ConfigDatabase not found at {DatabasePath}");
                return;
            }

            var skillIds = new HashSet<string>();
            foreach (var skill in database.Skills)
            {
                if (skill != null)
                {
                    skillIds.Add(skill.Id);
                }
            }

            var classIds = new HashSet<string>();
            foreach (var classConfig in database.Classes)
            {
                if (classConfig == null)
                {
                    continue;
                }

                classIds.Add(classConfig.Id);
                if (!string.IsNullOrEmpty(classConfig.StartingSkillId) && !skillIds.Contains(classConfig.StartingSkillId))
                {
                    Debug.LogWarning($"[StarPixelSurge] Class {classConfig.Id} starting skill missing: {classConfig.StartingSkillId}");
                }

                foreach (var skillId in classConfig.SkillPool)
                {
                    if (!skillIds.Contains(skillId))
                    {
                        Debug.LogWarning($"[StarPixelSurge] Class {classConfig.Id} pool skill missing: {skillId}");
                    }
                }
            }

            var layerSet = new HashSet<int>();
            foreach (var wave in database.Waves)
            {
                if (wave != null)
                {
                    layerSet.Add(wave.Layer);
                }
            }

            foreach (var combo in database.Combos)
            {
                if (combo == null)
                {
                    continue;
                }

                if (!skillIds.Contains(combo.SkillA) || !skillIds.Contains(combo.SkillB))
                {
                    Debug.LogWarning($"[StarPixelSurge] Combo {combo.Id} references missing skill(s).");
                }
            }

            var firstMissing = 0;
            for (var layer = 1; layer <= 10; layer++)
            {
                if (!layerSet.Contains(layer))
                {
                    firstMissing++;
                }
            }

            Debug.Log($"[StarPixelSurge] Validate done. Skills={database.Skills.Length}, Classes={database.Classes.Length}, Combos={database.Combos.Length}, Waves={database.Waves.Length}, MissingLayers(1-10)={firstMissing}");
        }

        private static void AddSceneToBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var scene in scenes)
            {
                if (scene.path == path)
                {
                    return;
                }
            }

            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
#endif
