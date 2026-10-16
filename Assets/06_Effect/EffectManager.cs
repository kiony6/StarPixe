using System.Collections.Generic;
using StarPixelSurge.Framework;
using UnityEngine;

namespace StarPixelSurge.Effect
{
    /// <summary>
    /// 特效管理器：按 effectId 生成与回收特效，并持有当前特效分级。
    /// 已注册预制体优先使用预制体；未注册时回退到按档位裁剪层数的程序化特效，
    /// 保证接入美术资源前后表现都不中断（需求 6.3、10.1-10.3）。
    /// </summary>
    public class EffectManager : MonoSingleton<EffectManager>
    {
        private static readonly Color[] Palette =
        {
            NeonPalette.Cyan,
            NeonPalette.Magenta,
            NeonPalette.Lime,
            NeonPalette.Amber,
            NeonPalette.Violet,
            NeonPalette.Crimson,
            NeonPalette.Ice,
            NeonPalette.Gold
        };

        private readonly Dictionary<string, GameObject> _prefabs = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, GameObject> _proceduralTemplates = new Dictionary<string, GameObject>();

        /// <summary>当前特效分级，影响后续生成实例的粒子层数与发光叠加。</summary>
        public GraphicsTier Tier { get; private set; } = GraphicsTier.Standard;

        protected override void OnSingletonAwake()
        {
            if (GraphicsTierController.HasInstance)
            {
                Tier = GraphicsTierController.Instance.Tier;
            }
        }

        public void SetTier(GraphicsTier tier)
        {
            Tier = tier;
        }

        public void Register(string effectId, GameObject prefab)
        {
            if (!string.IsNullOrEmpty(effectId) && prefab != null)
            {
                _prefabs[effectId] = prefab;
            }
        }

        public GameObject Play(string effectId, Vector3 position)
        {
            return Play(effectId, position, Tier);
        }

        /// <summary>按指定档位生成特效，用于让档位变更只作用于后续生成实例（需求 10.4）。</summary>
        public GameObject Play(string effectId, Vector3 position, GraphicsTier tier)
        {
            if (string.IsNullOrEmpty(effectId))
            {
                return null;
            }

            if (_prefabs.TryGetValue(effectId, out var prefab) && prefab != null)
            {
                return Spawn(prefab, position);
            }

            return PlayProcedural(effectId, position, tier);
        }

        public void Stop(GameObject instance)
        {
            if (instance == null || !PoolManager.HasInstance)
            {
                return;
            }

            PoolManager.Instance.Despawn(instance);
        }

        /// <summary>回收特效实例，语义等同 <see cref="Stop"/>。</summary>
        public void Recycle(GameObject instance)
        {
            Stop(instance);
        }

        private static GameObject Spawn(GameObject prefab, Vector3 position)
        {
            if (PoolManager.HasInstance)
            {
                return PoolManager.Instance.Spawn(prefab, position, Quaternion.identity);
            }

            return Instantiate(prefab, position, Quaternion.identity);
        }

        private GameObject PlayProcedural(string effectId, Vector3 position, GraphicsTier tier)
        {
            var template = GetProceduralTemplate(effectId, tier);
            return template != null ? Spawn(template, position) : null;
        }

        private GameObject GetProceduralTemplate(string effectId, GraphicsTier tier)
        {
            var ultimate = IsUltimate(effectId);
            var color = ResolveColor(effectId, ultimate);
            var layers = LayerCount(tier, ultimate);
            var key = $"{ColorKey(color)}_{layers}{(ultimate ? "_u" : "_n")}";

            if (_proceduralTemplates.TryGetValue(key, out var cached) && cached != null)
            {
                return cached;
            }

            var root = new GameObject("Fx_" + key);
            root.transform.SetParent(transform, false);

            for (var i = 0; i < layers; i++)
            {
                var child = new GameObject("Layer" + i);
                child.transform.SetParent(root.transform, false);

                var renderer = child.AddComponent<SpriteRenderer>();
                renderer.sortingOrder = 20 + i;

                var layerColor = i == 0 ? color : NeonPalette.WithAlpha(color, 0.55f);
                var shape = i == 0
                    ? (ultimate ? RuntimeSprite.Shape.Star : RuntimeSprite.Shape.Diamond)
                    : RuntimeSprite.Shape.Ring;

                renderer.sprite = RuntimeSprite.Get(shape, layerColor, 16 + i * 6);
                renderer.color = layerColor;
                child.transform.localScale = Vector3.one * Mathf.Max(0.2f, 1f - i * 0.15f);
            }

            var burst = root.AddComponent<RuntimeEffectBurst>();
            burst.Lifetime = ultimate ? 0.6f : 0.32f;
            burst.StartScale = ultimate ? 0.5f : 0.4f;
            burst.EndScale = ultimate ? 2.6f : 1.6f;
            burst.Spin = ultimate ? 90f : 40f;
            burst.CacheRenderers();

            root.SetActive(false);
            _proceduralTemplates[key] = root;
            return root;
        }

        private static bool IsUltimate(string effectId)
        {
            return effectId.IndexOf("combo", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Color ResolveColor(string effectId, bool ultimate)
        {
            if (ultimate)
            {
                return NeonPalette.Gold;
            }

            var index = (effectId.GetHashCode() & 0x7fffffff) % Palette.Length;
            return Palette[index];
        }

        /// <summary>档位决定粒子层数：低配 1 层、标准 2 层、极致 3 层；终极技各加一层。</summary>
        private static int LayerCount(GraphicsTier tier, bool ultimate)
        {
            var baseLayers = tier == GraphicsTier.Low ? 1 : tier == GraphicsTier.Ultra ? 3 : 2;
            return Mathf.Clamp(baseLayers + (ultimate ? 1 : 0), 1, 4);
        }

        private static string ColorKey(Color color)
        {
            return $"{color.r:F2}{color.g:F2}{color.b:F2}";
        }
    }
}
