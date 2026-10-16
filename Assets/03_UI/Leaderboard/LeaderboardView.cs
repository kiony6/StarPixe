using System.Text;
using StarPixelSurge.Framework;
using StarPixelSurge.SaveData;
using UnityEngine;
using UnityEngine.UI;

namespace StarPixelSurge.UI
{
    /// <summary>
    /// 排行榜界面：显示历史最高分记录。
    /// </summary>
    public class LeaderboardView : UIView
    {
        private Text _titleText;
        private Text _listText;
        private Button _backButton;

        private static readonly Color PanelColor = new Color(0.06f, 0.07f, 0.12f, 0.95f);

        public void Build(Transform parent)
        {
            var panel = UiFactory.CreateImage(parent, "LeaderboardPanel", PanelColor);
            panel.raycastTarget = true;
            contentRoot = panel.gameObject;
            UiFactory.Stretch(panel.rectTransform);
            UiFactory.AddOutline(panel, new Color(0.3f, 0.6f, 1f), 3f);

            // 标题
            _titleText = UiFactory.CreateNeonText(panel.transform, "Title", "排行榜", 48, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.2f), 4f);
            _titleText.rectTransform.anchorMin = new Vector2(0.5f, 0.9f);
            _titleText.rectTransform.anchorMax = new Vector2(0.5f, 0.9f);
            _titleText.rectTransform.sizeDelta = new Vector2(400f, 60f);
            _titleText.rectTransform.anchoredPosition = Vector2.zero;

            // 排行榜列表
            _listText = UiFactory.CreateText(panel.transform, "List", string.Empty, 24, TextAnchor.MiddleCenter, Color.white);
            _listText.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            _listText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _listText.rectTransform.sizeDelta = new Vector2(600f, 400f);
            _listText.rectTransform.anchoredPosition = new Vector2(0f, 20f);

            // 返回按钮
            _backButton = UiFactory.CreateButton(panel.transform, "BackButton", "返回", new Color(0.4f, 0.4f, 0.45f), OnBack);
            _backButton.GetComponent<RectTransform>().anchorMin = new Vector2(0.5f, 0.08f);
            _backButton.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 0.08f);
            _backButton.GetComponent<RectTransform>().sizeDelta = new Vector2(160f, 44f);
            _backButton.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;

            RefreshData();
            OnClose();
        }

        private void RefreshData()
        {
            if (SaveManager.Instance == null) return;

            var data = SaveManager.Instance.Data;
            var sb = new StringBuilder();

            if (data.HighScores == null || data.HighScores.Count == 0)
            {
                sb.AppendLine("暂无记录");
                sb.AppendLine();
                sb.AppendLine("快来开始第一局游戏吧！");
            }
            else
            {
                sb.AppendLine($"排名   玩家            分数        波次");
                sb.AppendLine("────────────────────────────────");

                for (var i = 0; i < data.HighScores.Count && i < 10; i++)
                {
                    var entry = data.HighScores[i];
                    sb.AppendLine($"{i + 1,3}.   {entry.PlayerName,-12}   {entry.Score,8:N0}   {entry.Wave,3}");
                }
            }

            _listText.text = sb.ToString();
        }

        private void OnBack()
        {
            UIManager.Instance.Close<LeaderboardView>();
        }
    }
}
