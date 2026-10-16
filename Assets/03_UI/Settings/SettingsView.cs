using StarPixelSurge.Audio;
using StarPixelSurge.Framework;
using StarPixelSurge.SaveData;
using UnityEngine;
using UnityEngine.UI;

namespace StarPixelSurge.UI
{
    /// <summary>
    /// 设置界面：音量调节、昵称修改。
    /// </summary>
    public class SettingsView : UIView
    {
        private InputField _nameInput;
        private Slider _bgmSlider;
        private Slider _sfxSlider;
        private Button _saveButton;
        private Button _backButton;

        public void Build(Transform parent)
        {
            // 创建设置面板
            var panel = UiFactory.CreateImage(parent, "SettingsPanel", new Color(0.1f, 0.12f, 0.18f, 0.95f));
            panel.raycastTarget = true;
            contentRoot = panel.gameObject;
            UiFactory.Stretch(panel.rectTransform);

            // 标题
            var titleText = UiFactory.CreateText(panel.transform, "Title", "设置", 36, TextAnchor.MiddleCenter, new Color(0.4f, 0.8f, 1f));
            UiFactory.SetAnchoredRect(titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(400f, 50f));

            // 昵称输入
            var nameLabel = UiFactory.CreateText(panel.transform, "NameLabel", "玩家昵称:", 20, TextAnchor.MiddleLeft, Color.white);
            UiFactory.SetAnchoredRect(nameLabel.rectTransform, new Vector2(0.5f, 0.8f), new Vector2(0.5f, 0.8f), new Vector2(0.5f, 0.8f), new Vector2(-150f, 0f), new Vector2(150f, 30f));

            _nameInput = UiFactory.CreateInputField(panel.transform, "NameInput", "输入昵称", new Color(0.15f, 0.18f, 0.25f));
            UiFactory.SetAnchoredRect(_nameInput.GetComponent<RectTransform>(), new Vector2(0.5f, 0.8f), new Vector2(0.5f, 0.8f), new Vector2(0.5f, 0.8f), new Vector2(50f, 0f), new Vector2(250f, 36f));

            // BGM 音量
            var bgmLabel = UiFactory.CreateText(panel.transform, "BgmLabel", "BGM 音量:", 20, TextAnchor.MiddleLeft, Color.white);
            UiFactory.SetAnchoredRect(bgmLabel.rectTransform, new Vector2(0.5f, 0.65f), new Vector2(0.5f, 0.65f), new Vector2(0.5f, 0.65f), new Vector2(-150f, 0f), new Vector2(150f, 30f));

            _bgmSlider = UiFactory.CreateSlider(panel.transform, "BgmSlider", new Color(0.2f, 0.5f, 0.8f));
            UiFactory.SetAnchoredRect(_bgmSlider.GetComponent<RectTransform>(), new Vector2(0.5f, 0.65f), new Vector2(0.5f, 0.65f), new Vector2(0.5f, 0.65f), new Vector2(80f, 0f), new Vector2(200f, 24f));

            // SFX 音量
            var sfxLabel = UiFactory.CreateText(panel.transform, "SfxLabel", "音效音量:", 20, TextAnchor.MiddleLeft, Color.white);
            UiFactory.SetAnchoredRect(sfxLabel.rectTransform, new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.55f), new Vector2(-150f, 0f), new Vector2(150f, 30f));

            _sfxSlider = UiFactory.CreateSlider(panel.transform, "SfxSlider", new Color(0.2f, 0.7f, 0.4f));
            UiFactory.SetAnchoredRect(_sfxSlider.GetComponent<RectTransform>(), new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.55f), new Vector2(80f, 0f), new Vector2(200f, 24f));

            // 保存按钮
            _saveButton = UiFactory.CreateButton(panel.transform, "SaveButton", "保存", new Color(0.2f, 0.6f, 0.3f), OnSaveClicked);
            UiFactory.SetAnchoredRect(_saveButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.35f), new Vector2(0.5f, 0.35f), new Vector2(0.5f, 0.35f), new Vector2(-110f, 0f), new Vector2(100f, 40f));

            // 返回按钮
            _backButton = UiFactory.CreateButton(panel.transform, "BackButton", "返回", new Color(0.4f, 0.4f, 0.45f), OnBackClicked);
            UiFactory.SetAnchoredRect(_backButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.35f), new Vector2(0.5f, 0.35f), new Vector2(0.5f, 0.35f), new Vector2(110f, 0f), new Vector2(100f, 40f));

            RefreshData();
        }

        private void RefreshData()
        {
            if (SaveManager.Instance != null)
            {
                var data = SaveManager.Instance.Data;
                _nameInput.text = data.PlayerName;
                _bgmSlider.value = data.Settings.BgmVolume;
                _sfxSlider.value = data.Settings.SfxVolume;
            }
        }

        private void OnSaveClicked()
        {
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.SetPlayerName(_nameInput.text);
                SaveManager.Instance.Data.Settings.BgmVolume = _bgmSlider.value;
                SaveManager.Instance.Data.Settings.SfxVolume = _sfxSlider.value;
                SaveManager.Instance.Save();

                if (AudioManager.Instance != null)
                {
                    // 音量应用到 AudioManager
                }
            }

            UIManager.Instance.Close<SettingsView>();
            // 返回时重新打开暂停面板，保持游戏暂停状态
            var pause = UIManager.Instance.Get<PauseView>();
            if (pause != null)
            {
                pause.Open();
            }
        }

        private void OnBackClicked()
        {
            UIManager.Instance.Close<SettingsView>();
            // 返回时重新打开暂停面板，保持游戏暂停状态
            var pause = UIManager.Instance.Get<PauseView>();
            if (pause != null)
            {
                pause.Open();
            }
        }
    }
}
