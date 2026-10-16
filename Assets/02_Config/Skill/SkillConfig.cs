using System;
using UnityEngine;

namespace StarPixelSurge.Config
{
    public enum SkillType
    {
        Projectile = 0,
        Summon = 1,
        Area = 2,
        Aura = 3
    }

    /// <summary>
    /// 单个技能的可调数值与表现配置。改数值无需改代码。
    /// </summary>
    [CreateAssetMenu(menuName = "StarPixelSurge/Config/Skill", fileName = "Skill_")]
    public class SkillConfig : ScriptableObject
    {
        [Header("Identity")]
        public string Id;
        public string DisplayName;
        [TextArea] public string Description;
        public SkillType Type = SkillType.Projectile;
        public Sprite Icon;

        [Header("Growth")]
        public float BaseDamage = 10f;
        public float DamagePerLevel = 5f;
        public float BaseRange = 3f;
        public float RangePerLevel = 0.2f;
        public float BaseCooldown = 1f;
        public float CooldownPerLevel = 0.02f;
        public int MaxLevel = 5;

        [Header("Presentation")]
        public string EffectId;
        public string ProjectilePrefabPath;

        [Header("Combo")]
        public string[] ComboPartners = Array.Empty<string>();

        public float GetDamage(int level) => BaseDamage + DamagePerLevel * Mathf.Max(0, level - 1);

        public float GetRange(int level) => BaseRange + RangePerLevel * Mathf.Max(0, level - 1);

        public float GetCooldown(int level) => Mathf.Max(0.05f, BaseCooldown - CooldownPerLevel * Mathf.Max(0, level - 1));
    }
}
