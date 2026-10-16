using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace StarPixelSurge.UI
{
    /// <summary>
    /// 聊天系统UI：显示聊天消息，支持发送消息。
    /// </summary>
    public class ChatView : MonoBehaviour
    {
        private Text _chatText;
        private InputField _inputField;
        private Button _sendButton;
        private readonly List<string> _messages = new List<string>();
        private const int MaxMessages = 50;

        public void Build(Transform parent, Vector2 anchoredPosition, Vector2 size)
        {
            // 聊天面板背景
            var panel = UiFactory.CreateImage(parent, "ChatPanel", new Color(0.05f, 0.06f, 0.1f, 0.8f));
            panel.raycastTarget = true;
            UiFactory.SetAnchoredRect(panel.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), anchoredPosition, Vector2.zero, size);

            // 聊天内容区域
            _chatText = UiFactory.CreateText(panel.transform, "ChatContent", "", 16, TextAnchor.UpperLeft, Color.white);
            UiFactory.SetAnchoredRect(_chatText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -10f), new Vector2(size.x - 20f, size.y - 60f));

            // 输入框
            _inputField = UiFactory.CreateInputField(panel.transform, "ChatInput", "输入消息...", new Color(0.12f, 0.14f, 0.2f, 1f));
            UiFactory.SetAnchoredRect(_inputField.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(10f, 10f), new Vector2(size.x - 90f, 36f));

            // 发送按钮
            _sendButton = UiFactory.CreateButton(panel.transform, "SendButton", "发送", new Color(0.2f, 0.5f, 0.8f), OnSendClicked);
            UiFactory.SetAnchoredRect(_sendButton.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-10f, 10f), new Vector2(70f, 36f));
        }

        /// <summary>添加系统消息。</summary>
        public void AddSystemMessage(string message)
        {
            AddMessage("系统", message, new Color(0.5f, 0.9f, 1f));
        }

        /// <summary>添加房主消息。</summary>
        public void AddHostMessage(string playerName, string message)
        {
            AddMessage(playerName, message, new Color(1f, 0.85f, 0.3f));
        }

        /// <summary>添加普通玩家消息。</summary>
        public void AddPlayerMessage(string playerName, string message)
        {
            AddMessage(playerName, message, new Color(0.8f, 0.85f, 1f));
        }

        private void AddMessage(string playerName, string message, Color color)
        {
            var coloredName = $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{playerName}</color>";
            var line = $"{coloredName}: {message}";
            _messages.Add(line);

            if (_messages.Count > MaxMessages)
            {
                _messages.RemoveAt(0);
            }

            _chatText.text = string.Join("\n", _messages);
        }

        private void OnSendClicked()
        {
            var text = _inputField.text.Trim();
            if (string.IsNullOrEmpty(text)) return;

            // TODO: 发送消息到网络
            AddPlayerMessage("我", text);
            _inputField.text = "";
        }
    }
}
