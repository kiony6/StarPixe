using StarPixelSurge.Config;
using StarPixelSurge.Framework;
using UnityEngine;

namespace StarPixelSurge.GameCore
{
    /// <summary>
    /// 对局启动入口。装配玩家、技能、波次并开始一场单机无尽对局。
    /// 未在 Inspector 指定预制体时自动构建运行时占位对象，保证空场景可直接运行。
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private PlayerController playerPrefab;
        [SerializeField] private GameObject monsterPrefab;
        [SerializeField] private string startingClassId;
        [SerializeField] private string startingSkillId;
        [SerializeField] private bool autoStartOnPlay = true;

        private GameObject _runtimeMonsterTemplate;

        private void Start()
        {
            if (autoStartOnPlay)
            {
                StartSinglePlayer();
            }
        }

        public void StartSinglePlayer()
        {
            var seed = Random.Range(int.MinValue, int.MaxValue);
            GameManager.Instance.StartMatch(MatchConfig.SinglePlayer(startingClassId, seed));

            var player = CreatePlayer();
            var stats = player.GetComponent<PlayerStats>();

            var classConfig = ConfigService.Instance.GetClass(startingClassId);
            stats?.Initialize(classConfig);

            SkillManager.Instance.ResetAll();
            SkillManager.Instance.SetPlayer(player.transform);
            if (!string.IsNullOrEmpty(startingSkillId))
            {
                SkillManager.Instance.GrantSkill(startingSkillId);
            }

            UpgradeManager.Instance.BindPlayer(stats);

            WaveManager.Instance.SetPlayer(player.transform);
            WaveManager.Instance.SetMonsterPrefab(ResolveMonsterPrefab());
            WaveManager.Instance.StartWaves();
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
