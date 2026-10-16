using System.Collections.Generic;
using StarPixelSurge.Config;
using StarPixelSurge.Framework;
using StarPixelSurge.Map;
using UnityEngine;

namespace StarPixelSurge.GameCore
{
    /// <summary>
    /// 波次管理器：按时间推进层数，优先读取 WaveConfig 决定怪物构成与难度缩放，
    /// 缺省时回退到程序化难度曲线。怪物生成分摊到多帧以规避瞬间卡顿。
    /// </summary>
    public class WaveManager : MonoSingleton<WaveManager>
    {
        private struct SpawnRequest
        {
            public float HealthScale;
            public float SpeedScale;
            public float ExperienceScale;
            public bool IsBoss;
        }

        private struct Archetype
        {
            public float Health;
            public float Speed;
            public float Experience;
            public bool IsBoss;
        }

        [Header("Timing")]
        [SerializeField] private float layerDuration = 20f;

        [Header("Spawn")]
        [SerializeField] private GameObject monsterPrefab;
        [SerializeField] private int baseMonsterCount = 8;
        [SerializeField] private float spawnRadius = 9f;
        [SerializeField] private int maxSpawnPerFrame = 20;

        [Header("Difficulty Curve (fallback)")]
        [SerializeField] private float countGrowth = 1.20f;
        [SerializeField] private float healthGrowth = 1.15f;
        [SerializeField] private float speedGrowth = 1.02f;
        [SerializeField] private float baseMonsterHealth = 20f;
        [SerializeField] private float baseMonsterSpeed = 2f;
        [SerializeField] private float baseMonsterExperience = 1f;

        private Transform _player;
        private RoomMap _map;
        private readonly Queue<SpawnRequest> _spawnQueue = new Queue<SpawnRequest>();
        private float _layerTimer;
        private bool _running;
        private Vector2 _bossCenter;
        private bool _hasBossCenter;

        public int CurrentLayer { get; private set; }

        public void SetPlayer(Transform player)
        {
            _player = player;
        }

        public void SetMap(RoomMap map)
        {
            _map = map;
        }

        public void SetMonsterPrefab(GameObject prefab)
        {
            monsterPrefab = prefab;
        }

        public void StartWaves()
        {
            CurrentLayer = 0;
            _layerTimer = 0f;
            _spawnQueue.Clear();
            _running = true;
            AdvanceLayer();
        }

        public void StopWaves()
        {
            _running = false;
            _spawnQueue.Clear();
        }

        /// <summary>在玩家附近立即追加一波精英遭遇（用于精英房间）。</summary>
        public void SpawnEliteEncounter(int count = 5)
        {
            var (health, speed) = CurrentScales();
            for (var i = 0; i < count; i++)
            {
                var elite = GetArchetype("elite");
                _spawnQueue.Enqueue(new SpawnRequest
                {
                    HealthScale = health * elite.Health,
                    SpeedScale = speed * elite.Speed,
                    ExperienceScale = elite.Experience,
                    IsBoss = false
                });
            }
        }

        /// <summary>在指定位置附近追加首领遭遇（首领 + 护卫）。用于 BOSS 房间。</summary>
        public void SpawnBossEncounter(Vector2 center, int guards = 6)
        {
            _bossCenter = center;
            _hasBossCenter = true;

            var (health, speed) = CurrentScales();
            var boss = GetArchetype("boss");
            _spawnQueue.Enqueue(new SpawnRequest
            {
                HealthScale = health * boss.Health,
                SpeedScale = speed * boss.Speed,
                ExperienceScale = boss.Experience,
                IsBoss = true
            });

            for (var i = 0; i < guards; i++)
            {
                var grunt = GetArchetype("brute");
                _spawnQueue.Enqueue(new SpawnRequest
                {
                    HealthScale = health * grunt.Health,
                    SpeedScale = speed * grunt.Speed,
                    ExperienceScale = grunt.Experience,
                    IsBoss = false
                });
            }
        }

        private (float health, float speed) CurrentScales()
        {
            var config = ConfigService.HasInstance ? ConfigService.Instance.GetWave(Mathf.Max(1, CurrentLayer)) : null;
            if (config != null)
            {
                return (config.HealthScale, config.SpeedScale);
            }

            return (Mathf.Pow(healthGrowth, Mathf.Max(0, CurrentLayer - 1)), Mathf.Pow(speedGrowth, Mathf.Max(0, CurrentLayer - 1)));
        }

        private void Update()
        {
            if (!_running || _player == null)
            {
                return;
            }

            if (!GameManager.HasInstance || GameManager.Instance.State != GameState.Playing)
            {
                return;
            }

            _layerTimer += Time.deltaTime;
            if (_layerTimer >= layerDuration)
            {
                _layerTimer = 0f;
                AdvanceLayer();
            }

            DrainSpawnQueue();
        }

        private void AdvanceLayer()
        {
            CurrentLayer++;
            EventBus.Publish(new WaveLayerChangedEvent(CurrentLayer));

            var config = ConfigService.HasInstance ? ConfigService.Instance.GetWave(CurrentLayer) : null;
            if (config != null && config.Groups != null && config.Groups.Length > 0)
            {
                BuildFromConfig(config);
            }
            else
            {
                BuildProcedural();
            }
        }

        private void BuildFromConfig(WaveConfig config)
        {
            foreach (var group in config.Groups)
            {
                var archetype = GetArchetype(group.MonsterId);
                var count = Mathf.Max(1, Mathf.RoundToInt(group.Count * Mathf.Max(0.1f, config.CountScale)));
                for (var i = 0; i < count; i++)
                {
                    _spawnQueue.Enqueue(new SpawnRequest
                    {
                        HealthScale = config.HealthScale * archetype.Health,
                        SpeedScale = config.SpeedScale * archetype.Speed,
                        ExperienceScale = archetype.Experience,
                        IsBoss = archetype.IsBoss
                    });
                }
            }

            if (config.HasBoss)
            {
                var boss = GetArchetype("boss");
                _spawnQueue.Enqueue(new SpawnRequest
                {
                    HealthScale = config.HealthScale * boss.Health,
                    SpeedScale = config.SpeedScale * boss.Speed,
                    ExperienceScale = boss.Experience,
                    IsBoss = true
                });
            }
        }

        private void BuildProcedural()
        {
            var count = Mathf.RoundToInt(baseMonsterCount * Mathf.Pow(countGrowth, CurrentLayer - 1));
            for (var i = 0; i < count; i++)
            {
                _spawnQueue.Enqueue(new SpawnRequest
                {
                    HealthScale = Mathf.Pow(healthGrowth, CurrentLayer - 1),
                    SpeedScale = Mathf.Pow(speedGrowth, CurrentLayer - 1),
                    ExperienceScale = 1f + CurrentLayer * 0.1f,
                    IsBoss = false
                });
            }
        }

        private void DrainSpawnQueue()
        {
            var budget = maxSpawnPerFrame;
            while (budget-- > 0 && _spawnQueue.Count > 0)
            {
                SpawnMonster(_spawnQueue.Dequeue());
            }
        }

        private void SpawnMonster(SpawnRequest request)
        {
            if (monsterPrefab == null || _player == null || !PoolManager.HasInstance)
            {
                return;
            }

            var origin = (Vector2)_player.position;
            if (request.IsBoss && _hasBossCenter)
            {
                origin = _bossCenter;
            }

            Vector2 position;
            if (_map != null)
            {
                position = _map.GetSpawnPosition(origin, spawnRadius);
            }
            else
            {
                var angle = Random.Range(0f, Mathf.PI * 2f);
                position = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * spawnRadius;
            }

            var instance = PoolManager.Instance.Spawn(monsterPrefab, position, Quaternion.identity);
            var monster = instance.GetComponent<MonsterController>();
            if (monster == null)
            {
                return;
            }

            var health = baseMonsterHealth * request.HealthScale;
            var speed = baseMonsterSpeed * request.SpeedScale;
            var experience = baseMonsterExperience * request.ExperienceScale;

            monster.SetTarget(_player);
            monster.Configure(health, speed, experience, request.IsBoss);
        }

        /// <summary>按怪物 ID 返回基础属性倍率，使不同怪物分组具备差异化。</summary>
        private static Archetype GetArchetype(string monsterId)
        {
            switch (monsterId)
            {
                case "runner":
                    return new Archetype { Health = 0.6f, Speed = 1.6f, Experience = 0.9f };
                case "brute":
                    return new Archetype { Health = 2.2f, Speed = 0.75f, Experience = 1.6f };
                case "elite":
                    return new Archetype { Health = 3.5f, Speed = 0.9f, Experience = 2.5f };
                case "boss":
                    return new Archetype { Health = 12f, Speed = 0.7f, Experience = 10f, IsBoss = true };
                default:
                    return new Archetype { Health = 1f, Speed = 1f, Experience = 1f };
            }
        }
    }
}
