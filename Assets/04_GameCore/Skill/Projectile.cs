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

        public void Launch(Vector2 direction, float damage, float rangeScale = 1f)
        {
            _direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            _damage = damage;
            _life = lifetime * Mathf.Max(0.1f, rangeScale);
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
