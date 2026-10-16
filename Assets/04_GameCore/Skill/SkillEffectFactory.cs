using System;
using UnityEngine;

namespace StarPixelSurge.GameCore
{
    /// <summary>
    /// 技能特效类型。
    /// </summary>
    public enum SkillEffectType
    {
        StraightProjectile,   // 直线弹道
        PiercingProjectile,   // 穿透弹道
        HomingProjectile,     // 追踪弹道
        SplittingProjectile,  // 分裂弹道
        LaserBeam,            // 激光束
        AOEExplosion,         // 范围爆炸
        ChainLightning,       // 链式闪电
        NovaWave,             // 环形冲击波
    }

    /// <summary>
    /// 技能特效配置。
    /// </summary>
    [Serializable]
    public class SkillEffectConfig
    {
        public string SkillId;
        public SkillEffectType EffectType;
        public Color Color = Color.cyan;
        public float DamageMultiplier = 1f;
        public float Speed = 10f;
        public float Lifetime = 2f;
        public int PierceCount = 0;
        public float SplitAngle = 30f;
        public int SplitCount = 3;
        public float AoeRadius = 2f;
    }

    /// <summary>
    /// 技能特效生成器：根据技能类型生成对应的弹道和特效。
    /// </summary>
    public static class SkillEffectFactory
    {
        /// <summary>生成弹道。</summary>
        public static GameObject SpawnProjectile(Vector2 position, Vector2 direction, SkillEffectConfig config)
        {
            var go = new GameObject($"Projectile_{config.SkillId}");
            go.transform.position = position;

            var projectile = go.AddComponent<Projectile>();
            projectile.Launch(direction, 10f * config.DamageMultiplier, 1f, config.SkillId, config.Color, 1f, false, config.PierceCount, config.EffectType == SkillEffectType.HomingProjectile);

            return go;
        }

        /// <summary>生成 AOE 爆炸。</summary>
        public static void SpawnAOEExplosion(Vector2 position, float radius, Color color)
        {
            if (EffectManager.HasInstance)
            {
                EffectManager.Instance.SpawnExplosion(position, radius, color);
            }
        }
    }
}
