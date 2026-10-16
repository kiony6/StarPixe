using StarPixelSurge.Framework;
using UnityEngine;

namespace StarPixelSurge.UI
{
    /// <summary>
    /// 对局界面根节点。创建 Canvas 与事件系统，构建并注册 HUD、升级、结算三个视图。
    /// </summary>
    public class GameplayUIRoot : MonoBehaviour
    {
        public static GameplayUIRoot Instance { get; private set; }

        private Canvas _canvas;
        private ClassSelectionView _classSelection;
        private RoomView _room;
        private HudView _hud;
        private BossHealthBarView _bossBar;
        private UpgradeSelectionView _upgrade;
        private ResultView _result;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            BuildUi();
            EventBus.Subscribe<MatchStartedEvent>(OnMatchStarted);
            EventBus.Subscribe<MatchEndedEvent>(OnMatchEnded);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<MatchStartedEvent>(OnMatchStarted);
            EventBus.Unsubscribe<MatchEndedEvent>(OnMatchEnded);
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void BuildUi()
        {
            UiFactory.EnsureEventSystem();
            _canvas = UiFactory.CreateCanvas("GameplayCanvas", 100);

            _classSelection = CreateView<ClassSelectionView>("ClassSelectionView");
            _classSelection.Build(_canvas.transform);
            UIManager.Instance.Register(_classSelection);

            _room = CreateView<RoomView>("RoomView");
            _room.Build(_canvas.transform);
            UIManager.Instance.Register(_room);

            _hud = CreateView<HudView>("HudView");
            _hud.Build(_canvas.transform);
            UIManager.Instance.Register(_hud);

            _bossBar = CreateView<BossHealthBarView>("BossHealthBarView");
            _bossBar.Build(_canvas.transform);
            UIManager.Instance.Register(_bossBar);

            _upgrade = CreateView<UpgradeSelectionView>("UpgradeSelectionView");
            _upgrade.Build(_canvas.transform);
            UIManager.Instance.Register(_upgrade);

            _result = CreateView<ResultView>("ResultView");
            _result.Build(_canvas.transform);
            UIManager.Instance.Register(_result);
        }

        private T CreateView<T>(string name) where T : UIView
        {
            var go = new GameObject(name);
            go.transform.SetParent(_canvas.transform, false);
            return go.AddComponent<T>();
        }

        private void OnMatchStarted(MatchStartedEvent evt)
        {
            UIManager.Instance.Close<ClassSelectionView>();
            UIManager.Instance.Close<RoomView>();
            UIManager.Instance.Close<ResultView>();
            UIManager.Instance.Close<BossHealthBarView>();
            UIManager.Instance.Open<HudView>();
        }

        private void OnMatchEnded(MatchEndedEvent evt)
        {
            UIManager.Instance.Close<HudView>();
            UIManager.Instance.Close<BossHealthBarView>();
        }

        public void ShowClassSelection()
        {
            UIManager.Instance.Close<HudView>();
            UIManager.Instance.Close<BossHealthBarView>();
            UIManager.Instance.Close<ResultView>();
            UIManager.Instance.Close<RoomView>();
            UIManager.Instance.Open<ClassSelectionView>();
        }

        /// <summary>进入联机房间与准备态界面。</summary>
        public void ShowRoom()
        {
            UIManager.Instance.Close<HudView>();
            UIManager.Instance.Close<BossHealthBarView>();
            UIManager.Instance.Close<ResultView>();
            UIManager.Instance.Close<ClassSelectionView>();
            UIManager.Instance.Open<RoomView>();
        }

        public HudView Hud => _hud;

        public Canvas Canvas => _canvas;
    }
}
