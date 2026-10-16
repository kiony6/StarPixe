using System;
using System.Collections.Generic;
using StarPixelSurge.Framework;
using UnityEngine;

namespace StarPixelSurge.GameCore
{
    /// <summary>
    /// 词缀类型。
    /// </summary>
    public enum AffixType
    {
        DamageUp,        // 攻击加成
        CooldownReduction, // 冷却缩减
        Multishot,       // 多重投射
        MoveSpeedUp,     // 移速加成
        MaxHealthUp,     // 最大生命加成
        ProjectileSpeedUp, // 投射物速度加成
        Pierce,          // 穿透
        ChainLightning,  // 链式闪电
    }

    /// <summary>
    /// 词缀配置。
    /// </summary>
    [Serializable]
    public class AffixConfig
    {
        public string Id;
        public string DisplayName;
        public AffixType Type;
        public float Value;
        public string Description;
    }

    /// <summary>
    /// 词缀管理器：持有当前生效的词缀，提供属性查询。
    /// </summary>
    public class AffixManager : MonoSingleton<AffixManager>
    {
        private readonly Dictionary<AffixType, float> _affixValues = new Dictionary<AffixType, float>();

        /// <summary>添加词缀。</summary>
        public void AddAffix(AffixType type, float value)
        {
            if (_affixValues.ContainsKey(type))
            {
                _affixValues[type] += value;
            }
            else
            {
                _affixValues[type] = value;
            }
        }

        /// <summary>获取词缀值。</summary>
        public float GetAffixValue(AffixType type)
        {
            return _affixValues.TryGetValue(type, out var value) ? value : 0f;
        }

        /// <summary>攻击加成倍率。</summary>
        public float DamageMultiplier => 1f + GetAffixValue(AffixType.DamageUp);

        /// <summary>冷却缩减比例（0-1）。</summary>
        public float CooldownReduction => Mathf.Clamp01(GetAffixValue(AffixType.CooldownReduction));

        /// <summary>多重投射数量加成。</summary>
        public int MultishotCount => Mathf.FloorToInt(GetAffixValue(AffixType.Multishot));

        /// <summary>移速加成倍率。</summary>
        public float MoveSpeedMultiplier => 1f + GetAffixValue(AffixType.MoveSpeedUp);

        /// <summary>最大生命加成倍率。</summary>
        public float MaxHealthMultiplier => 1f + GetAffixValue(AffixType.MaxHealthUp);

        /// <summary>投射物速度加成倍率。</summary>
        public float ProjectileSpeedMultiplier => 1f + GetAffixValue(AffixType.ProjectileSpeedUp);

        /// <summary>穿透数量。</summary>
        public int PierceCount => Mathf.FloorToInt(GetAffixValue(AffixType.Pierce));

        /// <summary>是否有链式闪电。</summary>
        public bool HasChainLightning => GetAffixValue(AffixType.ChainLightning) > 0f;

        protected override void OnSingletonAwake()
        {
            // 重置词缀
            _affixValues.Clear();
        }
    }
}
