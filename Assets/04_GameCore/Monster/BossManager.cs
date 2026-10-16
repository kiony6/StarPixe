using System.Collections;
using UnityEngine;

namespace StarPixelSurge.GameCore
{
    /// <summary>
    /// Boss阶段。
    /// </summary>
    public enum BossPhase
    {
        Phase1,     // 第一阶段
        Phase2,     // 第二阶段（狂暴）
        Phase3,     // 第三阶段（终极狂暴）
    }

    /// <summary>
    /// Boss管理器：处理Boss阶段转换和特殊技能。
    /// </summary>
    public class BossManager : MonoBehaviour
    {
        [Header("Boss设置")]
        [SerializeField] private MonsterController _bossController;
        [SerializeField] private float _phase2Threshold = 0.6f;  // 60%血量进入第二阶段
        [SerializeField] private float _phase3Threshold = 0.3f;  // 30%血量进入第三阶段
        [SerializeField] private float _bulletRainInterval = 3f; // 子弹雨间隔

        public BossPhase CurrentPhase { get; private set; } = BossPhase.Phase1;
        public event System.Action<BossPhase> OnPhaseChanged;

        private float _bulletRainTimer;
        private bool _isEnraged;

        private void Start()
        {
            if (_bossController == null)
            {
                _bossController = GetComponent<MonsterController>();
            }
        }

        private void Update()
        {
            if (_bossController == null || !_bossController.IsAlive) return;

            // 检查阶段转换
            var healthPercent = _bossController.HealthNormalized;
            if (CurrentPhase == BossPhase.Phase1 && healthPercent <= _phase2Threshold)
            {
                TransitionToPhase(BossPhase.Phase2);
            }
            else if (CurrentPhase == BossPhase.Phase2 && healthPercent <= _phase3Threshold)
            {
                TransitionToPhase(BossPhase.Phase3);
            }

            // 子弹雨技能
            _bulletRainTimer -= Time.deltaTime;
            if (_bulletRainTimer <= 0f && CurrentPhase >= BossPhase.Phase2)
            {
                StartBulletRain();
                _bulletRainTimer = _bulletRainInterval;
            }
        }

        private void TransitionToPhase(BossPhase newPhase)
        {
            CurrentPhase = newPhase;
            OnPhaseChanged?.Invoke(newPhase);

            Debug.Log($"[Boss] 进入阶段: {newPhase}");

            switch (newPhase)
            {
                case BossPhase.Phase2:
                    // 狂暴：速度和伤害提升
                    _bossController.Empower(1.3f, 1.5f);
                    break;
                case BossPhase.Phase3:
                    // 终极狂暴：大幅提升
                    _bossController.Empower(1.5f, 2f);
                    break;
            }
        }

        private void StartBulletRain()
        {
            Debug.Log("[Boss] 释放子弹雨！");

            // 在屏幕上方生成弹幕雨
            var screenWidth = Camera.main.orthographicSize * Camera.main.aspect * 2f;
            var screenTop = Camera.main.transform.position.y + Camera.main.orthographicSize;

            var bulletCount = 20;
            for (var i = 0; i < bulletCount; i++)
            {
                var x = Random.Range(-screenWidth / 2f, screenWidth / 2f);
                var pos = new Vector2(Camera.main.transform.position.x + x, screenTop + 2f);

                var go = new GameObject("BossBulletRain");
                go.transform.position = pos;

                var projectile = go.AddComponent<EnemyProjectile>();
                projectile.Launch(Vector2.down, 8f);
            }
        }
    }
}
