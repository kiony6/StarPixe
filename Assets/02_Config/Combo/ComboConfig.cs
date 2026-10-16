using UnityEngine;

namespace StarPixelSurge.Config
{
    /// <summary>
    /// 组合技定义：两个指定技能同时满级时解锁专属全屏特效与联动机制。
    /// </summary>
    [CreateAssetMenu(menuName = "StarPixelSurge/Config/Combo", fileName = "Combo_")]
    public class ComboConfig : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        [TextArea] public string Description;

        public string SkillA;
        public string SkillB;
        public int RequiredLevelA = 5;
        public int RequiredLevelB = 5;

        public string FullScreenEffectId;
        public string LinkedMechanicId;

        [Tooltip("仅联机模式可解锁的组合技")]
        public bool MultiplayerOnly;
    }
}
