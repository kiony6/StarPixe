using System.Collections.Generic;
using StarPixelSurge.Framework;
using StarPixelSurge.Map;
using StarPixelSurge.Network;
using UnityEngine;

namespace StarPixelSurge.GameCore
{
    /// <summary>
    /// 怪物控制器：追击最近玩家、接触攻击、受击死亡并归还对象池。
    /// 通过静态注册表供技能系统快速检索最近的存活目标。
    /// 首领额外具备独立血条与低血转阶段狂暴（弹幕雨 + 全场强化）。
    /// </summary>
    public class MonsterController : MonoBehaviour, IPoolable
    {
        public static readonly List<MonsterController> Active = new List<MonsterController>();

        [SerializeField] private float maxHealth = 20f;
        [SerializeField] private float moveSpeed = 2f;
        [SerializeField] private float contactDamage = 5f;
        [SerializeField] private float attackInterval = 1f;
        [SerializeField] private float attackRange = 0.6f;
        [SerializeField] private float experienceReward = 1f;
        [SerializeField] private bool isBoss;

        [Header("Boss")]
        [SerializeField] private string bossDisplayName = "深渊领主";
        [SerializeField] private float enrageThreshold = 0.5f;
        [SerializeField] private float enrageSpeedMultiplier = 1.4f;
        [SerializeField] private float enrageDamageMultiplier = 1.6f;
        [SerializeField] private int enrageBarrageCount = 16;
        [SerializeField] private float empowerSpeedMultiplier = 1.25f;
        [SerializeField] private float empowerDamageMultiplier = 1.3f;

        private float _health;
        private float _baseSpeed;
        private float _baseContactDamage;
        private bool _enraged;
        private SpriteRenderer _renderer;
        private string _archetypeId = "grunt";

        private Transform _target;
        private float _attackTimer;
        private GameObject _barrageTemplate;

        public bool IsAlive => _health > 0f;

        public bool IsBoss => isBoss;

        public bool IsEnraged => _enraged;

        public float HealthNormalized => maxHealth > 0f ? Mathf.Clamp01(_health / maxHealth) : 0f;

        /// <summary>当前生命值，供网络快照采集。</summary>
        public float Health => _health;

        /// <summary>移动速度。</summary>
        public float MoveSpeed => moveSpeed;

        public void Configure(float health, float speed, float experience, bool boss = false, string archetypeId = "grunt")
        {
            _archetypeId = string.IsNullOrEmpty(archetypeId) ? "grunt" : archetypeId;

            // 根据怪物类型调整属性
            ApplyArchetypeStats(ref health, ref speed, ref experience);

            maxHealth = Mathf.Max(1f, health);
            moveSpeed = Mathf.Max(0.1f, speed);
            experienceReward = experience;
            isBoss = boss;
            _health = maxHealth;
            _baseSpeed = moveSpeed;
            _baseContactDamage = contactDamage;
            _enraged = false;
            transform.localScale = Vector3.one * (boss ? 1.8f : GetArchetypeScale(_archetypeId));
            ApplyVisual(_archetypeId, boss);

            if (boss)
            {
                EventBus.Publish(new BossSpawnedEvent(gameObject, bossDisplayName, maxHealth));
            }
        }

        /// <summary>根据怪物类型调整属性。</summary>
        private void ApplyArchetypeStats(ref float health, ref float speed, ref float experience)
        {
            switch (_archetypeId)
            {
                case "runner":
                    //  runner: 快速移动，低血量
                    health *= 0.7f;
                    speed *= 1.6f;
                    experience *= 0.9f;
                    break;
                case "swarmer":
                    // swarmer: 普通速度，低血量
                    health *= 0.6f;
                    speed *= 1.1f;
                    experience *= 0.7f;
                    break;
                case "charger":
                    // charger: 冲锋型，中等速度
                    health *= 1.0f;
                    speed *= 1.2f;
                    experience *= 1.0f;
                    break;
                case "brute":
                    // brute: 重型，高血量慢速
                    health *= 1.8f;
                    speed *= 0.7f;
                    experience *= 1.5f;
                    break;
                case "juggernaut":
                    // juggernaut: 超重型
                    health *= 2.5f;
                    speed *= 0.5f;
                    experience *= 2.0f;
                    break;
                case "elite":
                    // elite: 精英怪
                    health *= 1.5f;
                    speed *= 1.1f;
                    experience *= 2.5f;
                    break;
            }
        }

        /// <summary>根据怪物类型获取缩放。</summary>
        private float GetArchetypeScale(string archetypeId)
        {
            switch (archetypeId)
            {
                case "swarmer": return 0.7f;
                case "runner": return 0.8f;
                case "charger": return 1.0f;
                case "brute": return 1.3f;
                case "juggernaut": return 1.6f;
                case "elite": return 1.2f;
                default: return 1.0f;
            }
        }

        /// <summary>
        /// 按怪物原型应用占位外观：颜色与形状区分类型，尺寸区分体型。
        /// 真实美术资源存在时优先使用，缺失时回退到程序化贴图。
        /// </summary>
        private void ApplyVisual(string archetypeId, bool boss)
        {
            _archetypeId = string.IsNullOrEmpty(archetypeId) ? "grunt" : archetypeId;

            if (_renderer == null)
            {
                _renderer = GetComponent<SpriteRenderer>();
            }

            if (_renderer == null)
            {
                return;
            }

            var color = boss ? NeonPalette.Crimson : NeonPalette.Archetype(_archetypeId);
            _renderer.sprite = SpriteCatalog.Resolve("monsters", _archetypeId, color, ResolveShape(_archetypeId, boss), ResolvePixelSize(boss));
            _renderer.color = Color.white;
        }

        private static RuntimeSprite.Shape ResolveShape(string archetypeId, bool boss)
        {
            if (boss)
            {
                return RuntimeSprite.Shape.Star;
            }

            switch (archetypeId)
            {
                case "runner":
                    return RuntimeSprite.Shape.Triangle;
                case "swarmer":
                    return RuntimeSprite.Shape.Circle;
                case "charger":
                    return RuntimeSprite.Shape.Diamond;
                case "brute":
                case "juggernaut":
                    return RuntimeSprite.Shape.Hexagon;
                case "elite":
                    return RuntimeSprite.Shape.Ring;
                default:
                    return RuntimeSprite.Shape.Diamond;
            }
        }

        private static int ResolvePixelSize(bool boss)
        {
            return boss ? 16 : 8;
        }

        public void SetTarget(Transform target)
        {
            _target = target;
        }

        private void Update()
        {
            if (!IsAlive)
            {
                return;
            }

            if (GameManager.HasInstance && GameManager.Instance.State != GameState.Playing)
            {
                return;
            }

            if (_target == null)
            {
                _target = ResolveTarget();
                if (_target == null)
                {
                    return;
                }
            }

            var self = (Vector2)transform.position;
            var destination = (Vector2)_target.position;

            if (Vector2.Distance(self, destination) > attackRange)
            {
                var direction = (destination - self).normalized;
                if (NavigationService.HasInstance && NavigationService.Instance.TryGetDirection(self, out var flow))
                {
                    direction = flow;
                }

                var step = direction * moveSpeed * Time.deltaTime;
                var next = self + step;
                if (NavigationService.HasInstance && NavigationService.Instance.IsReady && !IsWalkable(next))
                {
                    // 流场不可用或目标被墙阻隔时，沿可行走轴滑动。
                    var slideX = new Vector2(next.x, self.y);
                    var slideY = new Vector2(self.x, next.y);
                    if (IsWalkable(slideX))
                    {
                        next = slideX;
                    }
                    else if (IsWalkable(slideY))
                    {
                        next = slideY;
                    }
                    else
                    {
                        next = self;
                    }
                }

                transform.position = next;
            }
            else
            {
                Attack();
            }
        }

        private static bool IsWalkable(Vector2 world)
        {
            return !NavigationService.HasInstance
                   || !NavigationService.Instance.IsReady
                   || NavigationService.Instance.IsWalkable(world);
        }

        /// <summary>
        /// 解析追击目标：联机对局优先锁定最近的玩家分身，单机回退到本地玩家控制器。
        /// </summary>
        private Transform ResolveTarget()
        {
            if (NetworkPlayerRegistry.HasInstance)
            {
                var nearest = NetworkPlayerRegistry.Instance.GetClosest(transform.position);
                if (nearest != null)
                {
                    return nearest.transform;
                }
            }

            return PlayerController.Instance != null ? PlayerController.Instance.transform : null;
        }

        private void Attack()
        {
            _attackTimer -= Time.deltaTime;
            if (_attackTimer > 0f)
            {
                return;
            }

            _attackTimer = attackInterval;
            var stats = _target.GetComponentInParent<PlayerStats>();
            if (stats != null)
            {
                stats.TakeDamage(contactDamage);
            }
        }

        public void TakeDamage(float amount)
        {
            if (!IsAlive || amount <= 0f)
            {
                return;
            }

            _health -= amount;

            if (isBoss)
            {
                EventBus.Publish(new BossHealthChangedEvent(gameObject, Mathf.Max(0f, _health), maxHealth));
            }

            if (_health <= 0f)
            {
                Die();
                return;
            }

            if (isBoss && !_enraged && HealthNormalized <= enrageThreshold)
            {
                Enrage();
            }
        }

        /// <summary>进入狂暴阶段：强化自身、强化全场怪物并释放弹幕雨。</summary>
        private void Enrage()
        {
            _enraged = true;
            moveSpeed = _baseSpeed * enrageSpeedMultiplier;
            contactDamage = _baseContactDamage * enrageDamageMultiplier;
            attackInterval = Mathf.Max(0.2f, attackInterval * 0.6f);

            EventBus.Publish(new BossEnragedEvent(gameObject, bossDisplayName));
            EmpowerAll(empowerSpeedMultiplier, empowerDamageMultiplier, this);
            TriggerBarrage();
        }

        /// <summary>强化全场存活怪物（可排除自身）。</summary>
        public static void EmpowerAll(float speedMultiplier, float damageMultiplier, MonsterController except = null)
        {
            for (var i = 0; i < Active.Count; i++)
            {
                var monster = Active[i];
                if (monster == null || monster == except || !monster.IsAlive)
                {
                    continue;
                }

                monster.Empower(speedMultiplier, damageMultiplier);
            }
        }

        public void Empower(float speedMultiplier, float damageMultiplier)
        {
            moveSpeed *= Mathf.Max(0.1f, speedMultiplier);
            contactDamage *= Mathf.Max(0.1f, damageMultiplier);
        }

        private void TriggerBarrage()
        {
            var template = ResolveBarrageTemplate();
            if (template == null)
            {
                return;
            }

            var origin = (Vector2)transform.position;
            var damage = contactDamage * 0.75f;

            for (var i = 0; i < enrageBarrageCount; i++)
            {
                var angle = Mathf.PI * 2f * i / Mathf.Max(1, enrageBarrageCount);
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

                GameObject instance;
                if (PoolManager.HasInstance)
                {
                    instance = PoolManager.Instance.Spawn(template, origin, Quaternion.identity);
                }
                else
                {
                    instance = Instantiate(template, origin, Quaternion.identity);
                    instance.SetActive(true);
                }

                if (instance == null)
                {
                    continue;
                }

                instance.GetComponent<EnemyProjectile>()?.Launch(direction, damage);
            }
        }

        private GameObject ResolveBarrageTemplate()
        {
            if (_barrageTemplate != null)
            {
                return _barrageTemplate;
            }

            var go = new GameObject("EnemyProjectile_Runtime");
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = RuntimeSprite.CreateCircle(NeonPalette.Magenta, 6);
            renderer.sortingOrder = 7;
            go.AddComponent<EnemyProjectile>();
            go.SetActive(false);
            _barrageTemplate = go;
            return _barrageTemplate;
        }

        private void Die()
        {
            EventBus.Publish(new MonsterKilledEvent(gameObject, transform.position, experienceReward));
            if (isBoss)
            {
                EventBus.Publish(new BossDefeatedEvent(gameObject));
            }

            if (PoolManager.HasInstance)
            {
                PoolManager.Instance.Despawn(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void OnSpawn()
        {
            _health = maxHealth;
            _attackTimer = 0f;
            _target = ResolveTarget();
            if (!Active.Contains(this))
            {
                Active.Add(this);
            }
        }

        public void OnDespawn()
        {
            _target = null;
            Active.Remove(this);
        }
    }
}
