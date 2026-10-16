using System.Collections.Generic;
using StarPixelSurge.Framework;
using StarPixelSurge.Map;
using UnityEngine;

namespace StarPixelSurge.GameCore
{
    /// <summary>
    /// 怪物控制器：追击最近玩家、接触攻击、受击死亡并归还对象池。
    /// 通过静态注册表供技能系统快速检索最近的存活目标。
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

        private float _health;

        private Transform _target;
        private float _attackTimer;

        public bool IsAlive => _health > 0f;

        public bool IsBoss => isBoss;

        public void Configure(float health, float speed, float experience, bool boss = false)
        {
            maxHealth = Mathf.Max(1f, health);
            moveSpeed = Mathf.Max(0.1f, speed);
            experienceReward = experience;
            isBoss = boss;
            _health = maxHealth;
            transform.localScale = Vector3.one * (boss ? 1.8f : 1f);
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
            if (_health <= 0f)
            {
                Die();
            }
        }

        private void Die()
        {
            EventBus.Publish(new MonsterKilledEvent(gameObject, transform.position, experienceReward));
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
