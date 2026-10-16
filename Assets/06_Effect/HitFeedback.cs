using StarPixelSurge.Framework;
using UnityEngine;

namespace StarPixelSurge.Effect
{
    /// <summary>命中档位，按伤害量划分，用于选择不同强度的打击反馈。</summary>
    public enum DamageTier
    {
        Light = 0,
        Heavy = 1,
        Massive = 2
    }

    /// <summary>
    /// 打击反馈调度器。监听伤害、终极技与首领事件，按档位触发特效、闪光与震屏。
    /// 低配档位的震屏与发光叠加由 <see cref="CameraFeedback"/> 直接忽略（需求 3.3）。
    /// </summary>
    public class HitFeedback : MonoSingleton<HitFeedback>
    {
        private const string HitEffectId = "fx_hit";

        [SerializeField] private float heavyDamageThreshold = 40f;
        [SerializeField] private float massiveDamageThreshold = 120f;

        protected override void OnSingletonAwake()
        {
            EventBus.Subscribe<DamageDealtEvent>(OnDamageDealt);
            EventBus.Subscribe<UltimateTriggeredEvent>(OnUltimateTriggered);
            EventBus.Subscribe<BossSpawnedEvent>(OnBossSpawned);
            EventBus.Subscribe<BossEnragedEvent>(OnBossEnraged);
        }

        protected override void OnDestroy()
        {
            EventBus.Unsubscribe<DamageDealtEvent>(OnDamageDealt);
            EventBus.Unsubscribe<UltimateTriggeredEvent>(OnUltimateTriggered);
            EventBus.Unsubscribe<BossSpawnedEvent>(OnBossSpawned);
            EventBus.Unsubscribe<BossEnragedEvent>(OnBossEnraged);
            base.OnDestroy();
        }

        private void OnDamageDealt(DamageDealtEvent evt)
        {
            var tier = Classify(evt.Damage, evt.Critical);

            if (EffectManager.HasInstance)
            {
                EffectManager.Instance.Play(HitEffectId, evt.Position);
            }

            var feedback = CameraFeedback.Instance;
            if (feedback == null)
            {
                return;
            }

            switch (tier)
            {
                case DamageTier.Massive:
                    feedback.Shake(evt.Critical ? 0.22f : 0.18f, 0.16f);
                    feedback.Flash(NeonPalette.WithAlpha(NeonPalette.Gold, 0.14f), 0.12f);
                    break;
                case DamageTier.Heavy:
                    feedback.Shake(evt.Critical ? 0.12f : 0.08f, 0.1f);
                    feedback.Flash(NeonPalette.WithAlpha(NeonPalette.Cyan, 0.08f), 0.07f);
                    break;
                default:
                    // 轻击只保留命中特效，避免高频震屏造成眩晕。
                    break;
            }
        }

        private void OnUltimateTriggered(UltimateTriggeredEvent evt)
        {
            var feedback = CameraFeedback.Instance;
            if (feedback != null)
            {
                feedback.Ultimate();
            }
        }

        private void OnBossSpawned(BossSpawnedEvent evt)
        {
            var feedback = CameraFeedback.Instance;
            if (feedback != null)
            {
                feedback.Shake(0.25f, 0.4f);
                feedback.Flash(NeonPalette.WithAlpha(NeonPalette.Crimson, 0.16f), 0.25f);
            }
        }

        private void OnBossEnraged(BossEnragedEvent evt)
        {
            var feedback = CameraFeedback.Instance;
            if (feedback != null)
            {
                feedback.Shake(0.3f, 0.45f);
                feedback.Flash(NeonPalette.WithAlpha(NeonPalette.Amber, 0.18f), 0.2f);
            }
        }

        private DamageTier Classify(float damage, bool critical)
        {
            if (critical || damage >= massiveDamageThreshold)
            {
                return DamageTier.Massive;
            }

            return damage >= heavyDamageThreshold ? DamageTier.Heavy : DamageTier.Light;
        }
    }
}
