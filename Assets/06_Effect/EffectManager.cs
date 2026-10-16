using System.Collections.Generic;
using StarPixelSurge.Framework;
using UnityEngine;

namespace StarPixelSurge.Effect
{
    /// <summary>
    /// 特效管理器：按 effectId 生成与回收特效，并持有当前特效分级。
    /// 低配档位的粒子层数与发光叠加由特效预制体依据 Tier 自行裁剪。
    /// </summary>
    public class EffectManager : MonoSingleton<EffectManager>
    {
        private readonly Dictionary<string, GameObject> _prefabs = new Dictionary<string, GameObject>();

        public GraphicsTier Tier { get; private set; } = GraphicsTier.Standard;

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
            if (string.IsNullOrEmpty(effectId) || !PoolManager.HasInstance)
            {
                return null;
            }

            if (!_prefabs.TryGetValue(effectId, out var prefab) || prefab == null)
            {
                return null;
            }

            return PoolManager.Instance.Spawn(prefab, position, Quaternion.identity);
        }

        public void Stop(GameObject instance)
        {
            if (instance != null && PoolManager.HasInstance)
            {
                PoolManager.Instance.Despawn(instance);
            }
        }
    }
}
