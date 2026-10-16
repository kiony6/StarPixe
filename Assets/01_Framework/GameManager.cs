using UnityEngine;
using StarPixelSurge.Framework;

namespace StarPixelSurge.Core
{
    /// <summary>
    /// 全局游戏状态管理器：控制开局、结束、重启、结算。
    /// </summary>
    public class GameManager : MonoSingleton<GameManager>
    {
        public enum GameState
        {
            MainMenu,
            Loading,
            InGame,
            Paused,
            GameOver,
            Victory
        }

        public GameState CurrentState { get; private set; } = GameState.MainMenu;

        public float GameTime { get; private set; }
        public int KillCount { get; private set; }
        public int CurrentWave { get; private set; }

        protected override void OnSingletonAwake()
        {
            ChangeState(GameState.MainMenu);
            Debug.Log("[GameManager] 游戏管理器初始化完成");
        }

        private void Update()
        {
            if (CurrentState == GameState.InGame)
            {
                GameTime += Time.deltaTime;
            }
        }

        /// <summary>改变游戏状态。</summary>
        public void ChangeState(GameState newState)
        {
            CurrentState = newState;
            Debug.Log($"[GameManager] 状态切换: {newState}");
        }

        /// <summary>开始新游戏。</summary>
        public void StartNewGame()
        {
            GameTime = 0f;
            KillCount = 0;
            CurrentWave = 1;
            ChangeState(GameState.InGame);
            Debug.Log("[GameManager] 新游戏开始！");
        }

        /// <summary>玩家死亡。</summary>
        public void OnPlayerDeath()
        {
            ChangeState(GameState.GameOver);
            Debug.Log($"[GameManager] 游戏结束！存活时间: {GameTime:F1}秒, 击杀: {KillCount}");
        }

        /// <summary>击杀怪物计数。</summary>
        public void OnKillMonster()
        {
            KillCount++;
        }

        /// <summary>切换波次。</summary>
        public void OnWaveChange(int wave)
        {
            CurrentWave = wave;
        }

        /// <summary>暂停游戏。</summary>
        public void PauseGame()
        {
            if (CurrentState == GameState.InGame)
            {
                ChangeState(GameState.Paused);
                Time.timeScale = 0f;
            }
        }

        /// <summary>恢复游戏。</summary>
        public void ResumeGame()
        {
            if (CurrentState == GameState.Paused)
            {
                ChangeState(GameState.InGame);
                Time.timeScale = 1f;
            }
        }
    }
}
