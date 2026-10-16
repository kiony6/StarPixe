using StarPixelSurge.Framework;
using StarPixelSurge.GameCore;
using StarPixelSurge.Network;
using UnityEngine;
using UnityEngine.UI;

namespace StarPixelSurge.UI
{
    /// <summary>
    /// 对局 HUD：显示生命、经验、层数、击杀与时长。
    /// </summary>
    public class HudView : UIView
    {
        private Image _healthFill;
        private Image _expFill;
        private Text _healthText;
        private Text _levelText;
        private Text _layerText;
        private Text _killsText;
        private Text _timerText;
        private Text _messageText;
        private float _messageTimer;

        private static readonly Color TrackColor = new Color(0.08f, 0.08f, 0.12f, 0.85f);
        private static readonly Color HealthColor = new Color(0.95f, 0.25f, 0.35f);
        private static readonly Color ExpColor = new Color(0.35f, 0.85f, 0.95f);

        public void Build(Transform parent)
        {
            contentRoot = new GameObject("HudContent");
            contentRoot.transform.SetParent(transform, false);
            var root = (RectTransform)contentRoot.AddComponent<RectTransform>();
            UiFactory.Stretch(root);

            var panel = contentRoot.transform;

            _healthFill = UiFactory.CreateBar(panel, "HealthBar", TrackColor, HealthColor, out _healthText, "HP");
            UiFactory.SetAnchoredRect(_healthFill.transform.parent.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(360f, 30f));
            ConfigureFill(_healthFill);

            _expFill = UiFactory.CreateBar(panel, "ExpBar", TrackColor, ExpColor, out var expLabel, string.Empty);
            expLabel.text = string.Empty;
            UiFactory.SetAnchoredRect(_expFill.transform.parent.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -64f), new Vector2(360f, 16f));
            ConfigureFill(_expFill);

            _levelText = UiFactory.CreateText(panel, "LevelText", "Lv.1", 22, TextAnchor.MiddleLeft, Color.white);
            UiFactory.SetAnchoredRect(_levelText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -92f), new Vector2(360f, 28f));

            _layerText = UiFactory.CreateText(panel, "LayerText", "波次 1", 30, TextAnchor.UpperCenter, new Color(1f, 0.85f, 0.35f));
            UiFactory.SetAnchoredRect(_layerText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(500f, 40f));

            _killsText = UiFactory.CreateText(panel, "KillsText", "击杀 0", 26, TextAnchor.UpperRight, Color.white);
            UiFactory.SetAnchoredRect(_killsText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(320f, 34f));

            _timerText = UiFactory.CreateText(panel, "TimerText", "00:00", 24, TextAnchor.UpperRight, new Color(0.8f, 0.85f, 1f));
            UiFactory.SetAnchoredRect(_timerText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -62f), new Vector2(320f, 30f));

            _messageText = UiFactory.CreateText(panel, "MessageText", string.Empty, 32, TextAnchor.UpperCenter, Color.white);
            UiFactory.SetAnchoredRect(_messageText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(1000f, 44f));

            OnClose();
        }

        protected override void OnViewOpened()
        {
            RefreshHealth();
            RefreshExperience();
            RefreshLayer();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<PlayerHealthChangedEvent>(OnHealthChanged);
            EventBus.Subscribe<ExperienceGainedEvent>(OnExperienceGained);
            EventBus.Subscribe<LevelUpEvent>(OnLevelUp);
            EventBus.Subscribe<WaveLayerChangedEvent>(OnWaveLayerChanged);
            EventBus.Subscribe<MonsterKilledEvent>(OnMonsterKilled);
            EventBus.Subscribe<RoomMessageEvent>(OnRoomMessage);
            EventBus.Subscribe<LootPickedEvent>(OnLootPicked);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<PlayerHealthChangedEvent>(OnHealthChanged);
            EventBus.Unsubscribe<ExperienceGainedEvent>(OnExperienceGained);
            EventBus.Unsubscribe<LevelUpEvent>(OnLevelUp);
            EventBus.Unsubscribe<WaveLayerChangedEvent>(OnWaveLayerChanged);
            EventBus.Unsubscribe<MonsterKilledEvent>(OnMonsterKilled);
            EventBus.Unsubscribe<RoomMessageEvent>(OnRoomMessage);
            EventBus.Unsubscribe<LootPickedEvent>(OnLootPicked);
        }

        private void Update()
        {
            if (!IsOpen || _timerText == null)
            {
                return;
            }

            var elapsed = GameManager.HasInstance ? GameManager.Instance.MatchElapsed : 0f;
            var minutes = Mathf.FloorToInt(elapsed / 60f);
            var seconds = Mathf.FloorToInt(elapsed % 60f);
            _timerText.text = $"{minutes:00}:{seconds:00}";

            var kills = GameManager.HasInstance ? GameManager.Instance.Kills : 0;
            _killsText.text = $"击杀 {kills}";

            if (_messageTimer > 0f)
            {
                _messageTimer -= Time.unscaledDeltaTime;
                if (_messageTimer <= 0f && _messageText != null)
                {
                    _messageText.text = string.Empty;
                }
            }
        }

        private static void ConfigureFill(Image fill)
        {
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;
        }

        private void OnHealthChanged(PlayerHealthChangedEvent evt)
        {
            RefreshHealth();
        }

        private void OnExperienceGained(ExperienceGainedEvent evt)
        {
            RefreshExperience();
        }

        private void OnLevelUp(LevelUpEvent evt)
        {
            RefreshExperience();
        }

        private void OnWaveLayerChanged(WaveLayerChangedEvent evt)
        {
            RefreshLayer();
        }

        private void OnMonsterKilled(MonsterKilledEvent evt)
        {
        }

        private void OnRoomMessage(RoomMessageEvent evt)
        {
            if (_messageText == null)
            {
                return;
            }

            _messageText.text = evt.Message;
            _messageText.color = evt.Color;
            _messageTimer = 3f;
        }

        private void OnLootPicked(LootPickedEvent evt)
        {
            if (_messageText == null)
            {
                return;
            }

            _messageText.text = $"拾取 {evt.DisplayName}：{evt.Description}";
            _messageText.color = evt.Color;
            _messageTimer = 2f;
        }

        private void RefreshHealth()
        {
            if (_healthFill == null)
            {
                return;
            }

            var player = ResolveLocalStats();
            if (player == null)
            {
                return;
            }

            var max = Mathf.Max(1f, player.MaxHealth);
            _healthFill.fillAmount = Mathf.Clamp01(player.CurrentHealth / max);
            if (_healthText != null)
            {
                _healthText.text = $"{Mathf.CeilToInt(player.CurrentHealth)} / {Mathf.CeilToInt(max)}";
            }
        }

        /// <summary>
        /// 解析本机玩家的数值容器。联机对局优先取本地分身，
        /// 避免误取远端分身导致血条显示其他玩家的生命值。
        /// </summary>
        private static PlayerStats ResolveLocalStats()
        {
            if (NetworkPlayerRegistry.HasInstance && NetworkPlayerRegistry.Instance.LocalAvatar != null)
            {
                return NetworkPlayerRegistry.Instance.LocalAvatar.Stats;
            }

            if (PlayerController.Instance != null)
            {
                return PlayerController.Instance.GetComponent<PlayerStats>();
            }

            return FindObjectOfType<PlayerStats>();
        }

        private void RefreshExperience()
        {
            if (_expFill == null)
            {
                return;
            }

            var experience = FindObjectOfType<PlayerExperience>();
            if (experience == null)
            {
                return;
            }

            _expFill.fillAmount = experience.Normalized;
            if (_levelText != null)
            {
                _levelText.text = $"Lv.{experience.Level}";
            }
        }

        private void RefreshLayer()
        {
            if (_layerText == null)
            {
                return;
            }

            var layer = GameManager.HasInstance ? GameManager.Instance.WaveLayer : 0;
            _layerText.text = $"波次 {layer}";
        }
    }
}
