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

    /// <summary>拾取掉落物，获得词缀加成。</summary>
    public readonly struct LootPickedEvent
    {
        public readonly string DisplayName;
        public readonly string Description;
        public readonly Color Color;

        public LootPickedEvent(string displayName, string description, Color color)
        {
            DisplayName = displayName;
            Description = description;
            Color = color;
        }
    }

    /// <summary>首领登场，UI 显示独立血条。</summary>
    public readonly struct BossSpawnedEvent
    {
        public readonly GameObject Boss;
        public readonly string DisplayName;
        public readonly float MaxHealth;

        public BossSpawnedEvent(GameObject boss, string displayName, float maxHealth)
        {
            Boss = boss;
            DisplayName = displayName;
            MaxHealth = maxHealth;
        }
    }

    /// <summary>首领血量变化。</summary>
    public readonly struct BossHealthChangedEvent
    {
        public readonly GameObject Boss;
        public readonly float Current;
        public readonly float Max;

        public BossHealthChangedEvent(GameObject boss, float current, float max)
        {
            Boss = boss;
            Current = current;
            Max = max;
        }
    }

    /// <summary>首领进入狂暴阶段。</summary>
    public readonly struct BossEnragedEvent
    {
        public readonly GameObject Boss;
        public readonly string DisplayName;

        public BossEnragedEvent(GameObject boss, string displayName)
        {
            Boss = boss;
            DisplayName = displayName;
        }
    }

    /// <summary>首领被击败，UI 隐藏血条。</summary>
    public readonly struct BossDefeatedEvent
    {
        public readonly GameObject Boss;

        public BossDefeatedEvent(GameObject boss)
        {
            Boss = boss;
        }
    }
}
