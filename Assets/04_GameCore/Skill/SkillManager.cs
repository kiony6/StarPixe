using System;
using System.Collections.Generic;
using StarPixelSurge.Config;
using StarPixelSurge.Effect;
using StarPixelSurge.Framework;
using UnityEngine;

namespace StarPixelSurge.GameCore
{
    /// <summary>组合技解锁事件。</summary>
    public readonly struct ComboUnlockedEvent
    {
        public readonly ComboConfig Combo;

        public ComboUnlockedEvent(ComboConfig combo)
        {
            Combo = combo;
        }
    }

    /// <summary>
    /// 技能管理器：持有已获得技能及等级，按冷却自动施放，并判定组合技解锁。
    /// </summary>
    public class SkillManager : MonoSingleton<SkillManager>
    {
        private readonly Dictionary<string, int> _skillLevels = new Dictionary<string, int>();
        private readonly Dictionary<string, float> _cooldowns = new Dictionary<string, float>();
        private readonly HashSet<string> _unlockedCombos = new HashSet<string>();

        private Transform _player;
        private PlayerStats _playerStats;
        private GameObject _runtimeProjectileTemplate;

        public event Action<ComboConfig> ComboUnlocked;

        public event Action<string, int> SkillGranted;

        public IReadOnlyDictionary<string, int> SkillLevels => _skillLevels;

        public void SetPlayer(Transform player)
        {
            _player = player;
            _playerStats = player != null ? player.GetComponentInParent<PlayerStats>() : null;
        }

        public bool HasSkill(string id)
        {
            return !string.IsNullOrEmpty(id) && _skillLevels.ContainsKey(id);
        }

        public int GetLevel(string id)
        {
            return _skillLevels.TryGetValue(id, out var level) ? level : 0;
        }

        public void GrantSkill(string id)
        {
            if (string.IsNullOrEmpty(id) || _skillLevels.ContainsKey(id))
            {
                return;
            }

            _skillLevels[id] = 1;
            SkillGranted?.Invoke(id, 1);
            CheckCombos();
        }

        public bool LevelUpSkill(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }

            if (!_skillLevels.ContainsKey(id))
            {
                GrantSkill(id);
                return true;
            }

            var config = ConfigService.HasInstance ? ConfigService.Instance.GetSkill(id) : null;
            var maxLevel = config != null ? config.MaxLevel : 5;
            if (_skillLevels[id] >= maxLevel)
            {
                return false;
            }

            _skillLevels[id]++;
            SkillGranted?.Invoke(id, _skillLevels[id]);
            CheckCombos();
            return true;
        }

        public void ResetAll()
        {
            _skillLevels.Clear();
            _cooldowns.Clear();
            _unlockedCombos.Clear();
        }

        private void Update()
        {
            if (_player == null || !ConfigService.HasInstance)
            {
                return;
            }

            if (!GameManager.HasInstance || GameManager.Instance.State != GameState.Playing)
            {
                return;
            }

            foreach (var pair in _skillLevels)
            {
                var config = ConfigService.Instance.GetSkill(pair.Key);
                if (config == null)
                {
                    continue;
                }

                _cooldowns.TryGetValue(pair.Key, out var cooldown);
                cooldown -= Time.deltaTime;

                if (cooldown <= 0f)
                {
                    var cooldownMultiplier = _playerStats != null ? _playerStats.SkillCooldownMultiplier : 1f;
                    cooldown = CastSkill(config, pair.Value) ? config.GetCooldown(pair.Value) * cooldownMultiplier : 0.1f;
                }

                _cooldowns[pair.Key] = cooldown;
            }
        }

        private bool CastSkill(SkillConfig config, int level)
        {
            var target = FindNearestMonster(_player.position);
            if (target == null)
            {
                return false;
            }

            var prefab = ResolveProjectilePrefab(config.ProjectilePrefabPath);
            if (prefab == null)
            {
                return false;
            }

            var direction = ((Vector2)(target.transform.position - _player.position)).normalized;
            var damage = config.GetDamage(level);
            var stats = _player.GetComponentInParent<PlayerStats>();

            var count = 1 + (stats != null ? stats.BonusProjectiles : 0);
            var rangeScale = stats != null ? stats.SkillRangeMultiplier : 1f;
            var spreadStep = 12f * Mathf.Deg2Rad;
            var baseAngle = Mathf.Atan2(direction.y, direction.x);
            var startOffset = -(count - 1) * 0.5f;

            var color = NeonPalette.SkillType((int)config.Type);
            var scale = ProjectileScale((int)config.Type);

            for (var i = 0; i < count; i++)
            {
                var instance = PoolManager.Instance.Spawn(prefab, _player.position, Quaternion.identity);
                var projectile = instance.GetComponent<Projectile>();
                if (projectile == null)
                {
                    continue;
                }

                var shotDamage = stats != null ? stats.RollDamage(damage) : damage;
                var critical = stats != null && stats.LastRollCritical;
                var angle = baseAngle + spreadStep * (startOffset + i);
                var shotDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                projectile.Launch(shotDirection, shotDamage, rangeScale, config.Id, color, scale, critical);
            }

            if (EffectManager.HasInstance)
            {
                EffectManager.Instance.Play(config.EffectId, _player.position);
            }

            EventBus.Publish(new SkillCastEvent(config.Id, _player.position));
            return true;
        }

        /// <summary>按技能类型给出弹道占位尺寸倍率。</summary>
        private static float ProjectileScale(int type)
        {
            switch (type)
            {
                case 2:
                    return 1.4f;
                case 1:
                    return 1.15f;
                case 3:
                    return 1.1f;
                default:
                    return 1f;
            }
        }

        private static MonsterController FindNearestMonster(Vector2 origin)
        {
            MonsterController best = null;
            var bestDistance = float.MaxValue;
            var list = MonsterController.Active;

            for (var i = 0; i < list.Count; i++)
            {
                var monster = list[i];
                if (monster == null || !monster.IsAlive)
                {
                    continue;
                }

                var distance = ((Vector2)monster.transform.position - origin).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = monster;
                }
            }

            return best;
        }

        private void CheckCombos()
        {
            if (!ConfigService.HasInstance)
            {
                return;
            }

            var multiplayer = GameManager.HasInstance && GameManager.Instance.CurrentMatch.Mode == MatchMode.Multiplayer;

            foreach (var combo in ConfigService.Instance.GetAllCombos())
            {
                if (combo == null || _unlockedCombos.Contains(combo.Id))
                {
                    continue;
                }

                if (combo.MultiplayerOnly && !multiplayer)
                {
                    continue;
                }

                if (GetLevel(combo.SkillA) >= combo.RequiredLevelA && GetLevel(combo.SkillB) >= combo.RequiredLevelB)
                {
                    _unlockedCombos.Add(combo.Id);
                    ComboUnlocked?.Invoke(combo);
                    EventBus.Publish(new ComboUnlockedEvent(combo));

                    if (EffectManager.HasInstance)
                    {
                        var origin = _player != null ? _player.position : Vector3.zero;
                        EffectManager.Instance.Play(combo.FullScreenEffectId, origin);
                    }

                    var ultimateOrigin = _player != null ? (Vector2)_player.position : Vector2.zero;
                    EventBus.Publish(new UltimateTriggeredEvent(ultimateOrigin, combo.FullScreenEffectId));
                }
            }
        }

        private static GameObject ResolvePrefab(string path)
        {
            return string.IsNullOrEmpty(path) ? null : Resources.Load<GameObject>(path);
        }

        /// <summary>
        /// 解析技能弹道预制体。未配置资源时回退到运行时生成的占位弹道，保证原型可直接游玩。
        /// </summary>
        private GameObject ResolveProjectilePrefab(string path)
        {
            var prefab = ResolvePrefab(path);
            return prefab != null ? prefab : GetRuntimeProjectileTemplate();
        }

        private GameObject GetRuntimeProjectileTemplate()
        {
            if (_runtimeProjectileTemplate != null)
            {
                return _runtimeProjectileTemplate;
            }

            var go = new GameObject("Projectile_Runtime");
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = RuntimeSprite.CreateDiamond(NeonPalette.Cyan, 6);
            renderer.sortingOrder = 8;
            go.AddComponent<Projectile>();
            go.SetActive(false);
            _runtimeProjectileTemplate = go;
            return go;
        }
    }
}
