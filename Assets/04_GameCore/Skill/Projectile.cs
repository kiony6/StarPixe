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

        public void Launch(Vector2 direction, float damage, float rangeScale = 1f)
        {
            _direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            _damage = damage;
            _life = lifetime * Mathf.Max(0.1f, rangeScale);
        }

        /// <summary>施放技能时一次性完成外观与运动参数初始化。</summary>
        public void Launch(Vector2 direction, float damage, float rangeScale, string skillId, Color color, float scale)
        {
            _skillId = skillId;
            SetVisual(color, scale);
            Launch(direction, damage, rangeScale);
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

                monster.TakeDamage(_damage);
                Despawn();
                return;
            }
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
