using StarPixelSurge.Framework;
using StarPixelSurge.SaveData;
using UnityEngine;
using UnityEngine.UI;

namespace StarPixelSurge.UI
{
    /// <summary>
    /// 主菜单界面：显示游戏标题、玩家昵称、最高分，以及开始游戏按钮。
    /// </summary>
    public class MainMenuView : UIView
    {
        public void Build(Transform parent)
        {
            // 创建主菜单面板
            var panel = UiFactory.CreateImage(parent, "MainMenuPanel", new Color(0.1f, 0.12f, 0.18f, 0.95f));
            panel.raycastTarget = true;
            contentRoot = panel.gameObject;
            UiFactory.Stretch(panel.rectTransform);

            // 游戏标题
            var titleText = UiFactory.CreateText(panel.transform, "Title", "StarPixelSurge", 48, TextAnchor.MiddleCenter, new Color(0.4f, 0.8f, 1f));
            UiFactory.SetAnchoredRect(titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(600f, 60f));

            // 玩家昵称
            var playerNameText = UiFactory.CreateText(panel.transform, "PlayerName", "玩家: Player", 20, TextAnchor.MiddleCenter, Color.white);
            UiFactory.SetAnchoredRect(playerNameText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 80f), new Vector2(400f, 30f));

            // 最高分
            var highScoreText = UiFactory.CreateText(panel.transform, "HighScore", "最高分: 0", 18, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.4f));
            UiFactory.SetAnchoredRect(highScoreText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(400f, 30f));

            // 开始游戏按钮
            var startButton = UiFactory.CreateButton(panel.transform, "StartButton", "开始游戏", new Color(0.2f, 0.5f, 0.8f), OnStartClicked);
            UiFactory.SetAnchoredRect(startButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -80f), new Vector2(200f, 50f));

            // 设置按钮
            var settingsButton = UiFactory.CreateButton(panel.transform, "SettingsButton", "设置", new Color(0.4f, 0.4f, 0.45f), OnSettingsClicked);
            UiFactory.SetAnchoredRect(settingsButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -150f), new Vector2(200f, 40f));

            // 排行榜按钮
            var leaderboardButton = UiFactory.CreateButton(panel.transform, "LeaderboardButton", "排行榜", new Color(0.6f, 0.5f, 0.2f), OnLeaderboardClicked);
            UiFactory.SetAnchoredRect(leaderboardButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -210f), new Vector2(200f, 40f));

            RefreshData(playerNameText, highScoreText);
        }

        private void RefreshData(Text nameText, Text scoreText)
        {
            if (SaveManager.Instance != null)
            {
                var data = SaveManager.Instance.Data;
                nameText.text = $"玩家: {data.PlayerName}";

                if (data.HighScores != null && data.HighScores.Count > 0)
                {
                    var best = data.HighScores[0];
                    scoreText.text = $"最高分: {best.Score} (波次 {best.Wave})";
                }
                else
                {
                    scoreText.text = "最高分: 0";
                }
            }
        }

        private void OnStartClicked()
        {
            if (GameplayUIRoot.Instance != null)
            {
                GameplayUIRoot.Instance.ShowClassSelection();
                UIManager.Instance.Close<MainMenuView>();
            }
        }

        private void OnSettingsClicked()
        {
            UIManager.Instance.Open<SettingsView>();
        }

        private void OnLeaderboardClicked()
        {
            UIManager.Instance.Open<LeaderboardView>();
        }
    }
}
