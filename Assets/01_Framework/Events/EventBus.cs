using System;
using System.Collections.Generic;

namespace StarPixelSurge.Framework
{
    /// <summary>
    /// 类型化全局事件总线，用于模块间解耦通信。
    /// </summary>
    public static class EventBus
    {
        private static readonly Dictionary<Type, Delegate> Handlers = new Dictionary<Type, Delegate>();

        public static void Subscribe<T>(Action<T> handler)
        {
            if (handler == null)
            {
                return;
            }

            var type = typeof(T);
            if (Handlers.TryGetValue(type, out var existing))
            {
                Handlers[type] = Delegate.Combine(existing, handler);
            }
            else
            {
                Handlers[type] = handler;
            }
        }

        public static void Unsubscribe<T>(Action<T> handler)
        {
            if (handler == null)
            {
                return;
            }

            var type = typeof(T);
            if (!Handlers.TryGetValue(type, out var existing))
            {
                return;
            }

            var updated = Delegate.Remove(existing, handler);
            if (updated == null)
            {
                Handlers.Remove(type);
            }
            else
            {
                Handlers[type] = updated;
            }
        }

        public static void Publish<T>(T evt)
        {
            if (Handlers.TryGetValue(typeof(T), out var handler) && handler is Action<T> action)
            {
                action.Invoke(evt);
            }
        }

        public static void Clear()
        {
            Handlers.Clear();
        }
    }
}
