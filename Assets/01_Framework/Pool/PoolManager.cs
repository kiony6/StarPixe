using System.Collections.Generic;
using UnityEngine;

namespace StarPixelSurge.Framework
{
    /// <summary>
    /// 记录实例来源预制体，供 PoolManager 归还时定位所属池。
    /// </summary>
    public class PooledObject : MonoBehaviour
    {
        public GameObject PrefabKey;
    }

    /// <summary>
    /// 全局对象池管理器，按预制体维度维护怪物、技能、弹道、特效的复用池。
    /// </summary>
    public class PoolManager : MonoSingleton<PoolManager>
    {
        private class PrefabPool
        {
            public readonly Stack<GameObject> Available = new Stack<GameObject>();
            public readonly HashSet<GameObject> Active = new HashSet<GameObject>();

            public int CountAll => Available.Count + Active.Count;
        }

        private readonly Dictionary<GameObject, PrefabPool> _pools = new Dictionary<GameObject, PrefabPool>();
        private Transform _root;

        protected override void OnSingletonAwake()
        {
            _root = new GameObject("~Pool").transform;
            _root.SetParent(transform, false);
        }

        public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (prefab == null)
            {
                return null;
            }

            if (!_pools.TryGetValue(prefab, out var pool))
            {
                pool = new PrefabPool();
                _pools[prefab] = pool;
            }

            GameObject instance;
            if (pool.Available.Count > 0)
            {
                instance = pool.Available.Pop();
            }
            else
            {
                instance = Instantiate(prefab, _root);
                instance.name = prefab.name;
                var marker = instance.GetComponent<PooledObject>();
                if (marker == null)
                {
                    marker = instance.AddComponent<PooledObject>();
                }

                marker.PrefabKey = prefab;
            }

            instance.transform.SetParent(parent, false);
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.SetActive(true);
            pool.Active.Add(instance);

            foreach (var poolable in instance.GetComponentsInChildren<IPoolable>(true))
            {
                poolable.OnSpawn();
            }

            return instance;
        }

        public void Despawn(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            var marker = instance.GetComponent<PooledObject>();
            if (marker == null || marker.PrefabKey == null || !_pools.TryGetValue(marker.PrefabKey, out var pool))
            {
                Destroy(instance);
                return;
            }

            foreach (var poolable in instance.GetComponentsInChildren<IPoolable>(true))
            {
                poolable.OnDespawn();
            }

            if (!pool.Active.Remove(instance))
            {
                return;
            }

            instance.SetActive(false);
            instance.transform.SetParent(_root, false);
            pool.Available.Push(instance);
        }

        public void DespawnAll()
        {
            foreach (var pool in _pools.Values)
            {
                var snapshot = new List<GameObject>(pool.Active);
                foreach (var instance in snapshot)
                {
                    Despawn(instance);
                }
            }
        }
    }
}
