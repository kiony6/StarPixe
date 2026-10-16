using System;
using System.Collections.Generic;
using UnityEngine;

namespace StarPixelSurge.SaveData
{
    /// <summary>
    /// 图鉴条目。
    /// </summary>
    [Serializable]
    public class CollectionEntry
    {
        public string Id;
        public string Name;
        public string Description;
        public bool IsUnlocked;
        public int KillCount;
    }

    /// <summary>
    /// 成就条目。
    /// </summary>
    [Serializable]
    public class AchievementEntry
    {
        public string Id;
        public string Name;
        public string Description;
        public bool IsCompleted;
        public int Progress;
        public int Target;
        public int Reward; // 奖励积分
    }

    /// <summary>
    /// 图鉴成就管理器：管理怪物图鉴和玩家成就。
    /// </summary>
    public class CollectionManager : MonoBehaviour
    {
        public static CollectionManager Instance { get; private set; }
        public static bool HasInstance => Instance != null;

        public List<CollectionEntry> MonsterCollection = new List<CollectionEntry>();
        public List<AchievementEntry> Achievements = new List<AchievementEntry>();

        public event Action<CollectionEntry> OnMonsterUnlocked;
        public event Action<AchievementEntry> OnAchievementCompleted;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                LoadDefaults();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>获取怪物图鉴列表。</summary>
        public List<CollectionEntry> GetBestiary()
        {
            return MonsterCollection;
        }

        /// <summary>获取已解锁的成就列表。</summary>
        public List<AchievementEntry> GetUnlockedAchievements()
        {
            return Achievements.FindAll(a => a.IsCompleted);
        }

        /// <summary>记录击杀怪物，解锁图鉴。</summary>
        public void RecordMonsterKill(string monsterId, string monsterName)
        {
            var entry = MonsterCollection.Find(m => m.Id == monsterId);
            if (entry == null)
            {
                entry = new CollectionEntry
                {
                    Id = monsterId,
                    Name = monsterName,
                    Description = $"击杀 {monsterName}",
                    IsUnlocked = false,
                    KillCount = 0
                };
                MonsterCollection.Add(entry);
            }

            entry.KillCount++;

            if (!entry.IsUnlocked)
            {
                entry.IsUnlocked = true;
                OnMonsterUnlocked?.Invoke(entry);
                Debug.Log($"[Collection] 解锁图鉴: {monsterName}");
            }

            // 检查成就进度
            CheckAchievementProgress("kill_total", 1);
        }

        /// <summary>记录成就进度。</summary>
        public void RecordAchievementProgress(string achievementId, int progress)
        {
            var achievement = Achievements.Find(a => a.Id == achievementId);
            if (achievement == null || achievement.IsCompleted) return;

            achievement.Progress += progress;

            if (achievement.Progress >= achievement.Target)
            {
                achievement.IsCompleted = true;
                OnAchievementCompleted?.Invoke(achievement);
                Debug.Log($"[Achievement] 完成成就: {achievement.Name}");
            }
        }

        private void CheckAchievementProgress(string achievementId, int progress)
        {
            RecordAchievementProgress(achievementId, progress);
        }

        private void LoadDefaults()
        {
            // 默认怪物图鉴
            if (MonsterCollection.Count == 0)
            {
                MonsterCollection.Add(new CollectionEntry { Id = "grunt", Name = "小怪", Description = "最基础的怪物" });
                MonsterCollection.Add(new CollectionEntry { Id = "fast", Name = "迅捷怪", Description = "移动速度很快" });
                MonsterCollection.Add(new CollectionEntry { Id = "tank", Name = "坦克怪", Description = "血量很高" });
                MonsterCollection.Add(new CollectionEntry { Id = "boss1", Name = "Boss", Description = "强大的首领" });
            }

            // 默认成就
            if (Achievements.Count == 0)
            {
                Achievements.Add(new AchievementEntry { Id = "kill_10", Name = "初出茅庐", Description = "击杀10个怪物", Target = 10, Reward = 100 });
                Achievements.Add(new AchievementEntry { Id = "kill_100", Name = "百人斩", Description = "击杀100个怪物", Target = 100, Reward = 500 });
                Achievements.Add(new AchievementEntry { Id = "kill_1000", Name = "怪物克星", Description = "击杀1000个怪物", Target = 1000, Reward = 2000 });
                Achievements.Add(new AchievementEntry { Id = "reach_level_10", Name = "成长之路", Description = "达到10级", Target = 10, Reward = 300 });
            }
        }
    }
}
