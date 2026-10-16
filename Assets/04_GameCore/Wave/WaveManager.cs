using System.Collections.Generic;
using StarPixelSurge.Framework;
using UnityEngine;

namespace StarPixelSurge.GameCore
{
    /// <summary>
    /// 波次管理器：按时间推进层数、按难度曲线生成怪物，并将生成分摊到多帧以规避瞬间卡顿。
    /// </summary>
    public class WaveManager : MonoSingleton<WaveManager>
    {
        [Header("Timing")]
        [SerializeField] private float layerDuration = 20f;

        [Header("Spawn")]
        [SerializeField] private GameObject monsterPrefab;
        [SerializeField] private int baseMonsterCount = 8;
        [SerializeField] private float spawnRadius = 9f;
        [SerializeField] private int maxSpawnPerFrame = 20;

        [Header("Difficulty Curve")]
        [SerializeField] private float countGrowth = 1.20f;
        [SerializeField] private float healthGrowth = 1.15f;
        [SerializeField] private float speedGrowth = 1.02f;
        [SerializeField] private float baseMonsterHealth = 20f;
        [SerializeField] private float baseMonsterSpeed = 2f;
        [SerializeField] private float baseMonsterExperience = 1f;

        private Transform _player;
        private readonly Queue<GameObject> _spawnQueue = new Queue<GameObject>();
        private float _layerTimer;
        private bool _running;

        public int CurrentLayer { get; private set; }

        public void SetPlayer(Transform player)
        {
            _player = player;
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

            var count = Mathf.RoundToInt(baseMonsterCount * Mathf.Pow(countGrowth, CurrentLayer - 1));
            for (var i = 0; i < count; i++)
            {
                _spawnQueue.Enqueue(monsterPrefab);
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

        private void SpawnMonster(GameObject prefab)
        {
            if (prefab == null || _player == null || !PoolManager.HasInstance)
            {
                return;
            }

            var angle = Random.Range(0f, Mathf.PI * 2f);
            var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * spawnRadius;
            var position = (Vector2)_player.position + offset;

            var instance = PoolManager.Instance.Spawn(prefab, position, Quaternion.identity);
            var monster = instance.GetComponent<MonsterController>();
            if (monster == null)
            {
                return;
            }

            var health = baseMonsterHealth * Mathf.Pow(healthGrowth, CurrentLayer - 1);
            var speed = baseMonsterSpeed * Mathf.Pow(speedGrowth, CurrentLayer - 1);
            var experience = baseMonsterExperience * (1f + CurrentLayer * 0.1f);

            monster.SetTarget(_player);
            monster.Configure(health, speed, experience, false);
        }
    }
}
