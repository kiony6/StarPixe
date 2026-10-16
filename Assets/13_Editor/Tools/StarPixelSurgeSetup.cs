#if UNITY_EDITOR
using System.Collections.Generic;
using StarPixelSurge.Audio;
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

            var audioGo = new GameObject("AudioSystems");
            audioGo.AddComponent<AudioManager>();
            audioGo.AddComponent<AudioFeedback>();

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

            var affixIds = new HashSet<string>();
            foreach (var affix in database.Affixes)
            {
                if (affix == null)
                {
                    continue;
                }

                if (!affixIds.Add(affix.Id))
                {
                    Debug.LogWarning($"[StarPixelSurge] Duplicate affix id: {affix.Id}");
                }

                if (!KnownStatKeys.Contains(affix.StatKey))
                {
                    Debug.LogWarning($"[StarPixelSurge] Affix {affix.Id} uses unknown stat key: {affix.StatKey}");
                }
            }

            var classIds = new HashSet<string>();
            foreach (var classConfig in database.Classes)
            {
                if (classConfig == null)
                {
                    continue;
                }

                if (!classIds.Add(classConfig.Id))
                {
                    Debug.LogWarning($"[StarPixelSurge] Duplicate class id: {classConfig.Id}");
                }

                if (!string.IsNullOrEmpty(classConfig.StartingSkillId) && !skillIds.Contains(classConfig.StartingSkillId))
                {
                    Debug.LogWarning($"[StarPixelSurge] Class {classConfig.Id} starting skill missing: {classConfig.StartingSkillId}");
                }

                if (classConfig.SkillPool == null || classConfig.SkillPool.Length == 0)
                {
                    Debug.LogWarning($"[StarPixelSurge] Class {classConfig.Id} has an empty skill pool.");
                }

                foreach (var skillId in classConfig.SkillPool)
                {
                    if (!skillIds.Contains(skillId))
                    {
                        Debug.LogWarning($"[StarPixelSurge] Class {classConfig.Id} pool skill missing: {skillId}");
                    }
                }
            }

            var mapThemeIds = new HashSet<string>();
            foreach (var mapTheme in database.MapThemes)
            {
                if (mapTheme != null && !mapThemeIds.Add(mapTheme.Id))
                {
                    Debug.LogWarning($"[StarPixelSurge] Duplicate map theme id: {mapTheme.Id}");
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

            var comboPairs = new HashSet<string>();
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

                var key = string.CompareOrdinal(combo.SkillA, combo.SkillB) <= 0
                    ? combo.SkillA + "+" + combo.SkillB
                    : combo.SkillB + "+" + combo.SkillA;
                if (!comboPairs.Add(key))
                {
                    Debug.LogWarning($"[StarPixelSurge] Duplicate combo skill pair: {key} ({combo.Id})");
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

            Debug.Log($"[StarPixelSurge] Validate done. Skills={database.Skills.Length}, Affixes={database.Affixes.Length}, Combos={database.Combos.Length}, Classes={database.Classes.Length}, Waves={database.Waves.Length}, MapThemes={database.MapThemes.Length}, MissingLayers(1-10)={firstMissing}");
        }

        private static readonly HashSet<string> KnownStatKeys = new HashSet<string>
        {
            "damage", "moveSpeed", "maxHealth", "critChance", "range", "cooldown", "pickupRange", "projectileCount"
        };

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
