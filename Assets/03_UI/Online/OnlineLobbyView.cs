using System.Collections.Generic;
using StarPixelSurge.Framework;
using StarPixelSurge.Network;
using StarPixelSurge.Network.Services;
using UnityEngine;
using UnityEngine.UI;

namespace StarPixelSurge.UI
{
    /// <summary>
    /// 联机大厅界面：创建房间 / 加入房间。
    /// </summary>
    public class OnlineLobbyView : UIView
    {
        private InputField _roomNameInput;
        private InputField _joinCodeInput;
        private Text _statusText;
        private Button _createButton;
        private Button _joinButton;
        private Button _backButton;

        private static readonly Color BackdropColor = new Color(0.03f, 0.03f, 0.06f, 0.98f);
        private static readonly Color PanelColor = new Color(0.09f, 0.10f, 0.17f, 1f);
        private static readonly Color AccentColor = new Color(0.22f, 0.62f, 0.95f, 1f);
        private static readonly Color DangerColor = new Color(0.9f, 0.35f, 0.35f, 1f);

        public void Build(Transform parent)
        {
            var backdrop = UiFactory.CreateImage(parent, "OnlineLobbyBackdrop", BackdropColor);
            backdrop.raycastTarget = true;
            contentRoot = backdrop.gameObject;
            UiFactory.Stretch((RectTransform)contentRoot.GetComponent<RectTransform>());

            // 标题
            var title = UiFactory.CreateNeonText(backdrop.transform, "Title", "联机大厅", 56, TextAnchor.UpperCenter, NeonPalette.Cyan, 5f);
            UiFactory.SetAnchoredRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(800f, 70f));

            // 创建房间面板
            var createPanel = UiFactory.CreateNeonPanel(backdrop.transform, "CreateRoomPanel", PanelColor, AccentColor, 2f);
            UiFactory.SetAnchoredRect(createPanel.rectTransform, new Vector2(0.5f, 0.65f), new Vector2(0.5f, 0.65f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(500f, 240f));

            var createTitle = UiFactory.CreateText(createPanel.transform, "CreateTitle", "创建房间", 32, TextAnchor.UpperCenter, Color.white);
            UiFactory.SetAnchoredRect(createTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(400f, 40f));

            _roomNameInput = UiFactory.CreateInputField(createPanel.transform, "RoomNameInput", "输入房间名...", new Color(0.15f, 0.16f, 0.25f, 1f));
            UiFactory.SetAnchoredRect(_roomNameInput.GetComponent<RectTransform>(), new Vector2(0.5f, 0.7f), new Vector2(0.5f, 0.7f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(380f, 44f));
            _roomNameInput.text = "我的房间";

            _createButton = UiFactory.CreateButton(createPanel.transform, "CreateButton", "创建并开始", AccentColor, OnCreateRoom);
            UiFactory.SetAnchoredRect(_createButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.25f), new Vector2(0.5f, 0.25f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(380f, 52f));

            // 加入房间面板
            var joinPanel = UiFactory.CreateNeonPanel(backdrop.transform, "JoinRoomPanel", PanelColor, AccentColor, 2f);
            UiFactory.SetAnchoredRect(joinPanel.rectTransform, new Vector2(0.5f, 0.3f), new Vector2(0.5f, 0.3f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(500f, 200f));

            var joinTitle = UiFactory.CreateText(joinPanel.transform, "JoinTitle", "加入房间", 32, TextAnchor.UpperCenter, Color.white);
            UiFactory.SetAnchoredRect(joinTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(400f, 40f));

            _joinCodeInput = UiFactory.CreateInputField(joinPanel.transform, "JoinCodeInput", "输入房间加入码...", new Color(0.15f, 0.16f, 0.25f, 1f));
            UiFactory.SetAnchoredRect(_joinCodeInput.GetComponent<RectTransform>(), new Vector2(0.5f, 0.65f), new Vector2(0.5f, 0.65f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(380f, 44f));

            _joinButton = UiFactory.CreateButton(joinPanel.transform, "JoinButton", "加入房间", new Color(0.25f, 0.6f, 0.5f), OnJoinRoom);
            UiFactory.SetAnchoredRect(_joinButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.25f), new Vector2(0.5f, 0.25f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(380f, 52f));

            // 状态提示
            _statusText = UiFactory.CreateText(backdrop.transform, "Status", "", 24, TextAnchor.MiddleCenter, new Color(0.8f, 0.8f, 0.9f));
            UiFactory.SetAnchoredRect(_statusText.rectTransform, new Vector2(0.5f, 0.12f), new Vector2(0.5f, 0.12f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(800f, 40f));

            // 返回按钮
            _backButton = UiFactory.CreateButton(backdrop.transform, "BackButton", "返回", new Color(0.4f, 0.4f, 0.45f), OnBack);
            UiFactory.SetAnchoredRect(_backButton.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -30f), new Vector2(120f, 48f));

            OnClose();
        }

        private async void OnCreateRoom()
        {
            SetBusy(true, "正在创建房间...");

            var roomName = string.IsNullOrEmpty(_roomNameInput.text) ? "未命名房间" : _roomNameInput.text;
            var joinCode = await OnlineMatchmaker.CreateOnlineRoomAsync(roomName, 8);

            if (!string.IsNullOrEmpty(joinCode))
            {
                SetBusy(false, $"房间创建成功！加入码: {joinCode}");
                // TODO: 打开房间大厅界面
            }
            else
            {
                SetBusy(false, "创建房间失败，请检查网络后重试");
            }
        }

        private async void OnJoinRoom()
        {
            var code = _joinCodeInput.text.Trim().ToUpper();
            if (string.IsNullOrEmpty(code))
            {
                SetBusy(false, "请输入房间加入码");
                return;
            }

            SetBusy(true, "正在加入房间...");
            await OnlineMatchmaker.JoinOnlineRoomAsync(code);
            SetBusy(false, "加入房间成功");
            // TODO: 打开房间大厅界面
        }

        private void OnBack()
        {
            OnClose();
        }

        private void SetBusy(bool busy, string status)
        {
            _createButton.interactable = !busy;
            _joinButton.interactable = !busy;
            _statusText.text = status;
        }
    }
}
