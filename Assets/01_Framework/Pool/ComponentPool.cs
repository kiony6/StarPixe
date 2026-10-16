using System;
using System.Collections.Generic;
using UnityEngine;

namespace StarPixelSurge.Framework
{
    /// <summary>
    /// 泛型组件对象池。以工厂委托创建实例，未激活实例挂载于池根节点下。
    /// </summary>
    public class ComponentPool<T> where T : Component
    {
        private readonly Func<T> _factory;
        private readonly Stack<T> _available = new Stack<T>();
        private readonly HashSet<T> _active = new HashSet<T>();
        private readonly Transform _root;
        private readonly int _maxSize;

        public int CountAll => _available.Count + _active.Count;

        public int CountActive => _active.Count;

        public int CountAvailable => _available.Count;

        public ComponentPool(Func<T> factory, Transform root = null, int prewarm = 0, int maxSize = 0)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _root = root;
            _maxSize = maxSize;

            for (var i = 0; i < prewarm; i++)
            {
                var item = Create();
                item.gameObject.SetActive(false);
                _available.Push(item);
            }
        }

        public T Get()
        {
            T item;
            if (_available.Count > 0)
            {
                item = _available.Pop();
            }
            else if (_maxSize <= 0 || CountAll < _maxSize)
            {
                item = Create();
            }
            else
            {
                return null;
            }

            _active.Add(item);
            item.gameObject.SetActive(true);
            if (item is IPoolable poolable)
            {
                poolable.OnSpawn();
            }

            return item;
        }

        public void Release(T item)
        {
            if (item == null || !_active.Remove(item))
            {
                return;
            }

            if (item is IPoolable poolable)
            {
                poolable.OnDespawn();
            }

            item.gameObject.SetActive(false);
            if (_root != null)
            {
                item.transform.SetParent(_root, false);
            }

            _available.Push(item);
        }

        public void ReleaseAll()
        {
            var snapshot = new List<T>(_active);
            foreach (var item in snapshot)
            {
                Release(item);
            }
        }

        private T Create()
        {
            var item = _factory();
            if (_root != null)
            {
                item.transform.SetParent(_root, false);
            }

            return item;
        }
    }
}
