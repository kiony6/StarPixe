using System;
using UnityEngine;

namespace StarPixelSurge.Framework
{
    /// <summary>
    /// 一次对局的启动参数。
    /// </summary>
    public readonly struct MatchConfig
    {
        public readonly MatchMode Mode;
        public readonly string ClassId;
        public readonly int Seed;

        public MatchConfig(MatchMode mode, string classId, int seed)
        {
            Mode = mode;
            ClassId = classId;
            Seed = seed;
        }

        public static MatchConfig SinglePlayer(string classId, int seed)
        {
            return new MatchConfig(MatchMode.SinglePlayer, classId, seed);
        }
    }

    /// <summary>
    /// 全局游戏状态与对局生命周期控制。
    /// </summary>
    public class GameManager : MonoSingleton<GameManager>
    {
        public GameState State { get; private set; } = GameState.Boot;

        public MatchConfig CurrentMatch { get; private set; }

        public int WaveLayer { get; private set; }

        public int Kills { get; private set; }

        public float MatchElapsed { get; private set; }

        public event Action<GameState, GameState> StateChanged;

        public event Action<MatchConfig> MatchStarted;

        public event Action<MatchEndedEvent> MatchEnded;

        protected override void OnSingletonAwake()
        {
            EventBus.Subscribe<MonsterKilledEvent>(OnMonsterKilled);
            EventBus.Subscribe<WaveLayerChangedEvent>(OnWaveLayerChanged);
            if (State == GameState.Boot)
            {
                SetState(GameState.MainMenu);
            }
        }

        private void Update()
        {
            if (State == GameState.Playing)
            {
                MatchElapsed += Time.deltaTime;
            }
        }

        protected override void OnDestroy()
        {
            EventBus.Unsubscribe<MonsterKilledEvent>(OnMonsterKilled);
            EventBus.Unsubscribe<WaveLayerChangedEvent>(OnWaveLayerChanged);
            base.OnDestroy();
        }

        public void StartMatch(MatchConfig config)
        {
            CurrentMatch = config;
            Kills = 0;
            WaveLayer = 0;
            MatchElapsed = 0f;

            SetState(GameState.Playing);
            MatchStarted?.Invoke(config);
            EventBus.Publish(new MatchStartedEvent(config.Mode));
        }

        public void CompleteMatch()
        {
            Time.timeScale = 1f;
            var result = new MatchEndedEvent(Mathf.FloorToInt(MatchElapsed), WaveLayer, Kills);
            SetState(GameState.Result);
            MatchEnded?.Invoke(result);
            EventBus.Publish(result);
        }

        public void EnterLevelUp()
        {
            if (State == GameState.Playing)
            {
                SetState(GameState.LevelUp);
                Time.timeScale = 0f;
            }
        }

        public void ExitLevelUp()
        {
            if (State == GameState.LevelUp)
            {
                Time.timeScale = 1f;
                SetState(GameState.Playing);
            }
        }

        public void Pause()
        {
            if (State == GameState.Playing)
            {
                SetState(GameState.Paused);
                Time.timeScale = 0f;
            }
        }

        public void Resume()
        {
            if (State == GameState.Paused)
            {
                Time.timeScale = 1f;
                SetState(GameState.Playing);
            }
        }

        public void ReturnToMainMenu()
        {
            Time.timeScale = 1f;
            SetState(GameState.MainMenu);
        }

        private void SetState(GameState next)
        {
            if (State == next)
            {
                return;
            }

            var previous = State;
            State = next;
            StateChanged?.Invoke(previous, next);
            EventBus.Publish(new GameStateChangedEvent(previous, next));
        }

        private void OnMonsterKilled(MonsterKilledEvent evt)
        {
            Kills++;
        }

        private void OnWaveLayerChanged(WaveLayerChangedEvent evt)
        {
            WaveLayer = evt.Layer;
        }
    }
}
