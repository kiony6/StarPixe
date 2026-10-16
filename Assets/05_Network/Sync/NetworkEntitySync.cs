using System;
using System.Collections.Generic;
using StarPixelSurge.Framework;
using UnityEngine;

namespace StarPixelSurge.Network
{
    /// <summary>
    /// 可参与帧同步的实体契约。绑定组件实现该接口后即可被快照采集。
    /// </summary>
    public interface INetworkEntity
    {
        int NetworkId { get; }

        EntityType NetworkType { get; }

        float NetworkHealth { get; }

        EntityStateFlags NetworkFlags { get; }

        Transform NetworkTransform { get; }
    }

    /// <summary>
    /// 实体同步桥接层。房主侧可将注册实体的权威状态打包为快照；
    /// 客户端侧据插值后的快照渲染远端实体视图。
    /// </summary>
    public class NetworkEntitySync : MonoSingleton<NetworkEntitySync>
    {
        private readonly Dictionary<int, INetworkEntity> _local = new Dictionary<int, INetworkEntity>();
        private readonly Dictionary<int, GameObject> _views = new Dictionary<int, GameObject>();
        private readonly List<EntityState> _snapshotScratch = new List<EntityState>();
        private readonly HashSet<int> _seen = new HashSet<int>();
        private readonly List<int> _stale = new List<int>();
        private int _nextId = MonsterIdStart;

        /// <summary>
        /// 玩家实体使用其 PlayerId（0-3）作为网络 ID，因此非玩家实体的 ID
        /// 从该值起分配，避免与玩家 ID 空间冲突。
        /// </summary>
        public const int MonsterIdStart = 100;

        /// <summary>是否为玩家实体 ID（玩家 ID 空间为 0 至 MonsterIdStart-1）。</summary>
        public static bool IsPlayerEntityId(int entityId) => entityId >= 0 && entityId < MonsterIdStart;

        /// <summary>客户端是否渲染远端实体视图。</summary>
        public bool RenderRemoteEntities { get; set; } = true;

        public int AllocateId()
        {
            return _nextId++;
        }

        public void RegisterLocal(INetworkEntity entity)
        {
            if (entity == null)
            {
                return;
            }

            _local[entity.NetworkId] = entity;
        }

        public void UnregisterLocal(int id)
        {
            _local.Remove(id);
        }

        public void Clear()
        {
            _local.Clear();
            foreach (var view in _views.Values)
            {
                if (view != null)
                {
                    Destroy(view);
                }
            }

            _views.Clear();
        }

        /// <summary>房主采集当前权威状态生成快照。</summary>
        public StateSnapshot BuildSnapshot(int frameIndex, int waveLayer, uint rngState)
        {
            _snapshotScratch.Clear();
            foreach (var entity in _local.Values)
            {
                if (entity == null)
                {
                    continue;
                }

                var t = entity.NetworkTransform;
                if (t == null)
                {
                    continue;
                }

                var p = t.position;
                _snapshotScratch.Add(new EntityState(
                    entity.NetworkId,
                    entity.NetworkType,
                    new Vector2(p.x, p.y),
                    entity.NetworkHealth,
                    entity.NetworkFlags));
            }

            return new StateSnapshot(frameIndex, _snapshotScratch.ToArray(), waveLayer, rngState);
        }

        /// <summary>初始化房主快照提供源。</summary>
        public void BindAsProvider(FrameSyncService frame, Func<int> waveLayer, Func<uint> rngState)
        {
            if (frame == null)
            {
                return;
            }

            frame.SnapshotProvider = () => BuildSnapshot(
                frame.CurrentFrameIndex,
                waveLayer != null ? waveLayer() : 0,
                rngState != null ? rngState() : 0u);
        }

        private void Update()
        {
            if (!RenderRemoteEntities || !FrameSyncService.HasInstance)
            {
                return;
            }

            var frame = FrameSyncService.Instance;
            if (frame.Role != NetworkRole.Client)
            {
                return;
            }

            RenderRemote(frame.GetInterpolatedEntities());
        }

        private void RenderRemote(IReadOnlyList<EntityState> states)
        {
            _seen.Clear();

            for (var i = 0; i < states.Count; i++)
            {
                var state = states[i];
                if (_local.ContainsKey(state.EntityId))
                {
                    continue;
                }

                _seen.Add(state.EntityId);

                if (!_views.TryGetValue(state.EntityId, out var view) || view == null)
                {
                    view = CreateView(state);
                    _views[state.EntityId] = view;
                }

                view.transform.position = new Vector3(state.Position.x, state.Position.y, 0f);
                if (view.activeSelf != state.IsAlive)
                {
                    view.SetActive(state.IsAlive);
                }

                var renderer = view.GetComponent<SpriteRenderer>();
                if (renderer != null)
                {
                    renderer.color = ColorFor(state);
                }
            }

            _stale.Clear();
            foreach (var pair in _views)
            {
                if (!_seen.Contains(pair.Key))
                {
                    _stale.Add(pair.Key);
                }
            }

            for (var i = 0; i < _stale.Count; i++)
            {
                var id = _stale[i];
                if (_views.TryGetValue(id, out var view) && view != null)
                {
                    Destroy(view);
                }

                _views.Remove(id);
            }
        }

        private GameObject CreateView(EntityState state)
        {
            var go = new GameObject($"Remote_{state.Type}_{state.EntityId}");
            go.transform.SetParent(transform, false);
            go.transform.localScale = state.Type == EntityType.Player ? Vector3.one * 0.8f : Vector3.one * 0.7f;

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = CreateSquare(ColorFor(state), 8);
            renderer.sortingOrder = state.Type == EntityType.Player ? 9 : 5;
            return go;
        }

        private static Color ColorFor(EntityState state)
        {
            switch (state.Type)
            {
                case EntityType.Player:
                    return new Color(0.35f, 0.9f, 1f);
                case EntityType.Monster:
                    return (state.Flags & EntityStateFlags.Boss) != 0
                        ? new Color(1f, 0.5f, 0.15f)
                        : new Color(0.9f, 0.3f, 0.75f);
                case EntityType.Projectile:
                    return new Color(1f, 1f, 0.6f);
                default:
                    return Color.white;
            }
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
