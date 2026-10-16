using UnityEngine;

namespace StarPixelSurge.Framework
{
    /// <summary>
    /// 霓虹配色表。集中管理占位与 UI 用色，避免各处硬编码色值。
    /// 所有颜色均为亮色系，搭配深色背景形成霓虹发光观感。
    /// </summary>
    public static class NeonPalette
    {
        public static readonly Color Cyan = new Color(0.35f, 0.95f, 1f);
        public static readonly Color Magenta = new Color(1f, 0.35f, 0.85f);
        public static readonly Color Lime = new Color(0.55f, 1f, 0.45f);
        public static readonly Color Amber = new Color(1f, 0.78f, 0.3f);
        public static readonly Color Violet = new Color(0.7f, 0.5f, 1f);
        public static readonly Color Crimson = new Color(1f, 0.32f, 0.42f);
        public static readonly Color Ice = new Color(0.6f, 0.85f, 1f);
        public static readonly Color Gold = new Color(1f, 0.82f, 0.35f);

        public static readonly Color Background = new Color(0.03f, 0.03f, 0.06f, 1f);
        public static readonly Color Panel = new Color(0.07f, 0.08f, 0.13f, 0.98f);
        public static readonly Color PanelDeep = new Color(0.05f, 0.05f, 0.09f, 0.98f);
        public static readonly Color Track = new Color(0.08f, 0.09f, 0.14f, 0.9f);
        public static readonly Color TextPrimary = new Color(0.92f, 0.96f, 1f);
        public static readonly Color TextMuted = new Color(0.62f, 0.68f, 0.8f);

        /// <summary>稀有度配色：0 普通 / 1 稀有 / 2 史诗。</summary>
        public static Color Rarity(int rarity)
        {
            switch (rarity)
            {
                case 2:
                    return Gold;
                case 1:
                    return Violet;
                default:
                    return Ice;
            }
        }

        /// <summary>怪物原型配色，用于区分不同怪物类型。</summary>
        public static Color Archetype(string archetypeId)
        {
            switch (archetypeId)
            {
                case "runner":
                    return Amber;
                case "swarmer":
                    return Lime;
                case "charger":
                    return new Color(1f, 0.52f, 0.3f);
                case "brute":
                    return new Color(0.62f, 0.42f, 0.95f);
                case "juggernaut":
                    return new Color(0.45f, 0.68f, 0.95f);
                case "elite":
                    return Magenta;
                case "boss":
                    return Crimson;
                default:
                    return new Color(0.85f, 0.45f, 0.6f);
            }
        }

        /// <summary>技能类型配色：0 弹道 / 1 召唤 / 2 范围 / 3 光环。</summary>
        public static Color SkillType(int type)
        {
            switch (type)
            {
                case 1:
                    return Lime;
                case 2:
                    return Amber;
                case 3:
                    return Violet;
                default:
                    return Cyan;
            }
        }

        public static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        public static Color Brighten(Color color, float amount)
        {
            return new Color(
                Mathf.Clamp01(color.r + amount),
                Mathf.Clamp01(color.g + amount),
                Mathf.Clamp01(color.b + amount),
                color.a);
        }

        public static Color Darken(Color color, float amount)
        {
            return new Color(
                Mathf.Clamp01(color.r - amount),
                Mathf.Clamp01(color.g - amount),
                Mathf.Clamp01(color.b - amount),
                color.a);
        }

        /// <summary>生成发光描边色：提亮并抬高不透明度。</summary>
        public static Color Glow(Color color, float amount = 0.25f)
        {
            var glow = Brighten(color, amount);
            glow.a = Mathf.Max(color.a, 0.9f);
            return glow;
        }
    }
}
