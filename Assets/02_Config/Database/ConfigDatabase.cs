using System;
using UnityEngine;

namespace StarPixelSurge.Config
{
    /// <summary>
    /// 配置总表，汇总所有 ScriptableObject 配置，供 ConfigService 建立索引。
    /// </summary>
    [CreateAssetMenu(menuName = "StarPixelSurge/Config/ConfigDatabase", fileName = "ConfigDatabase")]
    public class ConfigDatabase : ScriptableObject
    {
        public SkillConfig[] Skills = Array.Empty<SkillConfig>();
        public AffixConfig[] Affixes = Array.Empty<AffixConfig>();
        public ComboConfig[] Combos = Array.Empty<ComboConfig>();
        public ClassConfig[] Classes = Array.Empty<ClassConfig>();
        public WaveConfig[] Waves = Array.Empty<WaveConfig>();
        public MapThemeConfig[] MapThemes = Array.Empty<MapThemeConfig>();
    }
}
