using StarPixelSurge.Framework;
using StarPixelSurge.GameCore;
using UnityEngine;
using UnityEngine.UI;

namespace StarPixelSurge.UI
{
    /// <summary>
    /// ESC 暂停面板：对局中暂停，提供继续、设置、返回主菜单选项。
    /// </summary>
    public class PauseView : UIView
    {
        private Button _resumeButton;
        private Button _settingsButton;
        private Button _quitButton;

        private static readonly Color PanelColor = new Color(0.06f, 0.07f, 0.12f, 0.95f);

        public void Build(Transform parent)
        {
            var panel = UiFactory.CreateImage(parent, "PausePanel", PanelColor);
            panel.raycastTarget = true;
            contentRoot = panel.gameObject;
            UiFactory.Stretch(panel.rectTransform);

            // 标题
            var titleText = UiFactory.CreateNeonText(panel.transform, "Title", "游戏暂停", 48, TextAnchor.MiddleCenter, new Color(0.4f, 0.8f, 1f), 4f);
            titleText.rectTransform.anchorMin = new Vector2(0.5f, 0.8f);
            titleText.rectTransform.anchorMax = new Vector2(0.5f, 0.8f);
            titleText.rectTransform.sizeDelta = new Vector2(400f, 60f);
            titleText.rectTransform.anchoredPosition = Vector2.zero;

            // 继续游戏
            _resumeButton = UiFactory.CreateButton(panel.transform, "ResumeButton", "继续游戏", new Color(0.2f, 0.6f, 0.3f), OnResume);
            _resumeButton.GetComponent<RectTransform>().anchorMin = new Vector2(0.5f, 0.6f);
            _resumeButton.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 0.6f);
            _resumeButton.GetComponent<RectTransform>().sizeDelta = new Vector2(240f, 50f);
            _resumeButton.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;

            // 设置
            _settingsButton = UiFactory.CreateButton(panel.transform, "SettingsButton", "设置", new Color(0.4f, 0.4f, 0.45f), OnSettings);
            _settingsButton.GetComponent<RectTransform>().anchorMin = new Vector2(0.5f, 0.48f);
            _settingsButton.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 0.48f);
            _settingsButton.GetComponent<RectTransform>().sizeDelta = new Vector2(240f, 50f);
            _settingsButton.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;

            // 放弃本局
            _quitButton = UiFactory.CreateButton(panel.transform, "QuitButton", "放弃本局", new Color(0.7f, 0.25f, 0.25f), OnQuit);
            _quitButton.GetComponent<RectTransform>().anchorMin = new Vector2(0.5f, 0.36f);
            _quitButton.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 0.36f);
            _quitButton.GetComponent<RectTransform>().sizeDelta = new Vector2(240f, 50f);
            _quitButton.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;

            OnClose();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (IsOpen)
                {
                    OnResume();
                }
                else
                {
                    Open();
                }
            }
        }

        public void Open()
        {
            OnOpen();
            Time.timeScale = 0f;
        }

        private void OnResume()
        {
            OnClose();
            Time.timeScale = 1f;
        }

        private void OnSettings()
        {
            // 根本原因：PauseView 打开时没走 UIManager.Open，导致层级不对
            // 彻底解决：先关闭暂停面板，再打开设置，避免层级冲突
            OnClose();
            UIManager.Instance.Open<SettingsView>();
            // 保持游戏暂停状态
            Time.timeScale = 0f;
        }

        private void OnQuit()
        {
            Time.timeScale = 1f;
            OnClose();
            if (GameBootstrap.Instance != null)
            {
                GameBootstrap.Instance.EnterLobby();
            }
        }
    }
}
