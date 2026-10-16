using System.Collections.Generic;
using StarPixelSurge.Config;
using StarPixelSurge.Framework;
using StarPixelSurge.GameCore;
using StarPixelSurge.Map;
using UnityEngine;

namespace StarPixelSurge.Network
{
    /// <summary>
    /// 玩家分身注册表。房主在此为每位接入玩家建立权威分身并统一推进；
    /// 客户端仅登记本地分身（位置由房主快照驱动），远端玩家由实体同步层渲染。
    /// </summary>
    public class NetworkPlayerRegistry : MonoSingleton<NetworkPlayerRegistry>
    {
        private readonly Dictionary<int, NetworkPlayerAvatar> _avatars = new Dictionary<int, NetworkPlayerAvatar>();
        private readonly List<NetworkPlayerAvatar> _order = new List<NetworkPlayerAvatar>();

        private Transform _container;
        private RoomMap _map;

        /// <summary>本地玩家分身。</summary>
        public NetworkPlayerAvatar LocalAvatar { get; private set; }

        public IReadOnlyList<NetworkPlayerAvatar> Avatars => _order;

        public int Count => _order.Count;

        private Transform Container
        {
            get
            {
                if (_container == null)
                {
                    var go = new GameObject("~NetworkAvatars");
                    go.transform.SetParent(transform, false);
                    _container = go.transform;
                }

                return _container;
            }
        }

        public void SetMap(RoomMap map)
        {
            _map = map;
            for (var i = 0; i < _order.Count; i++)
            {
                _order[i].SetMap(map);
            }
        }

        /// <summary>
        /// 将本机现有玩家对象登记为本地分身。
        /// 房主自身分身 <paramref name="authoritative"/> 为 true，客户端为 false。
        /// </summary>
        public NetworkPlayerAvatar RegisterLocal(GameObject localPlayer, int localPlayerId, bool authoritative)
        {
            if (localPlayer == null)
            {
                return null;
            }

            // 本机玩家改由 NetworkPlayerAvatar 统一登记网络 ID（PlayerId），
            // 移除自动分配 ID 的绑定器，避免同一玩家在快照中出现两次。
            var binder = localPlayer.GetComponent<NetworkEntityBinder>();
            if (binder != null)
            {
                binder.enabled = false;
                Destroy(binder);
            }

            var avatar = localPlayer.GetComponent<NetworkPlayerAvatar>();
            if (avatar == null)
            {
                avatar = localPlayer.AddComponent<NetworkPlayerAvatar>();
            }

            avatar.Configure(localPlayerId, authoritative, true, _map);
            Track(avatar);
            LocalAvatar = avatar;
            return avatar;
        }

        /// <summary>房主为远端客户端创建权威分身。重复调用返回既有分身。</summary>
        public NetworkPlayerAvatar SpawnRemote(int playerId, string classId, Vector2 position)
        {
            if (_avatars.TryGetValue(playerId, out var existing) && existing != null)
            {
                return existing;
            }

            var go = new GameObject($"PlayerAvatar_{playerId}");
            go.transform.SetParent(Container, false);
            go.transform.position = new Vector3(position.x, position.y, 0f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = CreateSquare(new Color(0.45f, 0.85f, 1f), 8);
            renderer.sortingOrder = 10;

            var collider = go.AddComponent<CircleCollider2D>();
            collider.radius = 0.35f;

            var stats = go.AddComponent<PlayerStats>();
            var classConfig = ResolveClass(classId);
            stats.Initialize(classConfig);
            stats.SetMatchFatal(false);

            var avatar = go.AddComponent<NetworkPlayerAvatar>();
            avatar.Configure(playerId, true, false, _map);
            Track(avatar);
            return avatar;
        }

        public NetworkPlayerAvatar Get(int playerId)
        {
            return _avatars.TryGetValue(playerId, out var avatar) ? avatar : null;
        }

        /// <summary>房主推进所有权威分身位置。</summary>
        public void Tick(float deltaTime)
        {
            for (var i = 0; i < _order.Count; i++)
            {
                var avatar = _order[i];
                if (avatar != null && avatar.IsAuthoritative)
                {
                    avatar.Tick(deltaTime);
                }
            }
        }

        /// <summary>取得距指定位置最近的存活分身，用于怪物索敌。</summary>
        public NetworkPlayerAvatar GetClosest(Vector2 origin)
        {
            NetworkPlayerAvatar best = null;
            var bestDistance = float.MaxValue;
            for (var i = 0; i < _order.Count; i++)
            {
                var avatar = _order[i];
                if (avatar == null || avatar.Stats == null || avatar.Stats.IsDead)
                {
                    continue;
                }

                var distance = ((Vector2)avatar.transform.position - origin).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = avatar;
                }
            }

            return best;
        }

        public void Clear()
        {
            for (var i = 0; i < _order.Count; i++)
            {
                var avatar = _order[i];
                if (avatar == null || avatar == LocalAvatar)
                {
                    continue;
                }

                Destroy(avatar.gameObject);
            }

            _avatars.Clear();
            _order.Clear();
            LocalAvatar = null;
            _map = null;
        }

        private void Track(NetworkPlayerAvatar avatar)
        {
            if (avatar == null)
            {
                return;
            }

            _avatars[avatar.PlayerId] = avatar;
            if (!_order.Contains(avatar))
            {
                _order.Add(avatar);
            }
        }

        private static ClassConfig ResolveClass(string classId)
        {
            if (!ConfigService.HasInstance)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(classId))
            {
                var config = ConfigService.Instance.GetClass(classId);
                if (config != null)
                {
                    return config;
                }
            }

            foreach (var config in ConfigService.Instance.GetAllClasses())
            {
                return config;
            }

            return null;
        }

        private static Sprite CreateSquare(Color color, int size)
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
