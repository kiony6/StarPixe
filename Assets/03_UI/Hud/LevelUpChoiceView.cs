using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace StarPixelSurge.UI
{
    /// <summary>
    /// 升级选择界面：升级时弹出，玩家选择新技能或词缀。
    /// </summary>
    public class LevelUpChoiceView : MonoBehaviour
    {
        private RectTransform _panel;
        private Text _titleText;
        private Transform _choiceContainer;
        private Button _closeButton;

        private System.Action<int> _onChoiceSelected;
        private readonly List<GameObject> _choiceButtons = new List<GameObject>();

        public void Build(Transform parent)
        {
            // 半透明背景
            var bg = UiFactory.CreateImage(parent, "LevelUpBg", new Color(0f, 0f, 0f, 0.8f));
            _panel = bg.GetComponent<RectTransform>();
            UiFactory.Stretch(_panel);

            // 标题
            _titleText = UiFactory.CreateText(bg.transform, "Title", "升级！选择奖励", 36, TextAnchor.MiddleCenter, new Color(0.2f, 0.9f, 1f));
            UiFactory.SetAnchoredRect(_titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(600f, 50f));

            // 选项容器
            var container = UiFactory.CreateImage(bg.transform, "ChoiceContainer", new Color(0.1f, 0.12f, 0.2f, 0.9f));
            _choiceContainer = container.GetComponent<RectTransform>();
            UiFactory.SetAnchoredRect(_choiceContainer, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(700f, 400f));

            // 边框
            var outline = container.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.2f, 0.9f, 1f, 1f);
            outline.effectDistance = new Vector2(2f, 2f);

            gameObject.SetActive(false);
        }

        /// <summary>显示选择界面。</summary>
        public void Show(List<ChoiceItem> choices, System.Action<int> onSelected)
        {
            _onChoiceSelected = onSelected;
            gameObject.SetActive(true);
            Time.timeScale = 0f;

            ClearChoices();

            for (var i = 0; i < choices.Count; i++)
            {
                CreateChoiceButton(choices[i], i);
            }
        }

        /// <summary>隐藏选择界面。</summary>
        public void Hide()
        {
            gameObject.SetActive(false);
            Time.timeScale = 1f;
        }

        private void CreateChoiceButton(ChoiceItem choice, int index)
        {
            var go = UiFactory.CreateButton(_choiceContainer, $"Choice_{index}", choice.Title, choice.Color, () => OnChoiceClicked(index));
            UiFactory.SetAnchoredRect(go.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f - index * 100f), new Vector2(600f, 80f));

            // 描述文字
            var desc = UiFactory.CreateText(go.transform, "Desc", choice.Description, 16, TextAnchor.MiddleCenter, new Color(0.8f, 0.85f, 0.95f));
            UiFactory.SetAnchoredRect(desc.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(580f, 30f));

            _choiceButtons.Add(go.gameObject);
        }

        private void OnChoiceClicked(int index)
        {
            _onChoiceSelected?.Invoke(index);
            Hide();
        }

        private void ClearChoices()
        {
            foreach (var btn in _choiceButtons)
            {
                if (btn != null) Destroy(btn);
            }
            _choiceButtons.Clear();
        }
    }

    /// <summary>
    /// 选项数据。
    /// </summary>
    public class ChoiceItem
    {
        public string Title;
        public string Description;
        public Color Color = Color.cyan;
    }
}
