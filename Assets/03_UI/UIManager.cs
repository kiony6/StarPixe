using UnityEngine;
using System.Collections.Generic;
using StarPixelSurge.Framework;

namespace StarPixelSurge.UI
{
    /// <summary>
    /// UI管理器：界面弹窗、显示隐藏、层级管理。
    /// </summary>
    public class UIManager : MonoSingleton<UIManager>
    {
        private readonly Dictionary<string, GameObject> _uiPanels = new Dictionary<string, GameObject>();
        private readonly Stack<string> _panelStack = new Stack<string>();

        protected override void OnSingletonAwake()
        {
            Debug.Log("[UIManager] UI管理器初始化完成");
        }

        /// <summary>注册UI面板。</summary>
        public void RegisterPanel(string panelName, GameObject panel)
        {
            if (!_uiPanels.ContainsKey(panelName))
            {
                _uiPanels.Add(panelName, panel);
                panel.SetActive(false);
            }
        }

        /// <summary>显示UI面板。</summary>
        public void ShowPanel(string panelName)
        {
            if (_uiPanels.TryGetValue(panelName, out var panel))
            {
                panel.SetActive(true);
                _panelStack.Push(panelName);
                Debug.Log($"[UIManager] 显示面板: {panelName}");
            }
        }

        /// <summary>隐藏UI面板。</summary>
        public void HidePanel(string panelName)
        {
            if (_uiPanels.TryGetValue(panelName, out var panel))
            {
                panel.SetActive(false);
                Debug.Log($"[UIManager] 隐藏面板: {panelName}");
            }
        }

        /// <summary>隐藏最上层面板。</summary>
        public void HideTopPanel()
        {
            if (_panelStack.Count > 0)
            {
                var top = _panelStack.Pop();
                HidePanel(top);
            }
        }

        /// <summary>隐藏所有面板。</summary>
        public void HideAllPanels()
        {
            foreach (var panel in _uiPanels.Values)
            {
                panel.SetActive(false);
            }
            _panelStack.Clear();
        }
    }
}
