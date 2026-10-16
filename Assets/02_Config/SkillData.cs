using UnityEngine;

namespace StarPixelSurge.Config
{
    /// <summary>
    /// 技能数据配置（ScriptableObject）。
    /// </summary>
    [CreateAssetMenu(fileName = "NewSkill", menuName = "StarPixelSurge/Skill Data", order = 1)]
    public class SkillData : ScriptableObject
    {
        [Header("基本信息")]
        public string skillId;
        public string skillName;
        [TextArea] public string description;
        public Sprite icon;

        [Header("数值")]
        public float damage = 10f;
        public float cooldown = 1f;
        public float projectileSpeed = 10f;
        public int projectileCount = 1;
        public float range = 10f;

        [Header("特效")]
        public Color projectileColor = Color.cyan;
        public GameObject projectilePrefab;

        [Header("解锁条件")]
        public int requiredLevel = 1;
        public string requiredSkillId;
    }

    /// <summary>
    /// 技能数据库：存储所有技能配置。
    /// </summary>
    [CreateAssetMenu(fileName = "SkillDatabase", menuName = "StarPixelSurge/Skill Database", order = 2)]
    public class SkillDatabase : ScriptableObject
    {
        public System.List<SkillData> allSkills = new System.List<SkillData>();

        /// <summary>根据ID获取技能。</summary>
        public SkillData GetSkillById(string id)
        {
            return allSkills.Find(s => s.skillId == id);
        }
    }
}
