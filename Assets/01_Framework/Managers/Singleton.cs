using System;
using UnityEngine;

namespace StarPixelSurge.Framework
{
    /// <summary>
    /// 全局唯一 MonoBehaviour 单例基类。首次访问时若场景内不存在实例则自动创建并跨场景保留。
    /// </summary>
    public abstract class MonoSingleton<T> : MonoBehaviour where T : MonoSingleton<T>
    {
        private static T _instance;
        private static readonly object _lock = new object();
        private static bool _quitting;

        public static T Instance
        {
            get
            {
                if (_quitting)
                {
                    return null;
                }

                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = FindObjectOfType<T>();
                        if (_instance == null)
                        {
                            var go = new GameObject($"[{typeof(T).Name}]");
                            _instance = go.AddComponent<T>();
                        }
                    }

                    return _instance;
                }
            }
        }

        public static bool HasInstance => _instance != null;

        protected virtual void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = (T)this;
            if (transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }

            OnSingletonAwake();
        }

        /// <summary>实例就绪后的初始化回调，替代在 Awake 中的业务初始化。</summary>
        protected virtual void OnSingletonAwake()
        {
        }

        protected virtual void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        protected virtual void OnApplicationQuit()
        {
            _quitting = true;
        }
    }

    /// <summary>
    /// 非 MonoBehaviour 的纯逻辑单例，用于无需挂载到场景的服务类。
    /// </summary>
    public abstract class LazySingleton<T> where T : class, new()
    {
        private static readonly Lazy<T> LazyInstance = new Lazy<T>(() => new T());

        public static T Instance => LazyInstance.Value;
    }
}
