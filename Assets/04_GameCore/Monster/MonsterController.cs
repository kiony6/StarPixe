using System.Collections.Generic;
using StarPixelSurge.Framework;
using StarPixelSurge.Map;
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

        private Transform _target;
        private float _attackTimer;
        private GameObject _barrageTemplate;

        public bool IsAlive => _health > 0f;

        public bool IsBoss => isBoss;

        public bool IsEnraged => _enraged;

        public float HealthNormalized => maxHealth > 0f ? Mathf.Clamp01(_health / maxHealth) : 0f;

        /// <summary>当前生命值，供网络快照采集。</summary>
        public float Health => _health;

        public void Configure(float health, float speed, float experience, bool boss = false)
        {
            maxHealth = Mathf.Max(1f, health);
            moveSpeed = Mathf.Max(0.1f, speed);
            experienceReward = experience;
            isBoss = boss;
            _health = maxHealth;
            _baseSpeed = moveSpeed;
            _baseContactDamage = contactDamage;
            _enraged = false;
            transform.localScale = Vector3.one * (boss ? 1.8f : 1f);

            if (boss)
            {
                EventBus.Publish(new BossSpawnedEvent(gameObject, bossDisplayName, maxHealth));
            }
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
                _target = PlayerController.Instance != null ? PlayerController.Instance.transform : null;
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
            renderer.sprite = RuntimeSprite.CreateSquare(new Color(1f, 0.45f, 0.75f), 6);
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
            _target = PlayerController.Instance != null ? PlayerController.Instance.transform : null;
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
