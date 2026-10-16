using System;
using UnityEngine;

namespace StarPixelSurge.Config
{
    public enum AffixRarity
    {
        Common = 0,
        Rare = 1,
        Epic = 2
    }

    /// <summary>
    /// 词条定义。附加到技能或玩家属性上，改变数值或机制。
    /// </summary>
    [CreateAssetMenu(menuName = "StarPixelSurge/Config/Affix", fileName = "Affix_")]
    public class AffixConfig : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        [TextArea] public string Description;

        [Tooltip("damage / range / cooldown / critChance / moveSpeed / projectileCount")]
        public string StatKey;

        public float Value;
        public bool IsMultiplicative;
        public AffixRarity Rarity = AffixRarity.Common;
        public string[] Tags = Array.Empty<string>();
    }
}
