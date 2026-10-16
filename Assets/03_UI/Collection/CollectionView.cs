using StarPixelSurge.Framework;
using StarPixelSurge.SaveData;
using UnityEngine;
using UnityEngine.UI;

namespace StarPixelSurge.UI
{
    /// <summary>
    /// 图鉴/成就界面：显示怪物图鉴和已解锁成就。
    /// </summary>
    public class CollectionView : UIView
    {
        private Text _titleText;
        private Text _bestiaryText;
        private Text _achievementText;
        private Button _backButton;

        public void Build(Transform parent)
        {
            // 创建面板
            var panel = UiFactory.CreateImage(parent, "CollectionPanel", new Color(0.1f, 0.12f, 0.18f, 0.95f));
            panel.raycastTarget = true;
            contentRoot = panel.gameObject;
            UiFactory.Stretch(panel.rectTransform);

            // 标题
            _titleText = UiFactory.CreateText(panel.transform, "Title", "图鉴 / 成就", 36, TextAnchor.MiddleCenter, new Color(0.4f, 0.8f, 1f));
            UiFactory.SetAnchoredRect(_titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(400f, 50f));

            // 怪物图鉴区域
            var bestiaryLabel = UiFactory.CreateText(panel.transform, "BestiaryLabel", "怪物图鉴", 24, TextAnchor.UpperLeft, Color.white);
            UiFactory.SetAnchoredRect(bestiaryLabel.rectTransform, new Vector2(0.1f, 0.9f), new Vector2(0.1f, 0.9f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(200f, 30f));

            _bestiaryText = UiFactory.CreateText(panel.transform, "BestiaryText", "暂无数据", 18, TextAnchor.UpperLeft, new Color(0.8f, 0.8f, 0.8f));
            UiFactory.SetAnchoredRect(_bestiaryText.rectTransform, new Vector2(0.1f, 0.85f), new Vector2(0.1f, 0.85f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(700f, 300f));

            // 成就区域
            var achievementLabel = UiFactory.CreateText(panel.transform, "AchievementLabel", "成就", 24, TextAnchor.UpperLeft, Color.white);
            UiFactory.SetAnchoredRect(achievementLabel.rectTransform, new Vector2(0.1f, 0.45f), new Vector2(0.1f, 0.45f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(200f, 30f));

            _achievementText = UiFactory.CreateText(panel.transform, "AchievementText", "暂无数据", 18, TextAnchor.UpperLeft, new Color(0.8f, 0.8f, 0.8f));
            UiFactory.SetAnchoredRect(_achievementText.rectTransform, new Vector2(0.1f, 0.4f), new Vector2(0.1f, 0.4f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(700f, 200f));

            // 返回按钮
            _backButton = UiFactory.CreateButton(panel.transform, "BackButton", "返回", new Color(0.3f, 0.4f, 0.6f), OnBackClicked);
            UiFactory.SetAnchoredRect(_backButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.05f), new Vector2(0.5f, 0.05f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(160f, 45f));
        }

        public override void OnOpen()
        {
            base.OnOpen();
            RefreshData();
        }

        /// <summary>刷新图鉴数据。</summary>
        private void RefreshData()
        {
            if (!CollectionManager.HasInstance) return;

            // 刷新怪物图鉴
            var bestiary = CollectionManager.Instance.GetBestiary();
            var bestiaryContent = "";
            foreach (var entry in bestiary.Values)
            {
                bestiaryContent += $"{entry.MonsterName}: 击杀 {entry.KillCount} 次\n";
            }
            if (string.IsNullOrEmpty(bestiaryContent))
            {
                bestiaryContent = "还没有发现怪物，去战斗吧！";
            }
            _bestiaryText.text = bestiaryContent;

            // 刷新成就
            var achievements = CollectionManager.Instance.GetUnlockedAchievements();
            var achievementContent = $"已解锁成就: {achievements.Count} 个\n";
            foreach (var id in achievements)
            {
                achievementContent += $"- {id}\n";
            }
            _achievementText.text = achievementContent;
        }

        private void OnBackClicked()
        {
            UIManager.Instance.Close<CollectionView>();
        }
    }
}
