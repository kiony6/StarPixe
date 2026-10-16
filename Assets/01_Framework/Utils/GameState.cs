using UnityEngine;

namespace StarPixelSurge.Framework
{
    /// <summary>
    /// 全局游戏状态机取值。
    /// </summary>
    public enum GameState
    {
        Boot = 0,
        MainMenu = 1,
        Room = 2,
        Playing = 3,
        LevelUp = 4,
        Paused = 5,
        Result = 6,
        Activation = 7
    }

    /// <summary>
    /// 对局模式。
    /// </summary>
    public enum MatchMode
    {
        SinglePlayer = 0,
        Multiplayer = 1
    }

    /// <summary>
    /// 特效分级档位。
    /// </summary>
    public enum GraphicsTier
    {
        Low = 0,
        Standard = 1,
        Ultra = 2
    }

    /// <summary>
    /// 全局状态切换事件。
    /// </summary>
    public readonly struct GameStateChangedEvent
    {
        public readonly GameState Previous;
        public readonly GameState Current;

        public GameStateChangedEvent(GameState previous, GameState current)
        {
            Previous = previous;
            Current = current;
        }
    }

    /// <summary>
    /// 对局开始事件。
    /// </summary>
    public readonly struct MatchStartedEvent
    {
        public readonly MatchMode Mode;

        public MatchStartedEvent(MatchMode mode)
        {
            Mode = mode;
        }
    }

    /// <summary>
    /// 对局结束事件。
    /// </summary>
    public readonly struct MatchEndedEvent
    {
        public readonly int SurviviedSeconds;
        public readonly int ReachedLayer;
        public readonly int Kills;

        public MatchEndedEvent(int survivedSeconds, int reachedLayer, int kills)
        {
            SurviviedSeconds = survivedSeconds;
            ReachedLayer = reachedLayer;
            Kills = kills;
        }
    }
}
