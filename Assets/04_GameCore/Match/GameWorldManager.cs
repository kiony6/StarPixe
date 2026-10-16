using UnityEngine;
using StarPixelSurge.Framework;

namespace StarPixelSurge.GameCore
{
    /// <summary>
    /// 游戏世界管理器：管理场景里的玩家、怪物、地图生成。
    /// </summary>
    public class GameWorldManager : MonoSingleton<GameWorldManager>
    {
        [Header("生成设置")]
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private GameObject monsterPrefab;
        [SerializeField] private Transform playerSpawnPoint;
        [SerializeField] private Transform monsterSpawnPoint;

        [Header("地图设置")]
        [SerializeField] private float mapSize = 50f;
        [SerializeField] private Color groundColor = new Color(0.1f, 0.12f, 0.15f);

        private GameObject _playerInstance;
        private readonly System.List<GameObject> _monsterInstances = new System.List<GameObject>();

        protected override void OnSingletonAwake()
        {
            GenerateWorld();
        }

        /// <summary>生成游戏世界。</summary>
        public void GenerateWorld()
        {
            GenerateGround();
            SpawnPlayer();
            SpawnInitialMonsters();
            Debug.Log("[GameWorld] 世界生成完成！");
        }

        /// <summary>生成地面。</summary>
        private void GenerateGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(mapSize / 10f, 1f, mapSize / 10f);

            var renderer = ground.GetComponent<MeshRenderer>();
            renderer.material.color = groundColor;
        }

        /// <summary>生成玩家。</summary>
        private void SpawnPlayer()
        {
            if (playerPrefab == null)
            {
                // 创建临时玩家
                _playerInstance = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                _playerInstance.name = "Player";
                _playerInstance.transform.position = playerSpawnPoint != null ? playerSpawnPoint.position : Vector3.zero;
                _playerInstance.transform.localScale = new Vector3(1f, 1.5f, 1f);

                var renderer = _playerInstance.GetComponent<MeshRenderer>();
                renderer.material.color = Color.cyan;
            }
            else
            {
                _playerInstance = Instantiate(playerPrefab, playerSpawnPoint != null ? playerSpawnPoint.position : Vector3.zero, Quaternion.identity);
            }
        }

        /// <summary>生成初始怪物。</summary>
        private void SpawnInitialMonsters()
        {
            for (var i = 0; i < 5; i++)
            {
                SpawnMonster();
            }
        }

        /// <summary>生成单个怪物。</summary>
        public GameObject SpawnMonster()
        {
            var pos = monsterSpawnPoint != null ? monsterSpawnPoint.position : Vector3.zero;
            // 随机偏移
            pos += new Vector3(
                Random.Range(-10f, 10f),
                0f,
                Random.Range(-10f, 10f)
            );

            GameObject monster;
            if (monsterPrefab == null)
            {
                monster = GameObject.CreatePrimitive(PrimitiveType.Cphere);
                monster.name = $"Monster_{_monsterInstances.Count}";
                monster.transform.position = pos;
                monster.transform.localScale = Vector3.one * 0.8f;

                var renderer = monster.GetComponent<MeshRenderer>();
                renderer.material.color = Color.red;
            }
            else
            {
                monster = Instantiate(monsterPrefab, pos, Quaternion.identity);
            }

            _monsterInstances.Add(monster);
            return monster;
        }
    }
}
