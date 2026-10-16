using StarPixelSurge.Framework;
using UnityEngine;

namespace StarPixelSurge.GameCore
{
    /// <summary>
    /// 敌方弹幕：直线飞行，命中玩家后结算伤害并归还对象池。
    /// </summary>
    public class EnemyProjectile : MonoBehaviour, IPoolable
    {
        [SerializeField] private float speed = 4.5f;
        [SerializeField] private float lifetime = 5f;
        [SerializeField] private float hitRadius = 0.3f;

        private Vector2 _direction = Vector2.right;
        private float _damage;
        private float _life;

        public void Launch(Vector2 direction, float damage)
        {
            _direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            _damage = damage;
            _life = lifetime;
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

            if (GameManager.HasInstance && GameManager.Instance.State != GameState.Playing)
            {
                return;
            }

            var hits = Physics2D.OverlapCircleAll(transform.position, hitRadius);
            for (var i = 0; i < hits.Length; i++)
            {
                var stats = hits[i].GetComponentInParent<PlayerStats>();
                if (stats == null || stats.IsDead)
                {
                    continue;
                }

                stats.TakeDamage(_damage);
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
