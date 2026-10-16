using UnityEngine;

namespace StarPixelSurge.Framework
{
    /// <summary>玩家获得经验。</summary>
    public readonly struct ExperienceGainedEvent
    {
        public readonly float Amount;
        public readonly float Current;
        public readonly float Required;

        public ExperienceGainedEvent(float amount, float current, float required)
        {
            Amount = amount;
            Current = current;
            Required = required;
        }
    }

    /// <summary>玩家升级，进入三选一界面。</summary>
    public readonly struct LevelUpEvent
    {
        public readonly int NewLevel;

        public LevelUpEvent(int newLevel)
        {
            NewLevel = newLevel;
        }
    }

    /// <summary>玩家生命值变化。</summary>
    public readonly struct PlayerHealthChangedEvent
    {
        public readonly float Current;
        public readonly float Max;

        public PlayerHealthChangedEvent(float current, float max)
        {
            Current = current;
            Max = max;
        }
    }

    /// <summary>怪物被击杀。</summary>
    public readonly struct MonsterKilledEvent
    {
        public readonly GameObject Monster;
        public readonly Vector2 Position;
        public readonly float ExperienceReward;

        public MonsterKilledEvent(GameObject monster, Vector2 position, float experienceReward)
        {
            Monster = monster;
            Position = position;
            ExperienceReward = experienceReward;
        }
    }

    /// <summary>波次层数递进。</summary>
    public readonly struct WaveLayerChangedEvent
    {
        public readonly int Layer;

        public WaveLayerChangedEvent(int layer)
        {
            Layer = layer;
        }
    }

    /// <summary>界面提示消息（进入特殊房间、触发事件等）。</summary>
    public readonly struct RoomMessageEvent
    {
        public readonly string Message;
        public readonly Color Color;

        public RoomMessageEvent(string message, Color color)
        {
            Message = message;
            Color = color;
        }
    }
}
