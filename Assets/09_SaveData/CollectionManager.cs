using System;
using System.Collections.Generic;
using StarPixelSurge.Framework;
using UnityEngine;

namespace StarPixelSurge.SaveData
{
    /// <summary>
    /// 成就定义。
    /// </summary>
    [Serializable]
    public class AchievementConfig
    {
        public string Id;
        public string DisplayName;
        public string Description;
        public int TargetValue;
    }

    /// <summary>
    /// 图鉴条目：怪物图鉴。
    /// </summary>
    [Serializable]
    public class BestiaryEntry
    {
        public string MonsterId;
        public string MonsterName;
        public int KillCount;
        public bool IsDiscovered;
    }

    /// <summary>
    /// 图鉴/成就管理器：管理怪物图鉴和成就解锁。
    /// </summary>
    public class CollectionManager : MonoSingleton<CollectionManager>
    {
        private Dictionary<string, BestiaryEntry> _bestiary = new Dictionary<string, BestiaryEntry>();
        private Dictionary<string, int> _achievementProgress = new Dictionary<string, int>();
        private HashSet<string> _unlockedAchievements = new HashSet<string>();

        /// <summary>怪物击杀事件。</summary>
        public event Action<BestiaryEntry> OnMonsterKilled;

        /// <summary>成就解锁事件。</summary>
        public event Action<AchievementConfig> OnAchievementUnlocked;

        protected override void OnSingletonAwake()
        {
            LoadData();
        }

        /// <summary>记录怪物击杀。</summary>
        public void RecordMonsterKill(string monsterId, string monsterName)
        {
            if (!_bestiary.TryGetValue(monsterId, out var entry))
            {
                entry = new BestiaryEntry
                {
                    MonsterId = monsterId,
                    MonsterName = monsterName,
                    IsDiscovered = true,
                };
                _bestiary[monsterId] = entry;
            }

            entry.KillCount++;
            OnMonsterKilled?.Invoke(entry);
            SaveData();
        }

        /// <summary>记录成就进度。</summary>
        public void RecordAchievementProgress(string achievementId, int progress)
        {
            if (_unlockedAchievements.Contains(achievementId))
            {
                return;
            }

            _achievementProgress.TryGetValue(achievementId, out var current);
            current += progress;
            _achievementProgress[achievementId] = current;

            // 检查是否达到目标
            var config = GetAchievementConfig(achievementId);
            if (config != null && current >= config.TargetValue)
            {
                UnlockAchievement(config);
            }
        }

        /// <summary>解锁成就。</summary>
        private void UnlockAchievement(AchievementConfig config)
        {
            if (!_unlockedAchievements.Contains(config.Id))
            {
                _unlockedAchievements.Add(config.Id);
                OnAchievementUnlocked?.Invoke(config);
                SaveData();
            }
        }

        /// <summary>获取成就配置（占位：从配置表读取）。</summary>
        private AchievementConfig GetAchievementConfig(string id)
        {
            // TODO: 从配置表读取
            return null;
        }

        /// <summary>获取所有图鉴条目。</summary>
        public IReadOnlyDictionary<string, BestiaryEntry> GetBestiary() => _bestiary;

        /// <summary>获取已解锁成就。</summary>
        public IReadOnlySet<string> GetUnlockedAchievements() => _unlockedAchievements;

        private void LoadData()
        {
            // TODO: 从存档加载
        }

        private void SaveData()
        {
            // TODO: 保存到存档
        }
    }
}
