using System;
using UnityEngine;

namespace StarPixelSurge.Config
{
    public enum ClassRole
    {
        Output = 0,
        Melee = 1,
        Support = 2,
        Summon = 3,
        Control = 4
    }

    /// <summary>
    /// 职业定义：基础属性、起始技能与技能池。
    /// </summary>
    [CreateAssetMenu(menuName = "StarPixelSurge/Config/Class", fileName = "Class_")]
    public class ClassConfig : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        public ClassRole Role;
        public Sprite Portrait;

        [Header("Base Stats")]
        public float BaseHealth = 100f;
        public float BaseMoveSpeed = 5f;
        public float BaseDamageMultiplier = 1f;

        [Header("Skills")]
        public string StartingSkillId;
        public string[] SkillPool = Array.Empty<string>();
    }
}
