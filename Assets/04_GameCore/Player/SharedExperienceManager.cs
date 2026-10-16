using System;
using System.Collections.Generic;
using UnityEngine;

namespace StarPixelSurge.GameCore
{
    /// <summary>
    /// 共享经验管理器：多人联机时，所有玩家共享经验池，等级共享，但技能选择独立。
    /// </summary>
    public class SharedExperienceManager : MonoSingleton<SharedExperienceManager>
    {
        [SerializeField] private int sharedLevel = 1;
        [SerializeField] private float baseExperienceToNext = 5f;
        [SerializeField] private float growthFactor = 1.35f;

        private float _currentExperience;
        public event Action<int> OnSharedLevelUp;
        public event Action<float, float, float> OnExperienceGained;

        public int SharedLevel => sharedLevel;
        public float CurrentExperience => _currentExperience;
        public float RequiredExperience => GetRequired(sharedLevel);

        protected override void OnSingletonAwake()
        {
            ResetProgress();
        }

        /// <summary>添加共享经验（所有玩家共享）。</summary>
        public void AddSharedExperience(float amount)
        {
            if (amount <= 0f) return;

            _currentExperience += amount;
            OnExperienceGained?.Invoke(amount, _currentExperience, RequiredExperience);

            var guard = 0;
            while (_currentExperience >= RequiredExperience && guard++ < 1000)
            {
                _currentExperience -= RequiredExperience;
                sharedLevel++;
                OnSharedLevelUp?.Invoke(sharedLevel);
                Debug.Log($"[SharedExp] 共享等级提升到: {sharedLevel}");
            }
        }

        public void ResetProgress()
        {
            sharedLevel = 1;
            _currentExperience = 0f;
        }

        private float GetRequired(int forLevel)
        {
            return Mathf.Round(baseExperienceToNext * Mathf.Pow(growthFactor, forLevel - 1));
        }
    }
}
