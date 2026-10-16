using System.Collections.Generic;
using UnityEngine;

namespace StarPixelSurge.Framework
{
    /// <summary>
    /// 美术资源目录。按 <c>Resources/Sprites/&lt;category&gt;/&lt;id&gt;</c> 约定加载真实 Sprite；
    /// 资源缺失时回退到程序化占位贴图，保证接入美术前后代码都不中断。
    ///
    /// 约定：放入该目录的贴图需按完整彩色美术绘制（加载后渲染器颜色统一为白色），
    /// 命名与 id 一致（例如 <c>Resources/Sprites/monsters/boss</c>）。详见
    /// <c>Assets/11_Res/Sprites/README.md</c>。
    /// </summary>
    public static class SpriteCatalog
    {
        public const string Root = "Sprites";

        private static readonly Dictionary<string, Sprite> Loaded = new Dictionary<string, Sprite>();
        private static readonly HashSet<string> Missing = new HashSet<string>();

        /// <summary>尝试加载真实 Sprite；未配置时返回 null。</summary>
        public static Sprite Load(string category, string id)
        {
            if (string.IsNullOrEmpty(category) || string.IsNullOrEmpty(id))
            {
                return null;
            }

            var key = category + "/" + id;
            if (Loaded.TryGetValue(key, out var cached))
            {
                return cached;
            }

            if (Missing.Contains(key))
            {
                return null;
            }

            var path = Root + "/" + key;
            var sprite = Resources.Load<Sprite>(path);
            if (sprite != null)
            {
                Loaded[key] = sprite;
                return sprite;
            }

            Missing.Add(key);
            return null;
        }

        /// <summary>解析 Sprite：优先真实资源，缺失时回退到指定形状与颜色的程序化占位。</summary>
        public static Sprite Resolve(string category, string id, Color fallbackColor, RuntimeSprite.Shape fallbackShape, int fallbackSize)
        {
            var real = Load(category, id);
            return real != null ? real : RuntimeSprite.Get(fallbackShape, fallbackColor, fallbackSize);
        }

        /// <summary>清空加载缓存。切换场景或热重载美术资源后调用。</summary>
        public static void Clear()
        {
            Loaded.Clear();
            Missing.Clear();
        }
    }
}
