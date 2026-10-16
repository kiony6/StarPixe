using UnityEngine;

namespace StarPixelSurge.GameCore
{
    /// <summary>
    /// 怪物行为类型。
    /// </summary>
    public enum MonsterBehaviorType
    {
        Chaser,      // 追击型：一直追玩家
        Ranged,      // 远程型：保持距离，发射弹幕
        Tank,        // 坦克型：慢速高血量
        Swarmer,     // 蜂群型：快速低血量
        Elite,       // 精英型：多种行为混合
    }

    /// <summary>
    /// 怪物AI行为控制器。
    /// </summary>
    [RequireComponent(typeof(MonsterController))]
    public class MonsterAIBehavior : MonoBehaviour
    {
        [SerializeField] private MonsterBehaviorType _behaviorType = MonsterBehaviorType.Chaser;
        [SerializeField] private float _attackRange = 3f;
        [SerializeField] private float _attackCooldown = 2f;
        [SerializeField] private float _projectileSpeed = 5f;

        private MonsterController _controller;
        private Transform _target;
        private float _attackTimer;

        private void Awake()
        {
            _controller = GetComponent<MonsterController>();
        }

        private void Start()
        {
            _target = PlayerController.Instance != null ? PlayerController.Instance.transform : null;
        }

        private void Update()
        {
            if (_target == null || !_controller.IsAlive) return;

            _attackTimer -= Time.deltaTime;

            switch (_behaviorType)
            {
                case MonsterBehaviorType.Chaser:
                    UpdateChaser();
                    break;
                case MonsterBehaviorType.Ranged:
                    UpdateRanged();
                    break;
                case MonsterBehaviorType.Tank:
                    UpdateTank();
                    break;
                case MonsterBehaviorType.Swarmer:
                    UpdateSwarmer();
                    break;
                case MonsterBehaviorType.Elite:
                    UpdateElite();
                    break;
            }
        }

        private void UpdateChaser()
        {
            // 一直追玩家
            MoveTowardsTarget();
        }

        private void UpdateRanged()
        {
            var distance = Vector2.Distance(transform.position, _target.position);

            if (distance > _attackRange)
            {
                // 太远了，靠近一点
                MoveTowardsTarget();
            }
            else if (distance < _attackRange * 0.7f)
            {
                // 太近了，后退
                MoveAwayFromTarget();
            }

            // 在攻击范围内就发射弹幕
            if (distance <= _attackRange && _attackTimer <= 0f)
            {
                FireProjectile();
                _attackTimer = _attackCooldown;
            }
        }

        private void UpdateTank()
        {
            // 慢速追击
            MoveTowardsTarget(0.5f);
        }

        private void UpdateSwarmer()
        {
            // 快速追击
            MoveTowardsTarget(1.5f);
        }

        private void UpdateElite()
        {
            var distance = Vector2.Distance(transform.position, _target.position);

            // 远程攻击 + 偶尔冲锋
            if (distance > _attackRange)
            {
                MoveTowardsTarget();
            }

            if (_attackTimer <= 0f)
            {
                // 交替发射弹幕和冲锋
                if (Random.value > 0.5f)
                {
                    FireProjectile();
                }
                else
                {
                    // 冲锋（加速移动）
                    StartCharge();
                }
                _attackTimer = _attackCooldown;
            }
        }

        private void MoveTowardsTarget(float speedMultiplier = 1f)
        {
            if (_target == null) return;
            var direction = (_target.position - transform.position).normalized;
            transform.position += (Vector3)(direction * _controller.MoveSpeed * speedMultiplier * Time.deltaTime);
        }

        private void MoveAwayFromTarget()
        {
            if (_target == null) return;
            var direction = (transform.position - _target.position).normalized;
            transform.position += (Vector3)(direction * _controller.MoveSpeed * Time.deltaTime);
        }

        private void FireProjectile()
        {
            if (_target == null) return;

            var direction = (_target.position - transform.position).normalized;
            var go = new GameObject("EnemyProjectile");
            go.transform.position = transform.position;

            var projectile = go.AddComponent<EnemyProjectile>();
            projectile.Launch(direction, 5f);
        }

        private void StartCharge()
        {
            // 占位：冲锋加速
            StartCoroutine(ChargeCoroutine());
        }

        private System.Collections.IEnumerator ChargeCoroutine()
        {
            var originalSpeed = _controller.MoveSpeed;
            _controller.MoveSpeed = originalSpeed * 2f;
            yield return new WaitForSeconds(0.5f);
            _controller.MoveSpeed = originalSpeed;
        }
    }
}
