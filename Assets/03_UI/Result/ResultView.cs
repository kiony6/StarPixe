using StarPixelSurge.Framework;
using StarPixelSurge.GameCore;
using UnityEngine;
using UnityEngine.UI;

namespace StarPixelSurge.UI
{
    /// <summary>
    /// 结算界面。对局结束时展示统计信息，并提供重开入口。
    /// </summary>
    public class ResultView : UIView
    {
        private Text _titleText;
        private Text _statsText;

        private static readonly Color PanelColor = new Color(0.06f, 0.07f, 0.12f, 0.96f);

        public void Build(Transform parent)
        {
            var panel = UiFactory.CreateImage(parent, "ResultPanel", PanelColor);
            panel.raycastTarget = true;
            contentRoot = panel.gameObject;
            UiFactory.SetAnchoredRect(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 520f));

            _titleText = UiFactory.CreateText(panel.transform, "Title", "对局结束", 52, TextAnchor.UpperCenter, new Color(1f, 0.4f, 0.45f));
            UiFactory.SetAnchoredRect(_titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(-40f, 70f));

            _statsText = UiFactory.CreateText(panel.transform, "Stats", string.Empty, 30, TextAnchor.MiddleCenter, Color.white);
            UiFactory.SetAnchoredRect(_statsText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(-80f, -200f));

            var restart = UiFactory.CreateButton(panel.transform, "RestartButton", "再来一局", new Color(0.2f, 0.6f, 0.9f), OnRestart);
            UiFactory.SetAnchoredRect(restart.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-160f, 50f), new Vector2(280f, 72f));

            var lobby = UiFactory.CreateButton(panel.transform, "LobbyButton", "返回大厅", new Color(0.35f, 0.35f, 0.45f), OnReturnToLobby);
            UiFactory.SetAnchoredRect(lobby.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(160f, 50f), new Vector2(280f, 72f));

            OnClose();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<MatchEndedEvent>(OnMatchEnded);
            EventBus.Subscribe<MatchStartedEvent>(OnMatchStarted);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<MatchEndedEvent>(OnMatchEnded);
            EventBus.Unsubscribe<MatchStartedEvent>(OnMatchStarted);
        }

        private void OnMatchEnded(MatchEndedEvent evt)
        {
            if (_titleText != null)
            {
                _titleText.text = "对局结束";
            }

            if (_statsText != null)
            {
                var minutes = Mathf.FloorToInt(evt.SurviviedSeconds / 60f);
                var seconds = Mathf.FloorToInt(evt.SurviviedSeconds % 60f);
                _statsText.text = $"存活时长   {minutes:00}:{seconds:00}\n\n到达波次   {evt.ReachedLayer}\n\n累计击杀   {evt.Kills}";
            }

            UIManager.Instance.PushModal(this);
        }

        private void OnMatchStarted(MatchStartedEvent evt)
        {
            if (IsOpen)
            {
                UIManager.Instance.PopModal();
            }
        }

        private void OnRestart()
        {
            UIManager.Instance.PopModal();
            if (GameBootstrap.Instance != null)
            {
                GameBootstrap.Instance.RestartMatch();
            }
        }

        private void OnReturnToLobby()
        {
            UIManager.Instance.PopModal();
            if (GameBootstrap.Instance != null)
            {
                GameBootstrap.Instance.EnterLobby();
            }
        }
    }
}
