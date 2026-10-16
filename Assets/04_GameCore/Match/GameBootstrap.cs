using StarPixelSurge.Config;
using StarPixelSurge.Framework;
using StarPixelSurge.UI;
using UnityEngine;

namespace StarPixelSurge.GameCore
{
    /// <summary>
    /// 对局启动入口。装配玩家、技能、波次、UI 并开始一场单机无尽对局。
    /// 未在 Inspector 指定预制体时自动构建运行时占位对象，保证空场景可直接运行。
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        public static GameBootstrap Instance { get; private set; }

        private const string DefaultClassId = "thunder_mage";
        private const string DefaultSkillId = "lightning_orb";

        [SerializeField] private PlayerController playerPrefab;
        [SerializeField] private GameObject monsterPrefab;
        [SerializeField] private string startingClassId;
        [SerializeField] private string startingSkillId;
        [SerializeField] private bool autoStartOnPlay = true;
        [SerializeField] private bool buildGameplayUi = true;

        private GameObject _runtimeMonsterTemplate;
        private PlayerController _player;
        private PlayerStats _playerStats;

        public PlayerStats PlayerStats => _playerStats;

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
            if (autoStartOnPlay)
            {
                StartSinglePlayer();
            }
        }

        public void StartSinglePlayer()
        {
            if (PoolManager.HasInstance)
            {
                PoolManager.Instance.DespawnAll();
            }

            var classId = ResolveClassId();

            if (_player == null)
            {
                _player = CreatePlayer();
            }

            _player.transform.position = Vector3.zero;
            _playerStats = _player.GetComponent<PlayerStats>();

            var classConfig = ConfigService.Instance.GetClass(classId);
            _playerStats?.Initialize(classConfig);
            _player.GetComponent<PlayerExperience>()?.ResetProgress();

            EnsureUi();

            var seed = Random.Range(int.MinValue, int.MaxValue);
            GameManager.Instance.StartMatch(MatchConfig.SinglePlayer(classId, seed));

            SkillManager.Instance.ResetAll();
            SkillManager.Instance.SetPlayer(_player.transform);
            var skillId = ResolveStartingSkill(classConfig);
            if (!string.IsNullOrEmpty(skillId))
            {
                SkillManager.Instance.GrantSkill(skillId);
            }

            UpgradeManager.Instance.BindPlayer(_playerStats);

            WaveManager.Instance.SetPlayer(_player.transform);
            WaveManager.Instance.SetMonsterPrefab(ResolveMonsterPrefab());
            WaveManager.Instance.StartWaves();
        }

        /// <summary>重开一局：清空对象池与波次，重置技能后从头开始。</summary>
        public void RestartMatch()
        {
            WaveManager.Instance.StopWaves();
            StartSinglePlayer();
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
            collider.radius = 0.4f;

            go.AddComponent<PlayerStats>();
            go.AddComponent<PlayerExperience>();
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
