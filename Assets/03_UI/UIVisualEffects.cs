using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace StarPixelSurge.UI
{
    /// <summary>
    /// UI视觉效果增强：霓虹发光、脉动动画、扫描线等。
    /// </summary>
    public static class UIVisualEffects
    {
        /// <summary>添加霓虹发光效果。</summary>
        public static void AddNeonGlow(this Graphic graphic, Color color, float intensity = 1f)
        {
            var outline = graphic.GetComponent<Outline>();
            if (outline == null)
            {
                outline = graphic.gameObject.AddComponent<Outline>();
            }
            outline.effectColor = new Color(color.r, color.g, color.b, 0.8f * intensity);
            outline.effectDistance = new Vector2(2f * intensity, 2f * intensity);
        }

        /// <summary>添加脉动发光动画。</summary>
        public static void AddPulseGlow(this Graphic graphic, Color color, float duration = 1f)
        {
            var pulse = graphic.gameObject.AddComponent<UIPulseEffect>();
            pulse.color = color;
            pulse.duration = duration;
        }

        /// <summary>添加悬浮动画。</summary>
        public static void AddFloatAnimation(this RectTransform rect, float amplitude = 5f, float duration = 2f)
        {
            var floatAnim = rect.gameObject.AddComponent<UIFloatAnimation>();
            floatAnim.amplitude = amplitude;
            floatAnim.duration = duration;
        }
    }

    /// <summary>
    /// UI脉动发光效果。
    /// </summary>
    public class UIPulseEffect : MonoBehaviour
    {
        public Color color = Color.cyan;
        public float duration = 1f;
        private Outline _outline;
        private float _time;

        private void Awake()
        {
            _outline = GetComponent<Outline>();
            if (_outline == null)
            {
                _outline = gameObject.AddComponent<Outline>();
            }
        }

        private void Update()
        {
            _time += Time.deltaTime;
            var pulse = (Mathf.Sin(_time * 2f * Mathf.PI / duration) + 1f) / 2f; // 0-1
            _outline.effectColor = new Color(color.r, color.g, color.b, 0.4f + pulse * 0.4f);
        }
    }

    /// <summary>
    /// UI悬浮动画。
    /// </summary>
    public class UIFloatAnimation : MonoBehaviour
    {
        public float amplitude = 5f;
        public float duration = 2f;
        private Vector2 _startPos;
        private float _time;

        private void Awake()
        {
            _startPos = GetComponent<RectTransform>().anchoredPosition;
        }

        private void Update()
        {
            _time += Time.deltaTime;
            var yOffset = Mathf.Sin(_time * 2f * Mathf.PI / duration) * amplitude;
            GetComponent<RectTransform>().anchoredPosition = _startPos + new Vector2(0f, yOffset);
        }
    }
}
