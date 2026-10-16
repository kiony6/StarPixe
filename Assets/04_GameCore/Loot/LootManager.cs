using System.Collections.Generic;
using StarPixelSurge.Config;
using StarPixelSurge.Framework;
using StarPixelSurge.Map;
using UnityEngine;

namespace StarPixelSurge.GameCore
{
    /// <summary>
    /// 掉落物生成服务。宝箱房间触发时在房间地面散落可拾取词缀掉落物。
    /// 配置库未提供词缀时使用内置回退词缀，保证原型开箱即有收益。
    /// </summary>
    public class LootManager : MonoSingleton<LootManager>
    {
        private GameObject _template;
        private static List<AffixConfig> _fallback;

        /// <summary>在指定房间地面生成 <paramref name="count"/> 个词缀掉落物。</summary>
        public void SpawnDrops(RoomMap map, Room room, int count)
        {
            var pool = ResolveAffixPool();
            if (pool.Count == 0 || count <= 0)
            {
                return;
            }

            var center = room.WorldCenter;
            var radius = Mathf.Max(2f, Mathf.Min(room.TileRect.width, room.TileRect.height) * 0.4f);

            var player = PlayerController.Instance;
            var stats = player != null ? player.GetComponentInParent<PlayerStats>() : null;
            var minDistance = stats != null ? stats.PickupRange + 1f : 2.5f;

            for (var i = 0; i < count; i++)
            {
                var affix = pool[Random.Range(0, pool.Count)];
                var position = center;

                for (var attempt = 0; attempt < 16; attempt++)
                {
                    var candidate = map != null ? map.GetSpawnPosition(center, radius) : center;
                    position = candidate;

                    if (player == null)
                    {
                        break;
                    }

                    if (((Vector2)player.transform.position - candidate).sqrMagnitude >= minDistance * minDistance)
                    {
                        break;
                    }
                }

                SpawnSingle(affix, position);
            }
        }

        private void SpawnSingle(AffixConfig affix, Vector2 position)
        {
            var template = ResolveTemplate();
            if (template == null)
            {
                return;
            }

            GameObject instance;
            if (PoolManager.HasInstance)
            {
                instance = PoolManager.Instance.Spawn(template, position, Quaternion.identity);
            }
            else
            {
                instance = Instantiate(template, position, Quaternion.identity);
                instance.SetActive(true);
            }

            if (instance == null)
            {
                return;
            }

            var drop = instance.GetComponent<LootDrop>();
            if (drop != null)
            {
                drop.Configure(affix);
            }
        }

        private GameObject ResolveTemplate()
        {
            if (_template != null)
            {
                return _template;
            }

            var go = new GameObject("LootDrop_Runtime");
            go.transform.SetParent(transform, false);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = RuntimeSprite.CreateSquare(Color.white, 10);
            renderer.sortingOrder = 6;

            go.AddComponent<LootDrop>();
            go.SetActive(false);
            _template = go;
            return _template;
        }

        private static List<AffixConfig> ResolveAffixPool()
        {
            var list = new List<AffixConfig>();
            if (ConfigService.HasInstance)
            {
                foreach (var affix in ConfigService.Instance.GetAllAffixes())
                {
                    if (affix != null)
                    {
                        list.Add(affix);
                    }
                }
            }

            if (list.Count == 0)
            {
                list.AddRange(GetFallbackAffixes());
            }

            return list;
        }

        private static List<AffixConfig> GetFallbackAffixes()
        {
            if (_fallback != null)
            {
                return _fallback;
            }

            _fallback = new List<AffixConfig>
            {
                CreateFallback("affix_power", "强能", "技能伤害 +15%", "damage", 0.15f, true, AffixRarity.Rare),
                CreateFallback("affix_reach", "延展", "技能范围 +20%", "range", 0.2f, false, AffixRarity.Common),
                CreateFallback("affix_swift", "迅捷", "技能冷却 -10%", "cooldown", -0.1f, false, AffixRarity.Common),
                CreateFallback("affix_precise", "精准", "暴击率 +5%", "critChance", 0.05f, false, AffixRarity.Rare),
                CreateFallback("affix_haste", "疾行", "移动速度 +0.5", "moveSpeed", 0.5f, false, AffixRarity.Common),
                CreateFallback("affix_volume", "扩量", "弹道数量 +1", "projectileCount", 1f, false, AffixRarity.Epic)
            };
            return _fallback;
        }

        private static AffixConfig CreateFallback(string id, string displayName, string description, string statKey, float value, bool multiplicative, AffixRarity rarity)
        {
            var affix = ScriptableObject.CreateInstance<AffixConfig>();
            affix.Id = id;
            affix.DisplayName = displayName;
            affix.Description = description;
            affix.StatKey = statKey;
            affix.Value = value;
            affix.IsMultiplicative = multiplicative;
            affix.Rarity = rarity;
            affix.Tags = System.Array.Empty<string>();
            return affix;
        }
    }
}
