using StarPixelSurge.Framework;
using UnityEngine;

namespace StarPixelSurge.GameCore
{
    /// <summary>
    /// 直线飞行的技能弹道。命中首个存活的怪物后结算伤害并归还对象池。
    /// </summary>
    public class Projectile : MonoBehaviour, IPoolable
    {
        [SerializeField] private float speed = 10f;
        [SerializeField] private float lifetime = 2f;
        [SerializeField] private float hitRadius = 0.3f;
        [SerializeField] private LayerMask hitMask = ~0;

        private Vector2 _direction = Vector2.right;
        private float _damage;
        private float _life;
        private SpriteRenderer _renderer;
        private string _skillId;
        private bool _critical;
        private int _pierceCount;
        private bool _homing;
        private Transform _homingTarget;
        private float _homingSpeed = 5f;

        public void Launch(Vector2 direction, float damage, float rangeScale = 1f)
        {
            _direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            _damage = damage;
            _life = lifetime * Mathf.Max(0.1f, rangeScale);
        }

        /// <summary>施放技能时一次性完成外观与运动参数初始化。</summary>
        public void Launch(Vector2 direction, float damage, float rangeScale, string skillId, Color color, float scale, bool critical = false, int pierce = 0, bool homing = false)
        {
            _skillId = skillId;
            _critical = critical;
            _pierceCount = pierce;
            _homing = homing;
            if (_homing)
            {
                FindHomingTarget();
            }
            SetVisual(color, scale);
            Launch(direction, damage, rangeScale);
        }

        private void FindHomingTarget()
        {
            if (MonsterController.Active.Count == 0) return;
            _homingTarget = MonsterController.Active[0].transform;
            var minDist = float.MaxValue;
            var pos = transform.position;
            foreach (var m in MonsterController.Active)
            {
                var dist = Vector2.Distance(pos, m.transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    _homingTarget = m.transform;
                }
            }
        }

        /// <summary>设置弹道的占位外观，按技能类型区分颜色，并按技能记录资源 id。</summary>
        public void SetVisual(Color color, float scale = 1f)
        {
            if (_renderer == null)
            {
                _renderer = GetComponent<SpriteRenderer>();
            }

            if (_renderer != null)
            {
                _renderer.sprite = SpriteCatalog.Resolve("projectiles", _skillId, color, RuntimeSprite.Shape.Diamond, 6);
                _renderer.color = Color.white;
            }

            transform.localScale = Vector3.one * Mathf.Max(0.2f, scale);
        }

        private void Update()
        {
            // 追踪逻辑
            if (_homing && _homingTarget != null)
            {
                var toTarget = (Vector2)_homingTarget.position - (Vector2)transform.position;
                if (toTarget.magnitude > 0.01f)
                {
                    var targetDir = toTarget.normalized;
                    _direction = Vector2.Lerp(_direction, targetDir, Time.deltaTime * _homingSpeed).normalized;
                }
            }

            transform.position += (Vector3)(_direction * speed * Time.deltaTime);

            _life -= Time.deltaTime;
            if (_life <= 0f)
            {
                Despawn();
                return;
            }

            CheckHit();
        }

        private void CheckHit()
        {
            var hits = Physics2D.OverlapCircleAll(transform.position, hitRadius, hitMask);
            for (var i = 0; i < hits.Length; i++)
            {
                var monster = hits[i].GetComponentInParent<MonsterController>();
                if (monster == null || !monster.IsAlive)
                {
                    continue;
                }

                DealDamage(monster);

                // 穿透逻辑
                if (_pierceCount > 0)
                {
                    _pierceCount--;
                }
                else
                {
                    Despawn();
                    return;
                }
            }
        }

        private void DealDamage(MonsterController monster)
        {
            monster.TakeDamage(_damage);
            var hitPoint = (Vector2)monster.transform.position;
            EventBus.Publish(new DamageDealtEvent(hitPoint, _damage, _critical));
        }

        private void Despawn()
        {
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
            _life = lifetime;
        }

        public void OnDespawn()
        {
            _life = 0f;
            _damage = 0f;
        }
    }
}
