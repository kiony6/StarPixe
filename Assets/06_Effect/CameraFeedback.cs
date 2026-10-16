using StarPixelSurge.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace StarPixelSurge.Effect
{
    /// <summary>
    /// 镜头反馈组件。挂在主相机上，负责震屏、闪屏（曝光/调色占位）与顿帧，
    /// 输出 <see cref="Offset"/> 由 <c>CameraFollow</c> 叠加到跟随位置之上。
    ///
    /// 低配档位按需求 3.3 / 10.1 关闭震屏与发光叠加；项目未引入后处理栈，
    /// 曝光提升与调色以全屏叠加色块作为等价占位表现。
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class CameraFeedback : MonoBehaviour
    {
        private static CameraFeedback _instance;

        public static CameraFeedback Instance => _instance;

        public static bool HasInstance => _instance != null;

        private GraphicsTier _tier = GraphicsTier.Standard;

        private float _shakeTime;
        private float _shakeDuration;
        private float _shakeIntensity;
        private float _shakeFrequency = 28f;
        private Vector2 _offset;

        private float _flashTime;
        private float _flashDuration;
        private Color _flashColor = Color.white;
        private Image _flashImage;
        private CanvasGroup _flashGroup;

        private int _freezeFrames;
        private bool _frozen;
        private float _previousTimeScale = 1f;

        /// <summary>当前震屏位移，供相机跟随叠加。</summary>
        public Vector2 Offset => _offset;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }

            _instance = this;
            if (GraphicsTierController.HasInstance)
            {
                _tier = GraphicsTierController.Instance.Tier;
            }

            EnsureOverlay();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }

            RestoreTimeScale();
        }

        private void OnDisable()
        {
            RestoreTimeScale();
        }

        public void SetTier(GraphicsTier tier)
        {
            _tier = tier;
            if (tier == GraphicsTier.Low)
            {
                Cancel();
            }
        }

        /// <summary>触发震屏。低配档位忽略。</summary>
        public void Shake(float intensity, float duration)
        {
            if (_tier == GraphicsTier.Low || intensity <= 0f || duration <= 0f)
            {
                return;
            }

            // 取更强的一次，避免弱反馈打断强反馈。
            if (intensity >= _shakeIntensity || _shakeTime <= 0f)
            {
                _shakeIntensity = intensity;
                _shakeDuration = duration;
                _shakeTime = duration;
            }
        }

        /// <summary>全屏闪光，用于曝光提升与调色脉冲的占位表现。低配档位忽略。</summary>
        public void Flash(Color color, float duration)
        {
            if (_tier == GraphicsTier.Low || duration <= 0f)
            {
                return;
            }

            EnsureOverlay();
            _flashColor = color;
            _flashDuration = duration;
            _flashTime = duration;
            if (_flashGroup != null)
            {
                _flashGroup.alpha = Mathf.Max(_flashGroup.alpha, Mathf.Clamp01(color.a));
            }
        }

        /// <summary>请求顿帧若干逻辑帧，采用缩放时间实现。低配档位忽略。</summary>
        public void FreezeFrame(int frames)
        {
            if (_tier == GraphicsTier.Low || frames <= 0)
            {
                return;
            }

            _freezeFrames = Mathf.Max(_freezeFrames, frames);
        }

        /// <summary>终极技表现：同时触发震屏、闪白与顿帧。</summary>
        public void Ultimate(float intensity = 0.55f, int freezeFrames = 4)
        {
            Shake(intensity, 0.35f);
            Flash(new Color(1f, 0.95f, 0.8f), 0.18f);
            FreezeFrame(freezeFrames);
        }

        /// <summary>清除所有反馈并恢复时间缩放。</summary>
        public void Cancel()
        {
            _shakeTime = 0f;
            _shakeIntensity = 0f;
            _offset = Vector2.zero;
            _flashTime = 0f;
            if (_flashGroup != null)
            {
                _flashGroup.alpha = 0f;
            }

            _freezeFrames = 0;
            RestoreTimeScale();
        }

        private void Update()
        {
            var unscaledDelta = Time.unscaledDeltaTime;

            UpdateShake(unscaledDelta);
            UpdateFlash(unscaledDelta);
            UpdateFreeze();
        }

        private void UpdateShake(float unscaledDelta)
        {
            if (_shakeTime <= 0f)
            {
                _offset = Vector2.zero;
                return;
            }

            _shakeTime -= unscaledDelta;
            var progress = _shakeDuration > 0f ? Mathf.Clamp01(_shakeTime / _shakeDuration) : 0f;
            var amplitude = _shakeIntensity * progress;
            var angle = Time.unscaledTime * _shakeFrequency;
            _offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle * 1.37f)) * amplitude;

            if (_shakeTime <= 0f)
            {
                _offset = Vector2.zero;
                _shakeIntensity = 0f;
            }
        }

        private void UpdateFlash(float unscaledDelta)
        {
            if (_flashGroup == null || _flashTime <= 0f)
            {
                return;
            }

            _flashTime -= unscaledDelta;
            var progress = _flashDuration > 0f ? Mathf.Clamp01(_flashTime / _flashDuration) : 0f;
            _flashGroup.alpha = _flashColor.a * progress;
            if (_flashImage != null)
            {
                _flashImage.color = _flashColor;
            }

            if (_flashTime <= 0f)
            {
                _flashGroup.alpha = 0f;
            }
        }

        private void UpdateFreeze()
        {
            if (_freezeFrames <= 0)
            {
                if (_frozen)
                {
                    RestoreTimeScale();
                }

                return;
            }

            if (!_frozen)
            {
                _previousTimeScale = Time.timeScale <= 0f ? 1f : Time.timeScale;
                Time.timeScale = 0f;
                _frozen = true;
            }

            _freezeFrames--;
            if (_freezeFrames <= 0)
            {
                RestoreTimeScale();
            }
        }

        private void RestoreTimeScale()
        {
            if (!_frozen)
            {
                return;
            }

            Time.timeScale = _previousTimeScale <= 0f ? 1f : _previousTimeScale;
            _frozen = false;
            _freezeFrames = 0;
        }

        /// <summary>懒创建全屏叠加层，用于闪光与曝光占位表现。</summary>
        private void EnsureOverlay()
        {
            if (_flashGroup != null)
            {
                return;
            }

            var canvasGo = new GameObject("CameraFeedbackOverlay");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            _flashGroup = canvasGo.AddComponent<CanvasGroup>();
            _flashGroup.alpha = 0f;
            _flashGroup.blocksRaycasts = false;
            _flashGroup.interactable = false;

            var imageGo = new GameObject("Flash", typeof(RectTransform), typeof(Image));
            imageGo.transform.SetParent(canvasGo.transform, false);
            var rect = (RectTransform)imageGo.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            _flashImage = imageGo.GetComponent<Image>();
            _flashImage.raycastTarget = false;
            _flashImage.color = _flashColor;
        }
    }
}
