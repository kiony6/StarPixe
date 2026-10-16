using StarPixelSurge.Framework;
using UnityEngine;

namespace StarPixelSurge.Effect
{
    /// <summary>
    /// 特效分级控制器。持有当前 Graphics_Tier，变更时广播给特效与镜头反馈，
    /// 并持续采样帧率；持续低于阈值时提示玩家建议档位（需求 10.1-10.5）。
    /// </summary>
    public class GraphicsTierController : MonoSingleton<GraphicsTierController>
    {
        [SerializeField] private GraphicsTier tier = GraphicsTier.Standard;
        [SerializeField] private float lowFpsThreshold = 40f;
        [SerializeField] private float sustainedSeconds = 6f;
        [SerializeField] private float notifyCooldownSeconds = 30f;

        private float _fpsSmooth = 60f;
        private float _lowFpsTimer;
        private float _notifyCooldown;

        /// <summary>当前生效档位。</summary>
        public GraphicsTier Tier => tier;

        /// <summary>依据帧率计算出的建议档位。</summary>
        public GraphicsTier RecommendedTier { get; private set; } = GraphicsTier.Standard;

        protected override void OnSingletonAwake()
        {
            ApplyTier(tier);
        }

        /// <summary>应用档位：同步到特效与镜头反馈，并广播变更事件。</summary>
        public void ApplyTier(GraphicsTier value)
        {
            tier = value;
            EffectManager.Instance.SetTier(value);
            if (CameraFeedback.HasInstance)
            {
                CameraFeedback.Instance.SetTier(value);
            }

            EventBus.Publish(new GraphicsTierChangedEvent(value));
        }

        public void SetTier(GraphicsTier value)
        {
            ApplyTier(value);
        }

        /// <summary>循环切换档位，供设置界面调用。</summary>
        public void CycleTier()
        {
            ApplyTier(NextTier(tier));
        }

        /// <summary>应用建议档位。</summary>
        public void ApplyRecommendedTier()
        {
            ApplyTier(RecommendedTier);
        }

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            if (dt <= 0f)
            {
                return;
            }

            HandleTierHotkey();

            _fpsSmooth = Mathf.Lerp(_fpsSmooth, 1f / dt, 0.05f);
            if (_notifyCooldown > 0f)
            {
                _notifyCooldown -= dt;
            }

            if (tier == GraphicsTier.Low || _fpsSmooth >= lowFpsThreshold)
            {
                _lowFpsTimer = 0f;
                return;
            }

            _lowFpsTimer += dt;
            if (_lowFpsTimer < sustainedSeconds)
            {
                return;
            }

            _lowFpsTimer = 0f;
            var candidate = _fpsSmooth < lowFpsThreshold * 0.6f ? GraphicsTier.Low : GraphicsTier.Standard;
            RecommendedTier = candidate;
            if (candidate != tier)
            {
                NotifyRecommended(candidate);
            }
        }

        private void NotifyRecommended(GraphicsTier candidate)
        {
            if (_notifyCooldown > 0f)
            {
                return;
            }

            _notifyCooldown = notifyCooldownSeconds;
            var label = candidate == GraphicsTier.Low ? "低配"
                : candidate == GraphicsTier.Standard ? "标准" : "极致";
            EventBus.Publish(new RoomMessageEvent(
                $"帧率偏低，建议将特效档位切换为「{label}」",
                new Color(1f, 0.8f, 0.4f)));
        }

        /// <summary>测试期热键：F10 循环切换档位并提示当前档位，后续由设置界面取代。</summary>
        private void HandleTierHotkey()
        {
            if (!Input.GetKeyDown(KeyCode.F10))
            {
                return;
            }

            CycleTier();
            EventBus.Publish(new RoomMessageEvent(
                $"特效档位：{TierLabel(tier)}（F10 切换）",
                new Color(0.6f, 0.9f, 1f)));
        }

        private static string TierLabel(GraphicsTier value)
        {
            switch (value)
            {
                case GraphicsTier.Low:
                    return "低配";
                case GraphicsTier.Ultra:
                    return "极致";
                default:
                    return "标准";
            }
        }

        private static GraphicsTier NextTier(GraphicsTier current)
        {
            switch (current)
            {
                case GraphicsTier.Low:
                    return GraphicsTier.Standard;
                case GraphicsTier.Standard:
                    return GraphicsTier.Ultra;
                default:
                    return GraphicsTier.Low;
            }
        }
    }
}
