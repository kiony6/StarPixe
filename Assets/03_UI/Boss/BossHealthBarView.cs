using StarPixelSurge.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace StarPixelSurge.UI
{
    /// <summary>
    /// 首领独立血条。首领登场时显示，血量归零时隐藏，狂暴阶段切换配色与提示。
    /// </summary>
    public class BossHealthBarView : UIView
    {
        private Image _fill;
        private Text _nameText;
        private Text _phaseText;

        private static readonly Color TrackColor = new Color(0.06f, 0.03f, 0.1f, 0.9f);
        private static readonly Color NormalColor = new Color(0.85f, 0.3f, 0.4f);
        private static readonly Color EnragedColor = new Color(1f, 0.55f, 0.15f);

        public void Build(Transform parent)
        {
            contentRoot = new GameObject("BossHealthBarContent");
            contentRoot.transform.SetParent(transform, false);
            var root = (RectTransform)contentRoot.AddComponent<RectTransform>();
            UiFactory.Stretch(root);

            var panel = contentRoot.transform;

            _fill = UiFactory.CreateBar(panel, "BossBar", TrackColor, NormalColor, out _, string.Empty);
            UiFactory.SetAnchoredRect(_fill.transform.parent.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -44f), new Vector2(1200f, 34f));
            _fill.type = Image.Type.Filled;
            _fill.fillMethod = Image.FillMethod.Horizontal;
            _fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            _fill.fillAmount = 1f;

            _nameText = UiFactory.CreateText(panel, "BossName", "首领", 26, TextAnchor.MiddleCenter, Color.white);
            UiFactory.SetAnchoredRect(_nameText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -44f), new Vector2(1200f, 34f));

            _phaseText = UiFactory.CreateText(panel, "BossPhase", string.Empty, 22, TextAnchor.UpperCenter, EnragedColor);
            UiFactory.SetAnchoredRect(_phaseText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -72f), new Vector2(1200f, 30f));

            OnClose();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<BossSpawnedEvent>(OnBossSpawned);
            EventBus.Subscribe<BossHealthChangedEvent>(OnBossHealthChanged);
            EventBus.Subscribe<BossEnragedEvent>(OnBossEnraged);
            EventBus.Subscribe<BossDefeatedEvent>(OnBossDefeated);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<BossSpawnedEvent>(OnBossSpawned);
            EventBus.Unsubscribe<BossHealthChangedEvent>(OnBossHealthChanged);
            EventBus.Unsubscribe<BossEnragedEvent>(OnBossEnraged);
            EventBus.Unsubscribe<BossDefeatedEvent>(OnBossDefeated);
        }

        private void OnBossSpawned(BossSpawnedEvent evt)
        {
            if (_nameText != null)
            {
                _nameText.text = string.IsNullOrEmpty(evt.DisplayName) ? "首领" : evt.DisplayName;
            }

            if (_phaseText != null)
            {
                _phaseText.text = string.Empty;
            }

            if (_fill != null)
            {
                _fill.color = NormalColor;
                _fill.fillAmount = 1f;
            }

            OnOpen();
        }

        private void OnBossHealthChanged(BossHealthChangedEvent evt)
        {
            if (_fill == null)
            {
                return;
            }

            var max = Mathf.Max(1f, evt.Max);
            _fill.fillAmount = Mathf.Clamp01(evt.Current / max);
        }

        private void OnBossEnraged(BossEnragedEvent evt)
        {
            if (_fill != null)
            {
                _fill.color = EnragedColor;
            }

            if (_phaseText != null)
            {
                _phaseText.text = "狂暴阶段";
            }
        }

        private void OnBossDefeated(BossDefeatedEvent evt)
        {
            OnClose();
        }
    }
}
