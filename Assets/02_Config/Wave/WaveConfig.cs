using System;
using UnityEngine;

namespace StarPixelSurge.Config
{
    [Serializable]
    public class MonsterGroupConfig
    {
        public string MonsterId;
        public string PrefabPath;
        public int Count = 5;
        public float SpawnInterval = 0.5f;
    }

    /// <summary>
    /// 单层波次配置：怪物组成与难度缩放。
    /// </summary>
    [CreateAssetMenu(menuName = "StarPixelSurge/Config/Wave", fileName = "Wave_")]
    public class WaveConfig : ScriptableObject
    {
        public int Layer;
        public string ThemeId;
        public MonsterGroupConfig[] Groups = Array.Empty<MonsterGroupConfig>();
        public float HealthScale = 1f;
        public float SpeedScale = 1f;
        public float CountScale = 1f;
        public bool HasBoss;
    }
}
