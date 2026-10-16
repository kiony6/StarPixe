using System.Collections.Generic;
using UnityEngine;

namespace StarPixelSurge.Config
{
    /// <summary>
    /// 默认技能配置：25个技能的初始数值。
    /// </summary>
    public static class SkillDefaults
    {
        /// <summary>获取所有默认技能配置。</summary>
        public static List<SkillConfig> GetDefaultSkills()
        {
            var skills = new List<SkillConfig>
            {
                // === 弹道类 (10个) ===
                CreateSkill(SkillIds.MagicBolt, "魔法弹", "基础魔法弹，自动射向最近敌人", SkillType.Projectile, 10f, 1f, 0.8f),
                CreateSkill(SkillIds.Fireball, "火球术", "发射火球，命中后小范围爆炸", SkillType.Projectile, 15f, 1.2f, 1.2f),
                CreateSkill(SkillIds.IceShard, "冰刃", "快速的冰属性投射物，有减速效果", SkillType.Projectile, 12f, 1f, 0.9f),
                CreateSkill(SkillIds.Lightning, "闪电箭", "快速的电属性投射物", SkillType.Projectile, 14f, 1.1f, 0.7f),
                CreateSkill(SkillIds.PoisonDart, "毒镖", "毒属性投射物，持续造成伤害", SkillType.Projectile, 8f, 1f, 1f),
                CreateSkill(SkillIds.PiercingArrow, "穿透箭", "可穿透多个敌人的箭矢", SkillType.Projectile, 12f, 1.3f, 1.1f),
                CreateSkill(SkillIds.HomingMissile, "追踪导弹", "自动追踪敌人的导弹", SkillType.Projectile, 18f, 1.5f, 1.5f),
                CreateSkill(SkillIds.SplitBomb, "分裂炸弹", "命中后分裂成多个小炸弹", SkillType.Projectile, 20f, 2f, 2f),
                CreateSkill(SkillIds.LaserBeam, "激光束", "持续的激光光束攻击", SkillType.Projectile, 25f, 1f, 1.8f),
                CreateSkill(SkillIds.NovaWave, "环形冲击波", "向四周扩散的冲击波", SkillType.Projectile, 16f, 1.5f, 1.6f),

                // === 范围类 (8个) ===
                CreateSkill(SkillIds.Explosion, "爆炸术", "在目标位置引发爆炸", SkillType.Area, 20f, 2f, 1.5f),
                CreateSkill(SkillIds.FrozenField, "冰冻领域", "冻结范围内的敌人", SkillType.Area, 15f, 2.5f, 2f),
                CreateSkill(SkillIds.PoisonCloud, "毒雾", "生成毒雾区域，持续伤害", SkillType.Area, 10f, 3f, 2.5f),
                CreateSkill(SkillIds.MeteorStrike, "陨石天降", "召唤陨石轰击目标区域", SkillType.Area, 30f, 3f, 3f),
                CreateSkill(SkillIds.Shockwave, "震荡波", "向前发出震荡波", SkillType.Area, 18f, 1.8f, 1.8f),
                CreateSkill(SkillIds.ChainLightning, "链式闪电", "闪电在敌人间跳跃", SkillType.Area, 16f, 1.5f, 1.5f),
                CreateSkill(SkillIds.Flamethrower, "火焰喷射", "向前喷射火焰", SkillType.Area, 12f, 2f, 2f),
                CreateSkill(SkillIds.GroundSmash, "地面猛击", "猛击地面，造成范围伤害", SkillType.Area, 22f, 2.2f, 2.2f),

                // === 光环类 (4个) ===
                CreateSkill(SkillIds.DamageAura, "伤害光环", "周围敌人持续受到伤害", SkillType.Aura, 5f, 0f, 1f),
                CreateSkill(SkillIds.SpeedAura, "加速光环", "提升自身移动速度", SkillType.Aura, 0f, 0f, 0.5f),
                CreateSkill(SkillIds.RegenAura, "恢复光环", "持续恢复生命值", SkillType.Aura, 2f, 0f, 1f),
                CreateSkill(SkillIds.ShieldAura, "护盾光环", "获得护盾，吸收伤害", SkillType.Aura, 10f, 0f, 2f),

                // === 召唤类 (3个) ===
                CreateSkill(SkillIds.SummonSkeleton, "召唤骷髅", "召唤骷髅战士协助战斗", SkillType.Summon, 15f, 5f, 3f),
                CreateSkill(SkillIds.SummonGolem, "召唤魔像", "召唤强大的魔像守卫", SkillType.Summon, 30f, 8f, 5f),
                CreateSkill(SkillIds.SummonSpirit, "召唤精灵", "召唤精灵辅助战斗", SkillType.Summon, 20f, 6f, 4f),
            };

            return skills;
        }

        private static SkillConfig CreateSkill(string id, string displayName, string description, SkillType type, float baseDamage, float baseCooldown, float baseRange)
        {
            var config = ScriptableObject.CreateInstance<SkillConfig>();
            config.Id = id;
            config.DisplayName = displayName;
            config.Description = description;
            config.Type = type;
            config.BaseDamage = baseDamage;
            config.BaseCooldown = baseCooldown;
            config.BaseRange = baseRange;
            config.DamagePerLevel = baseDamage * 0.3f;
            config.CooldownPerLevel = 0.02f;
            config.MaxLevel = 5;
            return config;
        }
    }
}
