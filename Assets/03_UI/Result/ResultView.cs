using System.Text;
using StarPixelSurge.Framework;
using StarPixelSurge.GameCore;
using StarPixelSurge.SaveData;
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
        private Text _newRecordText;

        private static readonly Color PanelColor = new Color(0.06f, 0.07f, 0.12f, 0.96f);

        public void Build(Transform parent)
        {
            var panel = UiFactory.CreateImage(parent, "ResultPanel", PanelColor);
            panel.raycastTarget = true;
            contentRoot = panel.gameObject;
            UiFactory.SetAnchoredRect(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 560f));
            UiFactory.AddOutline(panel, NeonPalette.Violet, 3f);

            _titleText = UiFactory.CreateNeonText(panel.transform, "Title", "对局结束", 52, TextAnchor.UpperCenter, NeonPalette.Crimson, 5f);
            UiFactory.SetAnchoredRect(_titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(-40f, 70f));

            _newRecordText = UiFactory.CreateText(panel.transform, "NewRecord", "🏆 新纪录！", 28, TextAnchor.UpperCenter, new Color(1f, 0.85f, 0.2f));
            UiFactory.SetAnchoredRect(_newRecordText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(-40f, 40f));
            _newRecordText.gameObject.SetActive(false);

            _statsText = UiFactory.CreateText(panel.transform, "Stats", string.Empty, 26, TextAnchor.MiddleCenter, Color.white);
            UiFactory.SetAnchoredRect(_statsText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(-80f, -180f));

            var restart = UiFactory.CreateButton(panel.transform, "RestartButton", "再来一局", new Color(0.2f, 0.6f, 0.9f), OnRestart);
            UiFactory.SetAnchoredRect(restart.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-160f, 50f), new Vector2(280f, 64f));

            var lobby = UiFactory.CreateButton(panel.transform, "LobbyButton", "返回大厅", new Color(0.35f, 0.35f, 0.45f), OnReturnToLobby);
            UiFactory.SetAnchoredRect(lobby.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(160f, 50f), new Vector2(280f, 64f));

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

            // 计算分数
            var score = evt.Kills * 10 + evt.ReachedLayer * 100 + Mathf.FloorToInt(evt.SurviviedSeconds) * 5;

            // 检查是否新纪录
            var isNewRecord = false;
            if (SaveManager.Instance != null)
            {
                var data = SaveManager.Instance.Data;
                if (data.HighScores.Count == 0 || score > data.HighScores[0].Score)
                {
                    isNewRecord = true;
                    SaveManager.Instance.AddHighScore(score, evt.ReachedLayer, GameBootstrap.Instance.LastClassId);
                }
            }

            if (_newRecordText != null)
            {
                _newRecordText.gameObject.SetActive(isNewRecord);
            }

            if (_statsText != null)
            {
                var minutes = Mathf.FloorToInt(evt.SurviviedSeconds / 60f);
                var seconds = Mathf.FloorToInt(evt.SurviviedSeconds % 60f);

                var sb = new StringBuilder();
                sb.AppendLine($"总分       {score:N0}");
                sb.AppendLine();
                sb.AppendLine($"存活时长   {minutes:00}:{seconds:00}");
                sb.AppendLine();
                sb.AppendLine($"到达波次   {evt.ReachedLayer}");
                sb.AppendLine();
                sb.AppendLine($"累计击杀   {evt.Kills:N0}");

                _statsText.text = sb.ToString();
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
