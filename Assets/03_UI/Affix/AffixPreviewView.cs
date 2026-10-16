using System.Collections.Generic;
using StarPixelSurge.Config;
using StarPixelSurge.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace StarPixelSurge.UI
{
    /// <summary>
    /// 词缀预览面板。对局中按 Tab 打开，罗列全部词缀的名称、描述、稀有度与数值，
    /// 并高亮本局已获得的词缀。不占用模态栈，避免与升级三选一冲突。
    /// </summary>
    public class AffixPreviewView : UIView
    {
        private RectTransform _listContainer;
        private Text _subtitleText;
        private Text _emptyText;

        private readonly List<AffixConfig> _catalog = new List<AffixConfig>();
        private readonly Dictionary<string, int> _acquired = new Dictionary<string, int>();
        private readonly List<GameObject> _rows = new List<GameObject>();

        private bool _catalogDirty = true;

        private const string ToggleHint = "Tab 词缀预览";

        private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.78f);
        private static readonly Color PanelColor = new Color(0.07f, 0.08f, 0.13f, 0.98f);
        private static readonly Color RowColor = new Color(0.11f, 0.13f, 0.2f, 0.95f);
        private static readonly Color RowOwnedColor = new Color(0.16f, 0.2f, 0.3f, 0.98f);
        private static readonly Color CommonColor = new Color(0.55f, 0.7f, 0.95f);
        private static readonly Color RareColor = new Color(0.75f, 0.55f, 1f);
        private static readonly Color EpicColor = new Color(1f, 0.75f, 0.3f);

        public void Build(Transform parent)
        {
            var dim = UiFactory.CreateImage(parent, "AffixPreviewDim", DimColor);
            dim.raycastTarget = true;
            contentRoot = dim.gameObject;
            var root = (RectTransform)contentRoot.GetComponent<RectTransform>();
            UiFactory.Stretch(root);

            var panel = UiFactory.CreateImage(dim.transform, "Panel", PanelColor);
            UiFactory.SetAnchoredRect(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200f, 760f));

            var title = UiFactory.CreateText(panel.transform, "Title", "词缀图鉴", 42, TextAnchor.UpperCenter, Color.white);
            UiFactory.SetAnchoredRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(1000f, 54f));

            _subtitleText = UiFactory.CreateText(panel.transform, "Subtitle", string.Empty, 24, TextAnchor.UpperCenter, new Color(0.8f, 0.85f, 1f));
            UiFactory.SetAnchoredRect(_subtitleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -88f), new Vector2(1000f, 32f));

            _listContainer = UiFactory.CreateRect(panel.transform, "List");
            UiFactory.SetAnchoredRect(_listContainer, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -18f), new Vector2(1080f, 560f));

            var layout = _listContainer.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            _emptyText = UiFactory.CreateText(panel.transform, "Empty", "暂无词缀配置", 28, TextAnchor.MiddleCenter, new Color(0.7f, 0.72f, 0.8f));
            UiFactory.SetAnchoredRect(_emptyText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -18f), new Vector2(1080f, 60f));

            var footer = UiFactory.CreateText(panel.transform, "Footer", "按 Tab 关闭", 22, TextAnchor.LowerCenter, new Color(0.7f, 0.75f, 0.85f));
            UiFactory.SetAnchoredRect(footer.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(1000f, 30f));

            OnClose();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<LootPickedEvent>(OnLootPicked);
            EventBus.Subscribe<MatchStartedEvent>(OnMatchStarted);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<LootPickedEvent>(OnLootPicked);
            EventBus.Unsubscribe<MatchStartedEvent>(OnMatchStarted);
        }

        private void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Tab))
            {
                return;
            }

            if (!GameManager.HasInstance || GameManager.Instance.State != GameState.Playing)
            {
                if (IsOpen)
                {
                    Close();
                }

                return;
            }

            Toggle();
        }

        protected override void OnViewOpened()
        {
            Refresh();
        }

        /// <summary>开关预览面板。关闭时直接隐藏，避免干扰升级等模态窗口。</summary>
        public void Toggle()
        {
            if (IsOpen)
            {
                Close();
            }
            else
            {
                OnOpen();
            }
        }

        public void Close()
        {
            OnClose();
        }

        public static string ToggleHintText => ToggleHint;

        private void OnMatchStarted(MatchStartedEvent evt)
        {
            _acquired.Clear();
            _catalogDirty = true;
            if (IsOpen)
            {
                Refresh();
            }
        }

        private void OnLootPicked(LootPickedEvent evt)
        {
            if (string.IsNullOrEmpty(evt.DisplayName))
            {
                return;
            }

            if (_acquired.TryGetValue(evt.DisplayName, out var count))
            {
                _acquired[evt.DisplayName] = count + 1;
            }
            else
            {
                _acquired[evt.DisplayName] = 1;
            }

            if (IsOpen)
            {
                Refresh();
            }
        }

        private void Refresh()
        {
            EnsureCatalog();
            ClearRows();

            var hasContent = _catalog.Count > 0;
            _emptyText.gameObject.SetActive(!hasContent);
            _subtitleText.text = hasContent ? $"已获得 {CountOwned()} / {_catalog.Count}" : string.Empty;

            if (!hasContent)
            {
                return;
            }

            for (var i = 0; i < _catalog.Count; i++)
            {
                CreateRow(_catalog[i], i);
            }
        }

        private void EnsureCatalog()
        {
            if (!_catalogDirty && _catalog.Count > 0)
            {
                return;
            }

            _catalogDirty = false;
            _catalog.Clear();

            if (!ConfigService.HasInstance)
            {
                return;
            }

            foreach (var affix in ConfigService.Instance.GetAllAffixes())
            {
                if (affix != null)
                {
                    _catalog.Add(affix);
                }
            }

            _catalog.Sort(CompareAffix);
        }

        private static int CompareAffix(AffixConfig a, AffixConfig b)
        {
            var byRarity = ((int)b.Rarity).CompareTo((int)a.Rarity);
            return byRarity != 0 ? byRarity : string.CompareOrdinal(a.DisplayName, b.DisplayName);
        }

        private int CountOwned()
        {
            var owned = 0;
            for (var i = 0; i < _catalog.Count; i++)
            {
                if (IsOwned(_catalog[i]))
                {
                    owned++;
                }
            }

            return owned;
        }

        private bool IsOwned(AffixConfig affix)
        {
            if (affix == null || string.IsNullOrEmpty(affix.DisplayName))
            {
                return false;
            }

            return _acquired.ContainsKey(affix.DisplayName);
        }

        private void CreateRow(AffixConfig affix, int index)
        {
            var owned = IsOwned(affix);
            var row = UiFactory.CreateImage(_listContainer, $"Row_{index}", owned ? RowOwnedColor : RowColor);
            row.rectTransform.sizeDelta = new Vector2(0f, 96f);

            var layout = row.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 96f;

            var accent = UiFactory.CreateImage(row.transform, "Accent", RarityColor(affix.Rarity));
            UiFactory.SetAnchoredRect(accent.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(8f, 0f));

            var nameLabel = owned ? $"{affix.DisplayName}  已获得" : affix.DisplayName;
            var nameText = UiFactory.CreateText(row.transform, "Name", nameLabel, 28, TextAnchor.UpperLeft, RarityColor(affix.Rarity));
            UiFactory.SetAnchoredRect(nameText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.7f, 1f), new Vector2(0f, 1f), new Vector2(24f, -10f), new Vector2(-24f, 0f));

            var rarityText = UiFactory.CreateText(row.transform, "Rarity", RarityLabel(affix.Rarity), 22, TextAnchor.UpperRight, RarityColor(affix.Rarity));
            UiFactory.SetAnchoredRect(rarityText.rectTransform, new Vector2(0.7f, 0.5f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -12f), new Vector2(0f, 0f));

            var description = string.IsNullOrEmpty(affix.Description) ? FormatEffect(affix) : affix.Description;
            var descriptionText = UiFactory.CreateText(row.transform, "Description", description, 23, TextAnchor.MiddleLeft, new Color(0.85f, 0.9f, 1f));
            UiFactory.SetAnchoredRect(descriptionText.rectTransform, new Vector2(0f, 0f), new Vector2(0.68f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, 8f), new Vector2(-24f, 0f));

            var valueText = UiFactory.CreateText(row.transform, "Value", FormatEffect(affix), 22, TextAnchor.MiddleRight, new Color(0.65f, 0.72f, 0.85f));
            UiFactory.SetAnchoredRect(valueText.rectTransform, new Vector2(0.68f, 0f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20f, 8f), new Vector2(0f, 0f));

            _rows.Add(row.gameObject);
        }

        private void ClearRows()
        {
            for (var i = 0; i < _rows.Count; i++)
            {
                if (_rows[i] != null)
                {
                    Destroy(_rows[i]);
                }
            }

            _rows.Clear();
        }

        /// <summary>由 StatKey/Value/IsMultiplicative 生成结构化数值文本，补充配置描述之外的口径信息。</summary>
        private static string FormatEffect(AffixConfig affix)
        {
            if (affix == null)
            {
                return string.Empty;
            }

            var label = StatLabel(affix.StatKey);
            var sign = affix.Value >= 0f ? "+" : "-";
            var magnitude = Mathf.Abs(affix.Value);
            var isPercent = IsPercentStat(affix.StatKey);

            var valueText = isPercent
                ? $"{sign}{Mathf.RoundToInt(magnitude * 100f)}%"
                : $"{sign}{magnitude:0.##}";

            var mode = affix.IsMultiplicative ? "乘算" : "加算";
            return string.IsNullOrEmpty(label) ? $"{valueText}（{mode}）" : $"{label} {valueText}（{mode}）";
        }

        private static bool IsPercentStat(string statKey)
        {
            switch (statKey)
            {
                case "damage":
                case "range":
                case "cooldown":
                case "critChance":
                    return true;
                default:
                    return false;
            }
        }

        private static string StatLabel(string statKey)
        {
            switch (statKey)
            {
                case "damage":
                    return "技能伤害";
                case "range":
                    return "技能范围";
                case "cooldown":
                    return "技能冷却";
                case "critChance":
                    return "暴击率";
                case "moveSpeed":
                    return "移动速度";
                case "projectileCount":
                    return "弹道数量";
                default:
                    return statKey;
            }
        }

        private static string RarityLabel(AffixRarity rarity)
        {
            switch (rarity)
            {
                case AffixRarity.Epic:
                    return "史诗";
                case AffixRarity.Rare:
                    return "稀有";
                default:
                    return "普通";
            }
        }

        private static Color RarityColor(AffixRarity rarity)
        {
            switch (rarity)
            {
                case AffixRarity.Epic:
                    return EpicColor;
                case AffixRarity.Rare:
                    return RareColor;
                default:
                    return CommonColor;
            }
        }
    }
}
