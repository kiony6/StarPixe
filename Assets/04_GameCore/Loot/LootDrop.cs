using StarPixelSurge.Config;
using StarPixelSurge.Framework;
using UnityEngine;

namespace StarPixelSurge.GameCore
{
    /// <summary>
    /// 地面掉落物。玩家进入拾取范围后自动结算词缀加成并归还对象池。
    /// </summary>
    public class LootDrop : MonoBehaviour, IPoolable
    {
        [SerializeField] private float bobAmplitude = 0.14f;
        [SerializeField] private float bobSpeed = 3f;
        [SerializeField] private float lifetime = 40f;

        private AffixConfig _affix;
        private SpriteRenderer _renderer;
        private Vector3 _origin;
        private float _life;
        private float _phase;
        private bool _collected;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
        }

        public void Configure(AffixConfig affix)
        {
            _affix = affix;
            _collected = false;
            _life = lifetime;
            _origin = transform.position;
            _phase = Random.Range(0f, Mathf.PI * 2f);

            if (_renderer == null)
            {
                _renderer = GetComponent<SpriteRenderer>();
            }

            if (_renderer != null)
            {
                var color = ResolveColor(affix);
                _renderer.sprite = SpriteCatalog.Resolve("loot", ResolveId(affix), color, ResolveShape(affix), 10);
                _renderer.color = Color.white;
            }
        }

        private void Update()
        {
            if (_collected)
            {
                return;
            }

            _life -= Time.deltaTime;
            if (_life <= 0f)
            {
                Despawn();
                return;
            }

            transform.position = _origin + new Vector3(0f, Mathf.Sin(Time.time * bobSpeed + _phase) * bobAmplitude, 0f);

            var player = PlayerController.Instance;
            if (player == null)
            {
                return;
            }

            var stats = player.GetComponentInParent<PlayerStats>();
            var range = stats != null ? Mathf.Max(0.5f, stats.PickupRange) : 1.5f;
            if (((Vector2)player.transform.position - (Vector2)transform.position).sqrMagnitude <= range * range)
            {
                Collect(stats);
            }
        }

        private void Collect(PlayerStats stats)
        {
            _collected = true;
            stats?.ApplyAffix(_affix);

            if (_affix != null)
            {
                EventBus.Publish(new LootPickedEvent(_affix.DisplayName, _affix.Description, ResolveColor(_affix)));
            }

            Despawn();
        }

        private static Color ResolveColor(AffixConfig affix)
        {
            return affix == null ? Color.white : NeonPalette.Rarity((int)affix.Rarity);
        }

        private static string ResolveId(AffixConfig affix)
        {
            return affix == null ? "common" : "affix_" + affix.Rarity.ToString().ToLowerInvariant();
        }

        /// <summary>按稀有度区分掉落物形状：普通方形、稀有条形环、史诗五角星。</summary>
        private static RuntimeSprite.Shape ResolveShape(AffixConfig affix)
        {
            if (affix == null)
            {
                return RuntimeSprite.Shape.Square;
            }

            switch (affix.Rarity)
            {
                case AffixRarity.Epic:
                    return RuntimeSprite.Shape.Star;
                case AffixRarity.Rare:
                    return RuntimeSprite.Shape.Ring;
                default:
                    return RuntimeSprite.Shape.Square;
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
            _affix = null;
            _collected = false;
            _life = lifetime;
            _origin = transform.position;
        }

        public void OnDespawn()
        {
            _affix = null;
            _collected = false;
        }
    }
}
