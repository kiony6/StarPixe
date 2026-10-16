using System.Collections.Generic;
using StarPixelSurge.Framework;
using StarPixelSurge.GameCore;
using UnityEngine;
using UnityEngine.UI;

namespace StarPixelSurge.UI
{
    /// <summary>
    /// 升级三选一界面。监听 UpgradeOfferedEvent 生成选项卡片，点击后交由 UpgradeManager 结算。
    /// </summary>
    public class UpgradeSelectionView : UIView
    {
        private RectTransform _cardContainer;
        private Text _titleText;
        private readonly List<GameObject> _cards = new List<GameObject>();

        private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.72f);
        private static readonly Color CardColor = new Color(0.1f, 0.12f, 0.2f, 0.98f);
        private static readonly Color CommonColor = new Color(0.55f, 0.7f, 0.95f);
        private static readonly Color RareColor = new Color(0.75f, 0.55f, 1f);
        private static readonly Color EpicColor = new Color(1f, 0.75f, 0.3f);

        public void Build(Transform parent)
        {
            var dim = UiFactory.CreateImage(parent, "UpgradeDim", DimColor);
            dim.raycastTarget = true;
            contentRoot = dim.gameObject;
            var root = (RectTransform)contentRoot.GetComponent<RectTransform>();
            UiFactory.Stretch(root);

            _titleText = UiFactory.CreateNeonText(dim.transform, "Title", "升级！选择一项强化", 44, TextAnchor.UpperCenter, NeonPalette.Gold, 4f);
            UiFactory.SetAnchoredRect(_titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(1000f, 60f));

            _cardContainer = UiFactory.CreateRect(dim.transform, "Cards");
            UiFactory.SetAnchoredRect(_cardContainer, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1160f, 460f));

            var layout = _cardContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 40f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            OnClose();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<UpgradeOfferedEvent>(OnOffered);
            EventBus.Subscribe<UpgradeChosenEvent>(OnChosen);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<UpgradeOfferedEvent>(OnOffered);
            EventBus.Unsubscribe<UpgradeChosenEvent>(OnChosen);
        }

        private void OnOffered(UpgradeOfferedEvent evt)
        {
            Populate(evt.Options, evt.PendingCount);
            UIManager.Instance.PushModal(this);
        }

        private void OnChosen(UpgradeChosenEvent evt)
        {
            // 若仍有多级待选，UpgradeManager 会紧接着再次推送 UpgradeOfferedEvent，此处保持打开。
            if (UpgradeManager.HasInstance && UpgradeManager.Instance.HasPendingChoice)
            {
                return;
            }

            UIManager.Instance.PopModal();
        }

        private void Populate(IReadOnlyList<UpgradeOption> options, int pendingCount)
        {
            ClearCards();

            _titleText.text = pendingCount > 1 ? $"升级！选择一项强化（剩余 {pendingCount} 次）" : "升级！选择一项强化";

            for (var i = 0; i < options.Count; i++)
            {
                CreateCard(options[i], i);
            }
        }

        private void CreateCard(UpgradeOption option, int index)
        {
            var card = UiFactory.CreateImage(_cardContainer, $"Card_{index}", CardColor);
            var rect = card.rectTransform;
            rect.sizeDelta = new Vector2(340f, 420f);

            var layout = card.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = 340f;
            layout.preferredHeight = 420f;

            var accent = UiFactory.CreateImage(card.transform, "Accent", RarityColor(option.Rarity));
            UiFactory.SetAnchoredRect(accent.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 8f));
            UiFactory.AddOutline(card, RarityColor(option.Rarity), 3f);
            UiFactory.AddGlow(accent, NeonPalette.WithAlpha(RarityColor(option.Rarity), 0.7f), 5f);

            var title = UiFactory.CreateText(card.transform, "Title", option.Title, 30, TextAnchor.UpperCenter, Color.white);
            UiFactory.SetAnchoredRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(-40f, 80f));

            var description = UiFactory.CreateText(card.transform, "Description", option.Description, 24, TextAnchor.MiddleCenter, new Color(0.85f, 0.9f, 1f));
            UiFactory.SetAnchoredRect(description.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(-40f, -200f));

            var chosenIndex = index;
            var button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = card;
            var colors = button.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.9f);
            colors.pressedColor = new Color(0.7f, 0.8f, 1f, 0.9f);
            button.colors = colors;
            button.onClick.AddListener(() => OnCardClicked(chosenIndex));

            _cards.Add(card.gameObject);
        }

        private void OnCardClicked(int index)
        {
            if (UpgradeManager.HasInstance)
            {
                UpgradeManager.Instance.Choose(index);
            }
        }

        private void ClearCards()
        {
            for (var i = 0; i < _cards.Count; i++)
            {
                if (_cards[i] != null)
                {
                    Destroy(_cards[i]);
                }
            }

            _cards.Clear();
        }

        private static Color RarityColor(int rarity)
        {
            switch (rarity)
            {
                case 2:
                    return EpicColor;
                case 1:
                    return RareColor;
                default:
                    return CommonColor;
            }
        }
    }
}
