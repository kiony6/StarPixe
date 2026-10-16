using UnityEngine;
using StarPixelSurge.Framework;

namespace StarPixelSurge.GameCore
{
    /// <summary>
    /// 游戏场景启动器：进入对局场景后初始化所有游戏系统。
    /// </summary>
    public class GameSceneBootstrap : MonoSingleton<GameSceneBootstrap>
    {
        [Header("系统初始化顺序")]
        [SerializeField] private bool initNetwork = true;
        [SerializeField] private bool initWaveManager = true;
        [SerializeField] private bool initPlayer = true;
        [SerializeField] private bool initUI = true;

        protected override void OnSingletonAwake()
        {
            Debug.Log("[GameScene] 游戏场景启动中...");
            InitializeSystems();
        }

        private void InitializeSystems()
        {
            if (initNetwork)
            {
                Debug.Log("[GameScene] 初始化网络系统...");
                // NetworkSystem会自动初始化
            }

            if (initWaveManager)
            {
                Debug.Log("[GameScene] 初始化波次系统...");
                if (WaveManager.HasInstance)
                {
                    WaveManager.Instance.StartWaves();
                }
            }

            if (initPlayer)
            {
                Debug.Log("[GameScene] 初始化玩家...");
                // 玩家会通过NetworkManager自动生成
            }

            if (initUI)
            {
                Debug.Log("[GameScene] 初始化UI...");
                // UI会自动加载
            }

            Debug.Log("[GameScene] 游戏场景启动完成！");
        }
    }
}
