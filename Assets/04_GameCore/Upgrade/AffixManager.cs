using System;
using System.Collections.Generic;
using UnityEngine;

namespace StarPixelSurge.GameCore
{
    /// <summary>
    /// 词缀类型。
    /// </summary>
    public enum AffixType
    {
        AttackDamage,      // 攻击加成
        CooldownReduction, // 冷却缩减
        Multishot,         // 多重投射
        MoveSpeed,         // 移动速度
        MaxHealth,         // 最大生命
        CritChance,        // 暴击率
        ProjectileSpeed,   // 弹道速度
        PickupRadius,      // 拾取范围
    }

    /// <summary>
    /// 词缀数据。
    /// </summary>
    [Serializable]
    public class AffixData
    {
        public string Id;
        public string Name;
        public string Description;
        public AffixType Type;
        public float Value;
        public Color Color = Color.white;
    }

    /// <summary>
    /// 词缀管理器：管理玩家身上的所有词缀效果。
    /// </summary>
    public class AffixManager : MonoBehaviour
    {
        public static AffixManager Instance { get; private set; }

        private readonly Dictionary<AffixType, float> _affixValues = new Dictionary<AffixType, float>();
        public event Action OnAffixChanged;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>添加词缀。</summary>
        public void AddAffix(AffixData affix)
        {
            if (!_affixValues.ContainsKey(affix.Type))
            {
                _affixValues[affix.Type] = 0f;
            }
            _affixValues[affix.Type] += affix.Value;
            OnAffixChanged?.Invoke();

            Debug.Log($"[Affix] 添加词缀: {affix.Name} +{affix.Value}");
        }

        /// <summary>获取词缀值。</summary>
        public float GetAffixValue(AffixType type)
        {
            return _affixValues.TryGetValue(type, out var value) ? value : 0f;
        }

        /// <summary>获取攻击伤害倍率。</summary>
        public float GetDamageMultiplier() => 1f + GetAffixValue(AffixType.AttackDamage) / 100f;

        /// <summary>获取冷却缩减（0-1之间）。</summary>
        public float GetCooldownReduction() => Mathf.Clamp01(GetAffixValue(AffixType.CooldownReduction) / 100f);

        /// <summary>获取多重投射数量加成。</summary>
        public int GetMultishotBonus() => Mathf.FloorToInt(GetAffixValue(AffixType.Multishot));

        /// <summary>获取移动速度倍率。</summary>
        public float GetMoveSpeedMultiplier() => 1f + GetAffixValue(AffixType.MoveSpeed) / 100f;

        /// <summary>获取暴击率（0-1之间）。</summary>
        public float GetCritChance() => Mathf.Clamp01(GetAffixValue(AffixType.CritChance) / 100f);
    }
}
