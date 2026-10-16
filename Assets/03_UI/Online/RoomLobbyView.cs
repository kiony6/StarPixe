using System.Collections.Generic;
using System.Threading.Tasks;
using StarPixelSurge.Framework;
using StarPixelSurge.Network;
using StarPixelSurge.Network.Services;
using UnityEngine;
using UnityEngine.UI;

namespace StarPixelSurge.UI
{
    /// <summary>
    /// 房间大厅界面：显示玩家列表、房主开始游戏、离开房间。
    /// </summary>
    public class RoomLobbyView : UIView
    {
        private Text _roomCodeText;
        private Text _roomNameText;
        private RectTransform _playerListContainer;
        private Button _startButton;
        private Button _leaveButton;
        private Text _voiceStatusText;
        private ChatView _chatView;
        private Button _lockRoomButton;
        private Text _lockStatusText;
        private bool _isRoomLocked;

        private readonly List<GameObject> _playerRows = new List<GameObject>();

        private static readonly Color BackdropColor = new Color(0.03f, 0.03f, 0.06f, 0.98f);
        private static readonly Color PanelColor = new Color(0.09f, 0.10f, 0.17f, 1f);
        private static readonly Color AccentColor = new Color(0.22f, 0.62f, 0.95f, 1f);
        private static readonly Color HostColor = new Color(1f, 0.85f, 0.3f, 1f);
        private static readonly Color PlayerColor = new Color(0.75f, 0.78f, 0.9f, 1f);

        public void Build(Transform parent)
        {
            var backdrop = UiFactory.CreateImage(parent, "RoomLobbyBackdrop", BackdropColor);
            backdrop.raycastTarget = true;
            contentRoot = backdrop.gameObject;
            UiFactory.Stretch((RectTransform)contentRoot.GetComponent<RectTransform>());

            // 标题
            var title = UiFactory.CreateNeonText(backdrop.transform, "Title", "房间大厅", 48, TextAnchor.UpperCenter, NeonPalette.Cyan, 5f);
            UiFactory.SetAnchoredRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(800f, 60f));

            // 房间信息面板
            var infoPanel = UiFactory.CreateNeonPanel(backdrop.transform, "RoomInfoPanel", PanelColor, AccentColor, 2f);
            UiFactory.SetAnchoredRect(infoPanel.rectTransform, new Vector2(0.5f, 0.82f), new Vector2(0.5f, 0.82f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(600f, 100f));

            _roomNameText = UiFactory.CreateText(infoPanel.transform, "RoomName", "房间名", 28, TextAnchor.MiddleCenter, Color.white);
            UiFactory.SetAnchoredRect(_roomNameText.rectTransform, new Vector2(0.5f, 0.65f), new Vector2(0.5f, 0.65f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(500f, 36f));

            _roomCodeText = UiFactory.CreateText(infoPanel.transform, "RoomCode", "加入码: ------", 24, TextAnchor.MiddleCenter, new Color(0.7f, 0.85f, 1f));
            UiFactory.SetAnchoredRect(_roomCodeText.rectTransform, new Vector2(0.5f, 0.3f), new Vector2(0.5f, 0.3f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(500f, 32f));

            // 玩家列表面板
            var listPanel = UiFactory.CreateNeonPanel(backdrop.transform, "PlayerListPanel", PanelColor, AccentColor, 2f);
            UiFactory.SetAnchoredRect(listPanel.rectTransform, new Vector2(0.5f, 0.48f), new Vector2(0.5f, 0.48f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(600f, 320f));

            var listTitle = UiFactory.CreateText(listPanel.transform, "ListTitle", "玩家列表", 28, TextAnchor.UpperCenter, Color.white);
            UiFactory.SetAnchoredRect(listTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(500f, 36f));

            _playerListContainer = UiFactory.CreateRect(listPanel.transform, "PlayerList");
            UiFactory.SetAnchoredRect(_playerListContainer, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(540f, 250f));

            var layout = _playerListContainer.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.padding = new RectOffset(16, 16, 16, 16);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;

            // 语音状态
            _voiceStatusText = UiFactory.CreateText(backdrop.transform, "VoiceStatus", "🎤 小队语音已连接", 24, TextAnchor.MiddleCenter, new Color(0.5f, 0.9f, 0.6f));
            UiFactory.SetAnchoredRect(_voiceStatusText.rectTransform, new Vector2(0.5f, 0.22f), new Vector2(0.5f, 0.22f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(500f, 36f));

            // 按钮区
            _startButton = UiFactory.CreateButton(backdrop.transform, "StartButton", "开始游戏", AccentColor, OnStartGame);
            UiFactory.SetAnchoredRect(_startButton.GetComponent<RectTransform>(), new Vector2(0.35f, 0.1f), new Vector2(0.35f, 0.1f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(220f, 56f));

            _leaveButton = UiFactory.CreateButton(backdrop.transform, "LeaveButton", "离开房间", new Color(0.8f, 0.4f, 0.4f), OnLeaveRoom);
            UiFactory.SetAnchoredRect(_leaveButton.GetComponent<RectTransform>(), new Vector2(0.65f, 0.1f), new Vector2(0.65f, 0.1f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(220f, 56f));

            // 锁房间按钮（仅房主可见）
            _lockRoomButton = UiFactory.CreateButton(backdrop.transform, "LockRoomButton", "锁定房间", new Color(0.6f, 0.5f, 0.2f), OnLockRoomClicked);
            UiFactory.SetAnchoredRect(_lockRoomButton.GetComponent<RectTransform>(), new Vector2(0.1f, 0.1f), new Vector2(0.1f, 0.1f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(160f, 48f));

            _lockStatusText = UiFactory.CreateText(backdrop.transform, "LockStatus", "", 18, TextAnchor.MiddleCenter, new Color(0.8f, 0.7f, 0.3f));
            UiFactory.SetAnchoredRect(_lockStatusText.rectTransform, new Vector2(0.1f, 0.18f), new Vector2(0.1f, 0.18f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(160f, 24f));

            // 聊天系统
            _chatView = gameObject.AddComponent<ChatView>();
            _chatView.Build(backdrop.transform, new Vector2(1f, 0.5f), new Vector2(300f, 250f));
            // 调整聊天面板位置到右侧
            var chatRect = _chatView.GetComponent<RectTransform>();
            UiFactory.SetAnchoredRect(chatRect, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-320f, 0f), new Vector2(300f, 250f));

            OnClose();
        }

        protected override void OnViewOpened()
        {
            RefreshPlayerList();
            RefreshStartButton();
        }

        /// <summary>设置房间信息。</summary>
        public void SetRoomInfo(string roomName, string joinCode, bool isHost)
        {
            _roomNameText.text = roomName;
            _roomCodeText.text = $"加入码: {joinCode}";
            _startButton.gameObject.SetActive(isHost);
        }

        /// <summary>刷新玩家列表。</summary>
        public void RefreshPlayerList()
        {
            ClearPlayerRows();

            // 从 NetManager 获取玩家列表
            if (NetManager.HasInstance)
            {
                var players = NetManager.Instance.Players;
                for (var i = 0; i < players.Count; i++)
                {
                    var playerId = players[i];
                    var isHost = i == 0; // 第一个玩家是房主
                    AddPlayerRow($"玩家 {playerId}", isHost, i == 0);
                }
            }
        }

        private void AddPlayerRow(string playerName, bool isHost, bool isReady)
        {
            var row = UiFactory.CreateImage(_playerListContainer, $"PlayerRow_{playerName}", new Color(0.13f, 0.14f, 0.22f, 1f));
            _playerRows.Add(row.gameObject);

            var nameText = UiFactory.CreateText(row.transform, "Name", playerName, 24, TextAnchor.MiddleLeft, isHost ? HostColor : PlayerColor);
            UiFactory.SetAnchoredRect(nameText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(300f, 36f));

            var badge = UiFactory.CreateText(row.transform, "Badge", isHost ? "房主" : "玩家", 20, TextAnchor.MiddleRight, isHost ? HostColor : PlayerColor);
            UiFactory.SetAnchoredRect(badge.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20f, 0f), new Vector2(80f, 30f));
        }

        private void ClearPlayerRows()
        {
            foreach (var row in _playerRows)
            {
                if (row != null) Object.Destroy(row);
            }
            _playerRows.Clear();
        }

        private void RefreshStartButton()
        {
            // 只有房主能看到开始按钮
            if (NetManager.HasInstance)
            {
                _startButton.gameObject.SetActive(NetManager.Instance.IsHost);
            }
        }

        private async void OnStartGame()
        {
            _startButton.interactable = false;
            // TODO: 通知所有客户端开始游戏
            await Task.CompletedTask;
        }

        private async void OnLeaveRoom()
        {
            await OnlineMatchmaker.LeaveRoomAsync();
            OnClose();
        }

        private void OnLockRoomClicked()
        {
            _isRoomLocked = !_isRoomLocked;
            _lockRoomButton.GetComponentInChildren<Text>().text = _isRoomLocked ? "解锁房间" : "锁定房间";
            _lockStatusText.text = _isRoomLocked ? "🔒 房间已锁定" : "🔓 房间开放";

            if (_chatView != null)
            {
                _chatView.AddSystemMessage(_isRoomLocked ? "房间已锁定，新玩家无法加入" : "房间已解锁，新玩家可以加入");
            }
        }

        private void OnKickPlayer(int playerIndex)
        {
            // TODO: 调用网络接口踢人
            if (_chatView != null)
            {
                _chatView.AddSystemMessage($"已踢出玩家 {playerIndex}");
            }
        }
    }
}
