using StarPixelSurge.Framework;
using UnityEngine;

namespace StarPixelSurge.Audio
{
    /// <summary>
    /// 音频反馈调度器。监听技能、击杀、升级、首领、终极技与波次事件，
    /// 驱动 <see cref="AudioManager"/> 播放对应音效并按波次强度切换 BGM 分段（需求 14）。
    /// </summary>
    public class AudioFeedback : MonoSingleton<AudioFeedback>
    {
        private int _layer = 1;
        private bool _bossActive;
        private bool _hasHealth;
        private float _lastHealth;

        protected override void OnSingletonAwake()
        {
            EventBus.Subscribe<SkillCastEvent>(OnSkillCast);
            EventBus.Subscribe<MonsterKilledEvent>(OnMonsterKilled);
            EventBus.Subscribe<LevelUpEvent>(OnLevelUp);
            EventBus.Subscribe<BossSpawnedEvent>(OnBossSpawned);
            EventBus.Subscribe<BossDefeatedEvent>(OnBossDefeated);
            EventBus.Subscribe<UltimateTriggeredEvent>(OnUltimateTriggered);
            EventBus.Subscribe<WaveLayerChangedEvent>(OnWaveLayerChanged);
            EventBus.Subscribe<LootPickedEvent>(OnLootPicked);
            EventBus.Subscribe<PlayerHealthChangedEvent>(OnPlayerHealthChanged);
        }

        protected override void OnDestroy()
        {
            EventBus.Unsubscribe<SkillCastEvent>(OnSkillCast);
            EventBus.Unsubscribe<MonsterKilledEvent>(OnMonsterKilled);
            EventBus.Unsubscribe<LevelUpEvent>(OnLevelUp);
            EventBus.Unsubscribe<BossSpawnedEvent>(OnBossSpawned);
            EventBus.Unsubscribe<BossDefeatedEvent>(OnBossDefeated);
            EventBus.Unsubscribe<UltimateTriggeredEvent>(OnUltimateTriggered);
            EventBus.Unsubscribe<WaveLayerChangedEvent>(OnWaveLayerChanged);
            EventBus.Unsubscribe<LootPickedEvent>(OnLootPicked);
            EventBus.Unsubscribe<PlayerHealthChangedEvent>(OnPlayerHealthChanged);
            base.OnDestroy();
        }

        private static void Play(string sfxId)
        {
            if (AudioManager.HasInstance)
            {
                AudioManager.Instance.PlaySfx(sfxId);
            }
        }

        private void OnSkillCast(SkillCastEvent evt)
        {
            Play(AudioManager.SfxSkill);
        }

        private void OnMonsterKilled(MonsterKilledEvent evt)
        {
            Play(AudioManager.SfxKill);
        }

        private void OnLevelUp(LevelUpEvent evt)
        {
            Play(AudioManager.SfxLevelUp);
        }

        private void OnBossSpawned(BossSpawnedEvent evt)
        {
            _bossActive = true;
            ApplyIntensity();
            Play(AudioManager.SfxBoss);
        }

        private void OnBossDefeated(BossDefeatedEvent evt)
        {
            _bossActive = false;
            ApplyIntensity();
        }

        private void OnUltimateTriggered(UltimateTriggeredEvent evt)
        {
            if (AudioManager.HasInstance)
            {
                AudioManager.Instance.PlayUltimate();
            }
        }

        private void OnWaveLayerChanged(WaveLayerChangedEvent evt)
        {
            _layer = evt.Layer;
            ApplyIntensity();
        }

        private void OnLootPicked(LootPickedEvent evt)
        {
            Play(AudioManager.SfxPickup);
        }

        private void OnPlayerHealthChanged(PlayerHealthChangedEvent evt)
        {
            if (_hasHealth && evt.Current < _lastHealth - 0.01f)
            {
                Play(AudioManager.SfxHurt);
            }

            _hasHealth = true;
            _lastHealth = evt.Current;
        }

        private void ApplyIntensity()
        {
            if (!AudioManager.HasInstance)
            {
                return;
            }

            var intensity = _bossActive ? MusicIntensity.Boss : LayerToIntensity(_layer);
            AudioManager.Instance.SetIntensity((int)intensity);
        }

        private static MusicIntensity LayerToIntensity(int layer)
        {
            if (layer <= 1)
            {
                return MusicIntensity.Calm;
            }

            return layer <= 5 ? MusicIntensity.Battle : MusicIntensity.Frenzy;
        }
    }
}
