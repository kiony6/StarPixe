using System.Collections.Generic;
using UnityEngine;

namespace StarPixelSurge.Config
{
    /// <summary>
    /// 技能数据库：定义所有25个基础技能。
    /// </summary>
    [CreateAssetMenu(menuName = "StarPixelSurge/Config/SkillDatabase", fileName = "SkillDatabase")]
    public class SkillDatabase : ScriptableObject
    {
        [Header("Projectile Skills 弹道类 (10个)")]
        public SkillConfig[] ProjectileSkills;

        [Header("Area Skills 范围类 (8个)")]
        public SkillConfig[] AreaSkills;

        [Header("Aura Skills 光环类 (4个)")]
        public SkillConfig[] AuraSkills;

        [Header("Summon Skills 召唤类 (3个)")]
        public SkillConfig[] SummonSkills;

        /// <summary>获取所有技能。</summary>
        public List<SkillConfig> GetAllSkills()
        {
            var all = new List<SkillConfig>();
            if (ProjectileSkills != null) all.AddRange(ProjectileSkills);
            if (AreaSkills != null) all.AddRange(AreaSkills);
            if (AuraSkills != null) all.AddRange(AuraSkills);
            if (SummonSkills != null) all.AddRange(SummonSkills);
            return all;
        }

        /// <summary>按ID查找技能。</summary>
        public SkillConfig GetSkillById(string id)
        {
            foreach (var skill in GetAllSkills())
            {
                if (skill.Id == id) return skill;
            }
            return null;
        }
    }

    /// <summary>
    /// 预设技能ID常量。
    /// </summary>
    public static class SkillIds
    {
        // 弹道类
        public const string MagicBolt = "magic_bolt";
        public const string Fireball = "fireball";
        public const string IceShard = "ice_shard";
        public const string Lightning = "lightning";
        public const string PoisonDart = "poison_dart";
        public const string PiercingArrow = "piercing_arrow";
        public const string HomingMissile = "homing_missile";
        public const string SplitBomb = "split_bomb";
        public const string LaserBeam = "laser_beam";
        public const string NovaWave = "nova_wave";

        // 范围类
        public const string Explosion = "explosion";
        public const string FrozenField = "frozen_field";
        public const string PoisonCloud = "poison_cloud";
        public const string MeteorStrike = "meteor_strike";
        public const string Shockwave = "shockwave";
        public const string ChainLightning = "chain_lightning";
        public const string Flamethrower = "flamethrower";
        public const string GroundSmash = "ground_smash";

        // 光环类
        public const string DamageAura = "damage_aura";
        public const string SpeedAura = "speed_aura";
        public const string RegenAura = "regen_aura";
        public const string ShieldAura = "shield_aura";

        // 召唤类
        public const string SummonSkeleton = "summon_skeleton";
        public const string SummonGolem = "summon_golem";
        public const string SummonSpirit = "summon_spirit";
    }
}
