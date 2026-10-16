using System.Collections.Generic;
using StarPixelSurge.Config;
using StarPixelSurge.Framework;
using StarPixelSurge.GameCore;
using UnityEngine;
using UnityEngine.UI;

namespace StarPixelSurge.UI
{
    /// <summary>
    /// 游戏大厅职业选择界面。展示全部职业预览，选定后进入对局。
    /// </summary>
    public class ClassSelectionView : UIView
    {
        private const string FallbackClassId = "thunder_mage";

        private RectTransform _cardContainer;
        private Text _hintText;
        private readonly List<GameObject> _cards = new List<GameObject>();

        private static readonly Color BackdropColor = new Color(0.03f, 0.03f, 0.06f, 0.98f);
        private static readonly Color CardColor = new Color(0.09f, 0.10f, 0.17f, 1f);
        private static readonly Color SelectColor = new Color(0.22f, 0.62f, 0.95f, 1f);

        public void Build(Transform parent)
        {
            var backdrop = UiFactory.CreateImage(parent, "ClassSelectionBackdrop", BackdropColor);
            backdrop.raycastTarget = true;
            contentRoot = backdrop.gameObject;
            UiFactory.Stretch((RectTransform)contentRoot.GetComponent<RectTransform>());

            var title = UiFactory.CreateText(backdrop.transform, "Title", "星界像素狂潮", 64, TextAnchor.UpperCenter, new Color(0.7f, 0.9f, 1f));
            UiFactory.SetAnchoredRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(1200f, 80f));

            var subtitle = UiFactory.CreateText(backdrop.transform, "Subtitle", "选择你的职业，进入无尽割草战场", 30, TextAnchor.UpperCenter, new Color(0.75f, 0.78f, 0.9f));
            UiFactory.SetAnchoredRect(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(1200f, 40f));

            _cardContainer = UiFactory.CreateRect(backdrop.transform, "ClassCards");
            UiFactory.SetAnchoredRect(_cardContainer, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(1400f, 620f));

            var layout = _cardContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 32f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            _hintText = UiFactory.CreateText(backdrop.transform, "Hint", "点击职业卡片开始单机对局，或进入联机房间组队", 26, TextAnchor.LowerCenter, new Color(0.7f, 0.75f, 0.9f));
            UiFactory.SetAnchoredRect(_hintText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1200f, 40f));

            var roomButton = UiFactory.CreateButton(backdrop.transform, "RoomButton", "多人联机", new Color(0.25f, 0.6f, 0.5f), OnEnterRoom);
            UiFactory.SetAnchoredRect(roomButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 100f), new Vector2(320f, 64f));

            OnClose();
        }

        protected override void OnViewOpened()
        {
            Populate();
        }

        private void Populate()
        {
            ClearCards();

            var classes = CollectClasses();
            if (classes.Count == 0)
            {
                if (_hintText != null)
                {
                    _hintText.text = "未找到职业配置数据。";
                }

                return;
            }

            foreach (var classConfig in classes)
            {
                CreateCard(classConfig);
            }
        }

        private List<ClassConfig> CollectClasses()
        {
            var list = new List<ClassConfig>();
            if (!ConfigService.HasInstance)
            {
                return list;
            }

            foreach (var classConfig in ConfigService.Instance.GetAllClasses())
            {
                if (classConfig != null)
                {
                    list.Add(classConfig);
                }
            }

            list.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return list;
        }

        private void CreateCard(ClassConfig classConfig)
        {
            var card = UiFactory.CreateImage(_cardContainer, $"Class_{classConfig.Id}", CardColor);
            card.rectTransform.sizeDelta = new Vector2(320f, 560f);

            var layout = card.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = 320f;
            layout.preferredHeight = 560f;

            var roleColor = RoleColor(classConfig.Role);

            var accent = UiFactory.CreateImage(card.transform, "Accent", roleColor);
            UiFactory.SetAnchoredRect(accent.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 10f));

            var portrait = UiFactory.CreateImage(card.transform, "Portrait", roleColor * 0.55f);
            UiFactory.SetAnchoredRect(portrait.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -46f), new Vector2(140f, 140f));

            var name = UiFactory.CreateText(card.transform, "Name", classConfig.DisplayName, 38, TextAnchor.UpperCenter, Color.white);
            UiFactory.SetAnchoredRect(name.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -200f), new Vector2(-30f, 50f));

            var role = UiFactory.CreateText(card.transform, "Role", $"定位：{RoleName(classConfig.Role)}", 24, TextAnchor.UpperCenter, roleColor);
            UiFactory.SetAnchoredRect(role.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -252f), new Vector2(-30f, 34f));

            var stats = UiFactory.CreateText(card.transform, "Stats",
                $"生命   {Mathf.RoundToInt(classConfig.BaseHealth)}\n" +
                $"移速   {classConfig.BaseMoveSpeed:0.0}\n" +
                $"伤害   x{classConfig.BaseDamageMultiplier:0.0}",
                24, TextAnchor.UpperCenter, new Color(0.85f, 0.88f, 1f));
            UiFactory.SetAnchoredRect(stats.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -300f), new Vector2(-30f, 110f));

            var startingSkill = ConfigService.Instance.GetSkill(classConfig.StartingSkillId);
            var skillName = startingSkill != null ? startingSkill.DisplayName : classConfig.StartingSkillId;
            var skill = UiFactory.CreateText(card.transform, "StartingSkill", $"起始技能：{skillName}", 24, TextAnchor.UpperCenter, new Color(1f, 0.85f, 0.45f));
            UiFactory.SetAnchoredRect(skill.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -430f), new Vector2(-30f, 34f));

            var pool = UiFactory.CreateText(card.transform, "Pool", $"技能池：{BuildPoolLabel(classConfig)}", 20, TextAnchor.UpperCenter, new Color(0.7f, 0.75f, 0.9f));
            UiFactory.SetAnchoredRect(pool.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -470f), new Vector2(-30f, 70f));

            var button = UiFactory.CreateButton(card.transform, "SelectButton", "选择", SelectColor, () => OnSelect(classConfig.Id));
            UiFactory.SetAnchoredRect(button.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(240f, 64f));

            _cards.Add(card.gameObject);
        }

        private static string BuildPoolLabel(ClassConfig classConfig)
        {
            if (classConfig.SkillPool == null || classConfig.SkillPool.Length == 0)
            {
                return "无";
            }

            var names = new List<string>();
            foreach (var id in classConfig.SkillPool)
            {
                var skill = ConfigService.HasInstance ? ConfigService.Instance.GetSkill(id) : null;
                names.Add(skill != null ? skill.DisplayName : id);
            }

            return string.Join(" / ", names);
        }

        private void OnSelect(string classId)
        {
            if (string.IsNullOrEmpty(classId))
            {
                classId = FallbackClassId;
            }

            if (GameBootstrap.Instance != null)
            {
                GameBootstrap.Instance.StartSinglePlayer(classId);
            }
        }

        private void OnEnterRoom()
        {
            if (GameplayUIRoot.Instance != null)
            {
                GameplayUIRoot.Instance.ShowRoom();
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

        private static string RoleName(ClassRole role)
        {
            switch (role)
            {
                case ClassRole.Output:
                    return "输出";
                case ClassRole.Melee:
                    return "近战";
                case ClassRole.Support:
                    return "辅助";
                case ClassRole.Summon:
                    return "召唤";
                case ClassRole.Control:
                    return "控制";
                default:
                    return "通用";
            }
        }

        private static Color RoleColor(ClassRole role)
        {
            switch (role)
            {
                case ClassRole.Output:
                    return new Color(0.45f, 0.72f, 1f);
                case ClassRole.Melee:
                    return new Color(1f, 0.5f, 0.4f);
                case ClassRole.Support:
                    return new Color(0.55f, 1f, 0.7f);
                case ClassRole.Summon:
                    return new Color(0.85f, 0.6f, 1f);
                case ClassRole.Control:
                    return new Color(0.6f, 0.9f, 1f);
                default:
                    return new Color(0.8f, 0.8f, 0.8f);
            }
        }
    }
}
