using System;
using System.Collections.Generic;
using UnityEngine;

namespace StarPixelSurge.UI
{
    /// <summary>
    /// 界面视图基类。由 UIManager 注册与开关。
    /// </summary>
    public abstract class UIView : MonoBehaviour
    {
        public bool IsOpen { get; private set; }

        public virtual void OnOpen()
        {
            IsOpen = true;
            gameObject.SetActive(true);
        }

        public virtual void OnClose()
        {
            IsOpen = false;
            gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 界面与弹窗管理器。维护视图注册表与单一模态窗口约束。
    /// </summary>
    public class UIManager : Framework.MonoSingleton<UIManager>
    {
        private readonly Dictionary<Type, UIView> _views = new Dictionary<Type, UIView>();
        private UIView _activeModal;

        public T Register<T>(T view) where T : UIView
        {
            if (view == null)
            {
                return null;
            }

            _views[typeof(T)] = view;
            view.OnClose();
            return view;
        }

        public T Get<T>() where T : UIView
        {
            return _views.TryGetValue(typeof(T), out var view) ? view as T : null;
        }

        public void Open<T>() where T : UIView
        {
            if (_views.TryGetValue(typeof(T), out var view))
            {
                view.OnOpen();
            }
            else
            {
                Debug.LogWarning($"[UIManager] View not registered: {typeof(T).Name}");
            }
        }

        public void Close<T>() where T : UIView
        {
            if (_views.TryGetValue(typeof(T), out var view))
            {
                view.OnClose();
            }
        }

        public void PushModal<T>() where T : UIView
        {
            PushModal(Get<T>());
        }

        public void PushModal(UIView view)
        {
            if (view == null)
            {
                return;
            }

            if (_activeModal != null && _activeModal != view)
            {
                _activeModal.OnClose();
            }

            _activeModal = view;
            view.OnOpen();
        }

        public void PopModal()
        {
            if (_activeModal == null)
            {
                return;
            }

            _activeModal.OnClose();
            _activeModal = null;
        }

        public void CloseAll()
        {
            foreach (var view in _views.Values)
            {
                view.OnClose();
            }

            _activeModal = null;
        }
    }
}
