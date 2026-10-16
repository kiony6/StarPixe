using System;
using System.Collections.Generic;
using StarPixelSurge.Config;
using StarPixelSurge.Framework;
using UnityEngine;

namespace StarPixelSurge.GameCore
{
    public enum UpgradeKind
    {
        MaxHealth = 0,
        MoveSpeed = 1,
        Damage = 2,
        CritChance = 3,
        PickupRange = 4,
        SkillLevel = 5,
        NewSkill = 6
    }

    [Serializable]
    public class UpgradeOption
    {
        public string Title;
        public string Description;
        public UpgradeKind Kind;
        public float Value;
        public string SkillId;
        public int Rarity;

        public UpgradeOption(string title, string description, UpgradeKind kind, float value, int rarity = 0, string skillId = null)
        {
            Title = title;
            Description = description;
            Kind = kind;
            Value = value;
            Rarity = rarity;
            SkillId = skillId;
        }
    }

    /// <summary>升级三选一选项生成完毕事件，由 UI 订阅展示。</summary>
    public readonly struct UpgradeOfferedEvent
    {
        public readonly IReadOnlyList<UpgradeOption> Options;
        public readonly int PendingCount;

        public UpgradeOfferedEvent(IReadOnlyList<UpgradeOption> options, int pendingCount)
        {
            Options = options;
            PendingCount = pendingCount;
        }
    }

    /// <summary>玩家选定升级事件。</summary>
    public readonly struct UpgradeChosenEvent
    {
        public readonly UpgradeOption Option;

        public UpgradeChosenEvent(UpgradeOption option)
        {
            Option = option;
        }
    }

    /// <summary>
    /// 升级管理器：监听升级事件、生成三选一随机选项、应用所选升级并恢复对局。
    /// 支持单帧多次升级的排队处理。
    /// </summary>
    public class UpgradeManager : MonoSingleton<UpgradeManager>
    {
        [SerializeField] private PlayerStats playerStats;
        [SerializeField] private int optionsPerLevel = 3;

        private readonly List<UpgradeOption> _currentOptions = new List<UpgradeOption>();
        private int _pendingLevelUps;

        public IReadOnlyList<UpgradeOption> CurrentOptions => _currentOptions;

        public bool HasPendingChoice => _currentOptions.Count > 0;

        public void BindPlayer(PlayerStats stats)
        {
            playerStats = stats;
            _pendingLevelUps = 0;
            _currentOptions.Clear();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<LevelUpEvent>(OnLevelUp);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<LevelUpEvent>(OnLevelUp);
        }

        private void OnLevelUp(LevelUpEvent evt)
        {
            _pendingLevelUps++;
            if (_currentOptions.Count == 0)
            {
                ShowNext();
            }
            else
            {
                Debug.Log($"[UpgradeManager] Level up queued. Pending={_pendingLevelUps}");
            }
        }

        private void ShowNext()
        {
            RollOptions(optionsPerLevel);
            if (GameManager.HasInstance)
            {
                GameManager.Instance.EnterLevelUp();
            }

            EventBus.Publish(new UpgradeOfferedEvent(_currentOptions, _pendingLevelUps));
        }

        public void Choose(int index)
        {
            if (index < 0 || index >= _currentOptions.Count)
            {
                return;
            }

            var option = _currentOptions[index];
            Apply(option);
            _currentOptions.Clear();
            _pendingLevelUps = Mathf.Max(0, _pendingLevelUps - 1);
            EventBus.Publish(new UpgradeChosenEvent(option));

            if (_pendingLevelUps > 0)
            {
                ShowNext();
                return;
            }

            if (GameManager.HasInstance)
            {
                GameManager.Instance.ExitLevelUp();
            }
        }

        private void RollOptions(int count)
        {
            _currentOptions.Clear();

            var pool = new List<UpgradeOption>
            {
                new UpgradeOption("强化体魄", "最大生命 +20", UpgradeKind.MaxHealth, 20f),
                new UpgradeOption("迅捷步伐", "移动速度 +0.5", UpgradeKind.MoveSpeed, 0.5f),
                new UpgradeOption("锋利打击", "伤害倍率 +10%", UpgradeKind.Damage, 0.1f, 1),
                new UpgradeOption("致命弱点", "暴击率 +5%", UpgradeKind.CritChance, 0.05f, 1),
                new UpgradeOption("磁力吸取", "拾取范围 +0.5", UpgradeKind.PickupRange, 0.5f)
            };

            if (SkillManager.HasInstance)
            {
                foreach (var pair in SkillManager.Instance.SkillLevels)
                {
                    var config = ConfigService.Instance.GetSkill(pair.Key);
                    if (config == null || pair.Value >= config.MaxLevel)
                    {
                        continue;
                    }

                    pool.Add(new UpgradeOption($"强化·{config.DisplayName}", $"{config.DisplayName} 等级 +1（Lv{pair.Value} -> Lv{pair.Value + 1}）", UpgradeKind.SkillLevel, 1f, 2, pair.Key));
                }

                foreach (var config in ConfigService.Instance.GetAllSkills())
                {
                    if (SkillManager.Instance.HasSkill(config.Id))
                    {
                        continue;
                    }

                    pool.Add(new UpgradeOption($"习得·{config.DisplayName}", $"获得技能 {config.DisplayName}", UpgradeKind.NewSkill, 1f, 2, config.Id));
                }
            }

            while (_currentOptions.Count < count && pool.Count > 0)
            {
                var pick = UnityEngine.Random.Range(0, pool.Count);
                _currentOptions.Add(pool[pick]);
                pool.RemoveAt(pick);
            }
        }

        private void Apply(UpgradeOption option)
        {
            switch (option.Kind)
            {
                case UpgradeKind.MaxHealth:
                    if (playerStats != null)
                    {
                        playerStats.AddMaxHealth(option.Value);
                    }

                    break;
                case UpgradeKind.MoveSpeed:
                    if (playerStats != null)
                    {
                        playerStats.AddMoveSpeed(option.Value);
                    }

                    break;
                case UpgradeKind.Damage:
                    if (playerStats != null)
                    {
                        playerStats.AddDamageMultiplier(option.Value);
                    }

                    break;
                case UpgradeKind.CritChance:
                    if (playerStats != null)
                    {
                        playerStats.AddCritChance(option.Value);
                    }

                    break;
                case UpgradeKind.PickupRange:
                    if (playerStats != null)
                    {
                        playerStats.AddPickupRange(option.Value);
                    }

                    break;
                case UpgradeKind.SkillLevel:
                case UpgradeKind.NewSkill:
                    if (SkillManager.HasInstance)
                    {
                        SkillManager.Instance.LevelUpSkill(option.SkillId);
                    }

                    break;
            }
        }
    }
}
