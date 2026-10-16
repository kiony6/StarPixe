using System.Collections.Generic;
using StarPixelSurge.Config;
using StarPixelSurge.Framework;
using StarPixelSurge.Network;
using UnityEngine;
using UnityEngine.UI;

namespace StarPixelSurge.UI
{
    /// <summary>
    /// 联机房间与准备态界面。展示房间号、成员名册、各成员职业与准备状态，
    /// 由本机玩家选择职业、切换准备态；房主在全员准备后可开始对局。
    /// 名册变化通过 <see cref="NetManager.RoomUpdated"/> 驱动刷新。
    /// </summary>
    public class RoomView : UIView
    {
        private const int MaxPlayers = 4;
        private const string DefaultRoomId = "LOCAL";
        private const string FallbackClassId = "thunder_mage";
        private const string DefaultRemoteHost = "127.0.0.1";
        private const string DefaultRemotePort = "7777";

        private static readonly Color BackdropColor = new Color(0.03f, 0.03f, 0.06f, 0.98f);
        private static readonly Color PanelColor = new Color(0.08f, 0.09f, 0.15f, 1f);
        private static readonly Color SlotColor = new Color(0.11f, 0.12f, 0.2f, 1f);
        private static readonly Color EmptySlotColor = new Color(0.07f, 0.07f, 0.11f, 1f);
        private static readonly Color HostColor = new Color(1f, 0.78f, 0.35f);
        private static readonly Color ReadyColor = new Color(0.4f, 0.95f, 0.6f);
        private static readonly Color NotReadyColor = new Color(0.65f, 0.65f, 0.72f);
        private static readonly Color AccentColor = new Color(0.25f, 0.65f, 0.95f);
        private static readonly Color JoinColor = new Color(0.25f, 0.6f, 0.5f);
        private static readonly Color LeaveColor = new Color(0.55f, 0.25f, 0.3f);
        private static readonly Color ModeColor = new Color(0.2f, 0.24f, 0.36f);

        private sealed class MemberSlot
        {
            public GameObject Root;
            public Image Background;
            public Text Title;
            public Text ClassName;
            public Text State;
        }

        private readonly List<MemberSlot> _slots = new List<MemberSlot>();
        private readonly List<ClassConfig> _classes = new List<ClassConfig>();

        private Transform _lobbyPanel;
        private Transform _roomPanel;
        private InputField _roomIdField;
        private InputField _addressField;
        private InputField _portField;
        private Button _modeButton;
        private Text _modeLabel;
        private Transform _udpGroup;
        private bool _useUdp;
        private Text _roomInfoText;
        private Text _statusText;
        private Button _readyButton;
        private Text _readyLabel;
        private Button _startButton;
        private Text _classText;
        private int _classIndex;

        private NetManager _net;
        private bool _bound;

        public void Build(Transform parent)
        {
            var backdrop = UiFactory.CreateImage(parent, "RoomBackdrop", BackdropColor);
            backdrop.raycastTarget = true;
            contentRoot = backdrop.gameObject;
            UiFactory.Stretch((RectTransform)contentRoot.GetComponent<RectTransform>());

            var title = UiFactory.CreateNeonText(backdrop.transform, "Title", "联机房间", 56, TextAnchor.UpperCenter, new Color(0.7f, 0.9f, 1f));
            UiFactory.SetAnchoredRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(1200f, 70f));

            _roomInfoText = UiFactory.CreateText(backdrop.transform, "RoomInfo", string.Empty, 28, TextAnchor.UpperCenter, new Color(0.8f, 0.85f, 1f));
            UiFactory.SetAnchoredRect(_roomInfoText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(1500f, 40f));

            _lobbyPanel = BuildLobbyPanel(backdrop.transform);
            _roomPanel = BuildRoomPanel(backdrop.transform);

            OnClose();
        }

        private Transform BuildLobbyPanel(Transform parent)
        {
            var panel = UiFactory.CreateNeonPanel(parent, "LobbyPanel", PanelColor, AccentColor, 3f);
            UiFactory.SetAnchoredRect((RectTransform)panel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(760f, 470f));

            var hint = UiFactory.CreateText(panel.transform, "Hint", "创建房间或输入房间号加入", 28, TextAnchor.UpperCenter, new Color(0.82f, 0.86f, 1f));
            UiFactory.SetAnchoredRect(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(700f, 40f));

            _roomIdField = UiFactory.CreateInputField(panel.transform, "RoomIdField", DefaultRoomId, new Color(0.05f, 0.05f, 0.09f, 1f));
            UiFactory.SetAnchoredRect((RectTransform)_roomIdField.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 96f), new Vector2(520f, 64f));
            _roomIdField.text = DefaultRoomId;

            // 传输模式切换：进程内回环（本地联调）与真实 UDP（局域网/公网）。
            _modeButton = UiFactory.CreateButton(panel.transform, "ModeButton", "传输：本地回环", ModeColor, OnToggleMode);
            UiFactory.SetAnchoredRect(_modeButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 22f), new Vector2(560f, 56f));
            _modeLabel = _modeButton.GetComponentInChildren<Text>();

            _udpGroup = UiFactory.CreateRect(panel.transform, "UdpGroup");
            UiFactory.SetAnchoredRect((RectTransform)_udpGroup, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -46f), new Vector2(700f, 64f));

            _addressField = UiFactory.CreateInputField(_udpGroup, "AddressField", DefaultRemoteHost, new Color(0.05f, 0.05f, 0.09f, 1f));
            UiFactory.SetAnchoredRect((RectTransform)_addressField.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-128f, 0f), new Vector2(410f, 60f));
            _addressField.text = DefaultRemoteHost;

            _portField = UiFactory.CreateInputField(_udpGroup, "PortField", DefaultRemotePort, new Color(0.05f, 0.05f, 0.09f, 1f));
            UiFactory.SetAnchoredRect((RectTransform)_portField.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(192f, 0f), new Vector2(200f, 60f));
            _portField.text = DefaultRemotePort;
            _portField.contentType = InputField.ContentType.IntegerNumber;

            var create = UiFactory.CreateButton(panel.transform, "CreateRoom", "创建房间", AccentColor, OnCreateRoom);
            UiFactory.SetAnchoredRect(create.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-150f, 36f), new Vector2(260f, 64f));

            var join = UiFactory.CreateButton(panel.transform, "JoinRoom", "加入房间", JoinColor, OnJoinRoom);
            UiFactory.SetAnchoredRect(join.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(150f, 36f), new Vector2(260f, 64f));

            RefreshMode();
            return panel.transform;
        }

        /// <summary>切换本地回环与真实 UDP 传输模式。</summary>
        private void OnToggleMode()
        {
            _useUdp = !_useUdp;
            RefreshMode();
        }

        private void RefreshMode()
        {
            if (_modeLabel != null)
            {
                _modeLabel.text = _useUdp ? "传输：UDP" : "传输：本地回环";
            }

            if (_udpGroup != null)
            {
                _udpGroup.gameObject.SetActive(_useUdp);
            }
        }

        /// <summary>把当前传输配置写入 <see cref="NetManager"/>，需在创建/加入房间之前调用。</summary>
        private void ApplyTransport()
        {
            if (_net == null)
            {
                return;
            }

            _net.UseUdp = _useUdp;
            if (!_useUdp)
            {
                return;
            }

            _net.RemoteHost = _addressField != null && !string.IsNullOrEmpty(_addressField.text)
                ? _addressField.text
                : DefaultRemoteHost;

            var port = ParsePort(_portField != null ? _portField.text : null);
            _net.RemotePort = port;
            _net.HostPort = port;
        }

        private static int ParsePort(string text)
        {
            return int.TryParse(text, out var port) ? Mathf.Clamp(port, 1, 65535) : int.Parse(DefaultRemotePort);
        }

        private Transform BuildRoomPanel(Transform parent)
        {
            var panel = UiFactory.CreateRect(parent, "RoomPanel");
            // RoomPanel 是 controls/status 的锚点容器，必须铺满背景；
            // 否则默认 100x100 的居中矩形会把底部锚点拉到屏幕中央。
            UiFactory.Stretch((RectTransform)panel);

            var slotContainer = UiFactory.CreateRect(panel, "MemberSlots");
            UiFactory.SetAnchoredRect(slotContainer, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(1400f, 420f));

            var layout = slotContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 28f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            for (var i = 0; i < MaxPlayers; i++)
            {
                _slots.Add(CreateSlot(slotContainer));
            }

            var controls = UiFactory.CreateImage(panel, "Controls", PanelColor);
            UiFactory.SetAnchoredRect((RectTransform)controls.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(1400f, 130f));
            UiFactory.AddOutline(controls, AccentColor, 3f);

            var prev = UiFactory.CreateButton(controls.transform, "PrevClass", "◀", new Color(0.16f, 0.18f, 0.28f), () => CycleClass(-1));
            UiFactory.SetAnchoredRect(prev.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-520f, 0f), new Vector2(72f, 72f));

            _classText = UiFactory.CreateText(controls.transform, "ClassText", string.Empty, 30, TextAnchor.MiddleCenter, Color.white);
            UiFactory.SetAnchoredRect(_classText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-340f, 0f), new Vector2(300f, 72f));

            var next = UiFactory.CreateButton(controls.transform, "NextClass", "▶", new Color(0.16f, 0.18f, 0.28f), () => CycleClass(1));
            UiFactory.SetAnchoredRect(next.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-160f, 0f), new Vector2(72f, 72f));

            _readyButton = UiFactory.CreateButton(controls.transform, "ReadyButton", "准备", ReadyColor, OnToggleReady);
            UiFactory.SetAnchoredRect(_readyButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(60f, 0f), new Vector2(240f, 72f));
            _readyLabel = _readyButton.GetComponentInChildren<Text>();

            _startButton = UiFactory.CreateButton(controls.transform, "StartButton", "开始对局", AccentColor, OnStartMatch);
            UiFactory.SetAnchoredRect(_startButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(340f, 0f), new Vector2(260f, 72f));

            var leave = UiFactory.CreateButton(controls.transform, "LeaveButton", "离开房间", LeaveColor, OnLeaveRoom);
            UiFactory.SetAnchoredRect(leave.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(590f, 0f), new Vector2(220f, 72f));

            _statusText = UiFactory.CreateText(panel, "Status", string.Empty, 26, TextAnchor.LowerCenter, new Color(0.75f, 0.8f, 0.95f));
            UiFactory.SetAnchoredRect(_statusText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1400f, 40f));

            return panel;
        }

        private MemberSlot CreateSlot(Transform parent)
        {
            var background = UiFactory.CreateImage(parent, "Slot", SlotColor);
            background.rectTransform.sizeDelta = new Vector2(320f, 400f);
            UiFactory.AddOutline(background, NeonPalette.WithAlpha(AccentColor, 0.7f), 3f);
            var element = background.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = 320f;
            element.preferredHeight = 400f;

            var accent = UiFactory.CreateImage(background.transform, "Accent", AccentColor);
            UiFactory.SetAnchoredRect(accent.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 10f));

            var title = UiFactory.CreateText(background.transform, "Title", string.Empty, 32, TextAnchor.UpperCenter, Color.white);
            UiFactory.SetAnchoredRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(-20f, 48f));

            var portrait = UiFactory.CreateImage(background.transform, "Portrait", AccentColor * 0.5f);
            UiFactory.SetAnchoredRect(portrait.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(150f, 150f));

            var className = UiFactory.CreateText(background.transform, "Class", string.Empty, 26, TextAnchor.MiddleCenter, new Color(0.85f, 0.88f, 1f));
            UiFactory.SetAnchoredRect(className.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 96f), new Vector2(-24f, 40f));

            var state = UiFactory.CreateText(background.transform, "State", string.Empty, 26, TextAnchor.MiddleCenter, NotReadyColor);
            UiFactory.SetAnchoredRect(state.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 44f), new Vector2(-24f, 40f));

            return new MemberSlot
            {
                Root = background.gameObject,
                Background = background,
                Title = title,
                ClassName = className,
                State = state
            };
        }

        protected override void OnViewOpened()
        {
            Bind();
            RefreshClasses();
            Refresh();
        }

        protected override void OnViewClosed()
        {
            Unbind();
        }

        private void Bind()
        {
            if (_bound)
            {
                return;
            }

            _net = NetManager.Instance;
            _net.RoomUpdated += Refresh;
            _bound = true;
        }

        private void Unbind()
        {
            if (!_bound)
            {
                return;
            }

            if (_net != null)
            {
                _net.RoomUpdated -= Refresh;
            }

            _bound = false;
        }

        private void RefreshClasses()
        {
            _classes.Clear();
            if (ConfigService.HasInstance)
            {
                foreach (var classConfig in ConfigService.Instance.GetAllClasses())
                {
                    if (classConfig != null)
                    {
                        _classes.Add(classConfig);
                    }
                }
            }

            _classes.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            _classIndex = 0;
        }

        private ClassConfig CurrentClass()
        {
            if (_classes.Count == 0)
            {
                return null;
            }

            return _classes[Mathf.Clamp(_classIndex, 0, _classes.Count - 1)];
        }

        private void CycleClass(int step)
        {
            if (_classes.Count == 0)
            {
                return;
            }

            _classIndex = (_classIndex + step + _classes.Count) % _classes.Count;
            PushLocalClass();
            Refresh();
        }

        private void PushLocalClass()
        {
            var classConfig = CurrentClass();
            _net?.SelectClass(classConfig != null ? classConfig.Id : FallbackClassId);
        }

        private void OnCreateRoom()
        {
            ApplyTransport();
            _net.CreateRoom();
            PushLocalClass();
            GameManager.Instance.EnterRoom();
            Refresh();
        }

        private void OnJoinRoom()
        {
            ApplyTransport();
            var roomId = _roomIdField != null && !string.IsNullOrEmpty(_roomIdField.text) ? _roomIdField.text : DefaultRoomId;
            _net.JoinRoom(roomId);
            PushLocalClass();
            GameManager.Instance.EnterRoom();
            Refresh();
        }

        private void OnToggleReady()
        {
            if (_net.LocalPlayerId < 0)
            {
                return;
            }

            var member = _net.Room.Get(_net.LocalPlayerId);
            var nextReady = member == null || !member.Ready;
            PushLocalClass();
            _net.SetReady(nextReady);
            Refresh();
        }

        private void OnStartMatch()
        {
            _net.StartMatch();
        }

        private void OnLeaveRoom()
        {
            _net.LeaveRoom();
            GameManager.Instance.ReturnToMainMenu();
            if (GameplayUIRoot.Instance != null)
            {
                GameplayUIRoot.Instance.ShowClassSelection();
            }
        }

        private void Refresh()
        {
            if (_net == null)
            {
                return;
            }

            var inRoom = _net.State != RoomState.Idle;
            if (_lobbyPanel != null)
            {
                _lobbyPanel.gameObject.SetActive(!inRoom);
            }

            if (_roomPanel != null)
            {
                _roomPanel.gameObject.SetActive(inRoom);
            }

            if (!inRoom)
            {
                if (_roomInfoText != null)
                {
                    _roomInfoText.text = string.Empty;
                }

                return;
            }

            var localId = _net.LocalPlayerId;
            var members = _net.Room.Members;

            if (_roomInfoText != null)
            {
                var role = _net.IsHost ? "房主" : "客户端";
                _roomInfoText.text = $"房间号：{_net.RoomId}    身份：{role}    玩家：{members.Count}/{MaxPlayers}";
            }

            for (var i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (i < members.Count)
                {
                    var member = members[i];
                    var isLocal = member.PlayerId == localId;
                    var classConfig = ResolveMemberClass(member.PlayerId, isLocal);

                    slot.Root.SetActive(true);
                    slot.Background.color = SlotColor;
                    slot.Title.text = member.IsHost ? $"玩家 {member.PlayerId}（房主）" : $"玩家 {member.PlayerId}";
                    slot.Title.color = member.IsHost ? HostColor : Color.white;
                    slot.ClassName.text = classConfig != null ? classConfig.DisplayName : "未选择职业";
                    slot.State.text = member.Ready ? "已准备" : "未准备";
                    slot.State.color = member.Ready ? ReadyColor : NotReadyColor;
                }
                else
                {
                    slot.Root.SetActive(true);
                    slot.Background.color = EmptySlotColor;
                    slot.Title.text = "空位";
                    slot.Title.color = NotReadyColor;
                    slot.ClassName.text = "等待玩家加入";
                    slot.State.text = string.Empty;
                    slot.State.color = NotReadyColor;
                }
            }

            var local = _net.Room.Get(localId);
            var localReady = local != null && local.Ready;
            if (_readyLabel != null)
            {
                _readyLabel.text = localReady ? "取消准备" : "准备";
            }

            var localClass = CurrentClass();
            if (_classText != null)
            {
                _classText.text = localClass != null ? localClass.DisplayName : "选择职业";
            }

            if (_startButton != null)
            {
                _startButton.gameObject.SetActive(_net.IsHost);
                _startButton.interactable = _net.CanStartMatch();
            }

            if (_statusText != null)
            {
                _statusText.text = BuildStatus(localReady, members.Count);
            }
        }

        private ClassConfig ResolveMemberClass(int playerId, bool isLocal)
        {
            if (isLocal)
            {
                var local = CurrentClass();
                if (local != null)
                {
                    return local;
                }
            }

            var classId = _net.GetClassId(playerId);
            if (!string.IsNullOrEmpty(classId) && ConfigService.HasInstance)
            {
                return ConfigService.Instance.GetClass(classId);
            }

            return null;
        }

        private string BuildStatus(bool localReady, int memberCount)
        {
            if (!_net.IsHost)
            {
                return localReady ? "等待房主开始对局" : "点击准备，等待房主开始对局";
            }

            if (memberCount < RoomService.MinPlayers)
            {
                return $"至少需要 {RoomService.MinPlayers} 名玩家才能开始";
            }

            return _net.CanStartMatch() ? "全员就绪，可以开始对局" : "等待其他玩家准备";
        }
    }
}
