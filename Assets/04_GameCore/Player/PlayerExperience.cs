using StarPixelSurge.Framework;
using UnityEngine;

namespace StarPixelSurge.GameCore
{
    /// <summary>
    /// 玩家经验与等级。击杀怪物获得经验，达到阈值触发升级并广播 LevelUpEvent。
    /// </summary>
    public class PlayerExperience : MonoBehaviour
    {
        [SerializeField] private int level = 1;
        [SerializeField] private float baseExperienceToNext = 5f;
        [SerializeField] private float growthFactor = 1.35f;
        [SerializeField] private float killBonusPerLevel = 0.1f;

        private float _current;

        public int Level => level;

        public float Current => _current;

        public float Required => GetRequired(level);

        public float Normalized => Mathf.Clamp01(Required <= 0f ? 0f : _current / Required);

        private void OnEnable()
        {
            EventBus.Subscribe<MonsterKilledEvent>(OnMonsterKilled);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<MonsterKilledEvent>(OnMonsterKilled);
        }

        public void AddExperience(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            _current += amount;
            EventBus.Publish(new ExperienceGainedEvent(amount, _current, Required));

            var guard = 0;
            while (_current >= Required && guard++ < 1000)
            {
                _current -= Required;
                level++;
                EventBus.Publish(new LevelUpEvent(level));
            }
        }

        private void OnMonsterKilled(MonsterKilledEvent evt)
        {
            AddExperience(evt.ExperienceReward);
        }

        private float GetRequired(int forLevel)
        {
            return Mathf.Round(baseExperienceToNext * Mathf.Pow(growthFactor, forLevel - 1));
        }
    }
}
