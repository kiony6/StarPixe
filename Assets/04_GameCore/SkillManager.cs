using UnityEngine;
using System.Collections.Generic;
using StarPixelSurge.Framework;
using StarPixelSurge.Config;

namespace StarPixelSurge.GameCore
{
    /// <summary>
    /// 技能管理器：所有技能生成、升级、词条赋值、组合技判定。
    /// </summary>
    public class SkillManager : MonoSingleton<SkillManager>
    {
        [Header("技能配置")]
        [SerializeField] private SkillDatabase skillDatabase;

        private readonly List<SkillConfig> _unlockedSkills = new List<SkillConfig>();
        private readonly Dictionary<string, int> _skillLevels = new Dictionary<string, int>();

        protected override void OnSingletonAwake()
        {
            Debug.Log("[SkillManager] 技能管理器初始化完成");
        }

        /// <summary>解锁技能。</summary>
        public void UnlockSkill(string skillId)
        {
            var skill = skillDatabase?.GetSkillById(skillId);
            if (skill == null)
            {
                Debug.LogWarning($"[SkillManager] 未找到技能: {skillId}");
                return;
            }

            if (!_unlockedSkills.Contains(skill))
            {
                _unlockedSkills.Add(skill);
                _skillLevels[skillId] = 1;
                Debug.Log($"[SkillManager] 解锁技能: {skill.Name} Lv.1");
            }
        }

        /// <summary>升级技能。</summary>
        public void LevelUpSkill(string skillId)
        {
            if (_skillLevels.ContainsKey(skillId))
            {
                _skillLevels[skillId]++;
                var level = _skillLevels[skillId];
                Debug.Log($"[SkillManager] 技能升级: {skillId} Lv.{level}");

                // 检查是否触发组合技
                CheckComboSkills(skillId);
            }
        }

        /// <summary>获取技能等级。</summary>
        public int GetSkillLevel(string skillId)
        {
            return _skillLevels.TryGetValue(skillId, out var level) ? level : 0;
        }

        /// <summary>获取已解锁技能列表。</summary>
        public List<SkillConfig> GetUnlockedSkills()
        {
            return new List<SkillConfig>(_unlockedSkills);
        }

        /// <summary>检查组合技。</summary>
        private void CheckComboSkills(string upgradedSkillId)
        {
            // TODO: 实现组合技判定逻辑
            // 当两个技能都满级时，触发组合技解锁
            Debug.Log($"[SkillManager] 检查组合技: {upgradedSkillId}");
        }

        /// <summary>重置技能。</summary>
        public void ResetSkills()
        {
            _unlockedSkills.Clear();
            _skillLevels.Clear();
        }
    }
}
