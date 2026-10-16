using StarPixelSurge.Config;
using StarPixelSurge.Framework;
using StarPixelSurge.Map;
using StarPixelSurge.Network;
using StarPixelSurge.UI;
using UnityEngine;

namespace StarPixelSurge.GameCore
{
    /// <summary>
    /// 对局启动入口。默认先进入职业选择大厅，选定职业后生成随机房间地图并开始一场单机无尽对局。
    /// 未在 Inspector 指定预制体或配置时自动构建运行时占位对象，保证空场景可直接运行。
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        public static GameBootstrap Instance { get; private set; }

        private const string DefaultClassId = "thunder_mage";
        private const string DefaultSkillId = "lightning_orb";
        private const string DefaultRoomMapResourcePath = "Config/RoomMapConfig_neon_city";

        [SerializeField] private PlayerController playerPrefab;
        [SerializeField] private GameObject monsterPrefab;
        [SerializeField] private RoomMapConfig roomMapConfig;
        [SerializeField] private string startingClassId;
        [SerializeField] private string startingSkillId;
        [SerializeField] private bool showClassSelection = true;
        [SerializeField] private bool autoStartOnPlay = true;
        [SerializeField] private bool buildGameplayUi = true;

        private GameObject _runtimeMonsterTemplate;
        private PlayerController _player;
        private PlayerStats _playerStats;
        private RoomMap _roomMap;
        private int _matchSeed;

        public PlayerStats PlayerStats => _playerStats;

        public RoomMap RoomMap => _roomMap;

        public string LastClassId { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Start()
        {
            if (!autoStartOnPlay)
            {
                return;
            }

            if (showClassSelection)
            {
                EnterLobby();
            }
            else
            {
                StartSinglePlayer();
            }
        }

        /// <summary>进入职业选择大厅。</summary>
        public void EnterLobby()
        {
            WaveManager.Instance.StopWaves();

            if (PoolManager.HasInstance)
            {
                PoolManager.Instance.DespawnAll();
            }

            DestroyRoomMap();

            if (NavigationService.HasInstance)
            {
                NavigationService.Instance.ClearMap();
            }

            if (RoomDirector.HasInstance)
            {
                RoomDirector.Instance.ClearMap();
            }

            if (_player != null)
            {
                _player.gameObject.SetActive(false);
            }

            EnsureUi();
            GameplayUIRoot.Instance.ShowClassSelection();
            GameManager.Instance.ReturnToMainMenu();
        }

        public void StartSinglePlayer()
        {
            StartSinglePlayer(ResolveClassId());
        }

        public void StartSinglePlayer(string classId)
        {
            StartMatch(classId, Random.Range(int.MinValue, int.MaxValue), MatchMode.SinglePlayer, true);
        }

        /// <summary>
        /// 联机开局。房主与客户端使用同一随机种子构建地图；
        /// 仅房主启动波次模拟，客户端等待快照驱动。
        /// </summary>
        public void StartMultiplayer(int seed, bool isHost, string classId = null)
        {
            StartMatch(classId, seed, MatchMode.Multiplayer, isHost);
        }

        private void StartMatch(string classId, int seed, MatchMode mode, bool runSimulation)
        {
            if (string.IsNullOrEmpty(classId))
            {
                classId = ResolveClassId();
            }

            LastClassId = classId;

            if (PoolManager.HasInstance)
            {
                PoolManager.Instance.DespawnAll();
            }

            WaveManager.Instance.StopWaves();

            var classConfig = ConfigService.Instance.GetClass(classId);
            _matchSeed = seed;

            BuildRoomMap();

            if (_player == null)
            {
                _player = CreatePlayer();
            }

            _player.gameObject.SetActive(true);
            var spawn = _roomMap != null ? _roomMap.StartPosition : Vector2.zero;
            _player.transform.position = new Vector3(spawn.x, spawn.y, 0f);

            _playerStats = _player.GetComponent<PlayerStats>();
            _playerStats?.Initialize(classConfig);
            var experience = _player.GetComponent<PlayerExperience>();
            experience?.ResetProgress();

            EnsureUi();

            GameManager.Instance.StartMatch(new MatchConfig(mode, classId, _matchSeed));

            SkillManager.Instance.ResetAll();
            SkillManager.Instance.SetPlayer(_player.transform);
            var skillId = ResolveStartingSkill(classConfig);
            if (!string.IsNullOrEmpty(skillId))
            {
                SkillManager.Instance.GrantSkill(skillId);
            }

            UpgradeManager.Instance.BindPlayer(_playerStats);

            NavigationService.Instance.SetMap(_roomMap);
            RoomDirector.Instance.Configure(_roomMap, _playerStats, experience);

            WaveManager.Instance.SetPlayer(_player.transform);
            WaveManager.Instance.SetMonsterPrefab(ResolveMonsterPrefab());
            WaveManager.Instance.SetMap(_roomMap);

            // 客户端不本地生成波次，由房主快照驱动远端实体。
            if (runSimulation)
            {
                WaveManager.Instance.StartWaves();
            }
        }

        /// <summary>重开一局：使用上一次职业重新生成地图并开局。</summary>
        public void RestartMatch()
        {
            StartSinglePlayer(string.IsNullOrEmpty(LastClassId) ? ResolveClassId() : LastClassId);
        }

        private void BuildRoomMap()
        {
            DestroyRoomMap();

            var config = ResolveRoomMapConfig();
            if (config == null)
            {
                return;
            }

            _roomMap = MapGenerator.Generate(config, _matchSeed, null);        }

        private void DestroyRoomMap()
        {
            if (_roomMap != null)
            {
                Destroy(_roomMap.gameObject);
                _roomMap = null;
            }
        }

        private RoomMapConfig ResolveRoomMapConfig()
        {
            if (roomMapConfig != null)
            {
                return roomMapConfig;
            }

            var all = Resources.LoadAll<RoomMapConfig>("Config");
            if (all != null && all.Length > 0)
            {
                return all[0];
            }

            return Resources.Load<RoomMapConfig>(DefaultRoomMapResourcePath);
        }

        private void EnsureUi()
        {
            if (!buildGameplayUi)
            {
                return;
            }

            if (GameplayUIRoot.Instance == null)
            {
                var go = new GameObject("GameplayUI");
                go.AddComponent<GameplayUIRoot>();
            }
        }

        private string ResolveClassId()
        {
            if (!string.IsNullOrEmpty(startingClassId) && ConfigService.Instance.GetClass(startingClassId) != null)
            {
                return startingClassId;
            }

            if (ConfigService.Instance.GetClass(DefaultClassId) != null)
            {
                return DefaultClassId;
            }

            foreach (var classConfig in ConfigService.Instance.GetAllClasses())
            {
                return classConfig.Id;
            }

            return startingClassId;
        }

        private string ResolveStartingSkill(ClassConfig classConfig)
        {
            if (!string.IsNullOrEmpty(startingSkillId))
            {
                return startingSkillId;
            }

            if (classConfig != null && !string.IsNullOrEmpty(classConfig.StartingSkillId))
            {
                return classConfig.StartingSkillId;
            }

            if (ConfigService.Instance.GetSkill(DefaultSkillId) != null)
            {
                return DefaultSkillId;
            }

            foreach (var skill in ConfigService.Instance.GetAllSkills())
            {
                return skill.Id;
            }

            return null;
        }

        private PlayerController CreatePlayer()
        {
            if (playerPrefab != null)
            {
                return Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
            }

            var go = new GameObject("Player");
            go.transform.position = Vector3.zero;

            var spriteRenderer = go.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = RuntimeSprite.CreateSquare(Color.cyan);
            spriteRenderer.sortingOrder = 10;

            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;

            var collider = go.AddComponent<CircleCollider2D>();
            collider.radius = 0.35f;

            go.AddComponent<PlayerStats>();
            go.AddComponent<PlayerExperience>();
            go.AddComponent<NetworkEntityBinder>();
            return go.AddComponent<PlayerController>();
        }

        private GameObject ResolveMonsterPrefab()
        {
            if (monsterPrefab != null)
            {
                return monsterPrefab;
            }

            if (_runtimeMonsterTemplate != null)
            {
                return _runtimeMonsterTemplate;
            }

            var go = new GameObject("Monster_Runtime");
            var spriteRenderer = go.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = RuntimeSprite.CreateSquare(Color.magenta);
            spriteRenderer.sortingOrder = 5;

            var collider = go.AddComponent<CircleCollider2D>();
            collider.radius = 0.4f;

            go.AddComponent<MonsterController>();
            go.AddComponent<NetworkEntityBinder>();
            go.SetActive(false);
            _runtimeMonsterTemplate = go;
            return go;
        }
    }

    /// <summary>运行时占位贴图工具，仅在未配置美术资源时使用。</summary>
    internal static class RuntimeSprite
    {
        public static Sprite CreateSquare(Color color, int size = 8)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point
            };

            var pixels = new Color[size * size];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            texture.SetPixels(pixels);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
