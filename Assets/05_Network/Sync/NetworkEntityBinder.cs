using StarPixelSurge.Framework;
using StarPixelSurge.GameCore;
using UnityEngine;

namespace StarPixelSurge.Network
{
    /// <summary>
    /// 实体网络绑定。挂载于玩家或怪物上后自动注册进 <see cref="NetworkEntitySync"/>，
    /// 供房主采集快照；对象池复用时会重新分配网络 ID 并重注册。
    /// </summary>
    public class NetworkEntityBinder : MonoBehaviour, INetworkEntity, IPoolable
    {
        private MonsterController _monster;
        private PlayerStats _player;
        private int _id;
        private bool _registered;

        public int NetworkId => _id;

        public Transform NetworkTransform => transform;

        public EntityType NetworkType => _monster != null ? EntityType.Monster : EntityType.Player;

        public float NetworkHealth
        {
            get
            {
                if (_monster != null)
                {
                    return _monster.Health;
                }

                return _player != null ? _player.CurrentHealth : 0f;
            }
        }

        public EntityStateFlags NetworkFlags
        {
            get
            {
                if (_monster != null)
                {
                    var flags = EntityStateFlags.None;
                    if (_monster.IsAlive)
                    {
                        flags |= EntityStateFlags.Alive;
                    }

                    if (_monster.IsBoss)
                    {
                        flags |= EntityStateFlags.Boss;
                    }

                    if (_monster.IsEnraged)
                    {
                        flags |= EntityStateFlags.Enraged;
                    }

                    return flags;
                }

                if (_player != null)
                {
                    return _player.IsDead ? EntityStateFlags.None : EntityStateFlags.Alive;
                }

                return EntityStateFlags.Alive;
            }
        }

        private void Awake()
        {
            Cache();
        }

        private void OnEnable()
        {
            Cache();
            Register();
        }

        private void OnDisable()
        {
            Unregister();
        }

        public void OnSpawn()
        {
            Cache();
            Unregister();
            _id = NetworkEntitySync.Instance.AllocateId();
            Register();
        }

        public void OnDespawn()
        {
            Unregister();
        }

        private void Cache()
        {
            if (_monster == null)
            {
                _monster = GetComponent<MonsterController>();
            }

            if (_player == null)
            {
                _player = GetComponent<PlayerStats>();
            }
        }

        private void Register()
        {
            if (_registered)
            {
                return;
            }

            if (_id <= 0)
            {
                _id = NetworkEntitySync.Instance.AllocateId();
            }

            NetworkEntitySync.Instance.RegisterLocal(this);
            _registered = true;
        }

        private void Unregister()
        {
            if (!_registered)
            {
                return;
            }

            try
            {
                if (NetworkEntitySync.HasInstance && _id > 0)
                {
                    NetworkEntitySync.Instance.UnregisterLocal(_id);
                }
            }
            catch
            {
                // 场景切换时忽略销毁顺序导致的空引用
            }

            _registered = false;
        }
    }
}
