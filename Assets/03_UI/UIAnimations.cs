using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace StarPixelSurge.UI
{
    /// <summary>
    /// UI动画工具类：提供各种UI动画效果。
    /// </summary>
    public static class UIAnimations
    {
        /// <summary>面板淡入效果。</summary>
        public static void FadeIn(GameObject panel, float duration = 0.3f)
        {
            var canvasGroup = panel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = panel.AddComponent<CanvasGroup>();
            }
            canvasGroup.alpha = 0f;
            var runner = panel.GetComponent<CoroutineRunner>();
            if (runner == null) runner = panel.AddComponent<CoroutineRunner>();
            runner.StartCoroutine(FadeInCoroutine(canvasGroup, duration));
        }

        /// <summary>面板淡出效果。</summary>
        public static void FadeOut(GameObject panel, float duration = 0.2f)
        {
            var canvasGroup = panel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = panel.AddComponent<CanvasGroup>();
            }
            var runner = panel.GetComponent<CoroutineRunner>();
            if (runner == null) runner = panel.AddComponent<CoroutineRunner>();
            runner.StartCoroutine(FadeOutCoroutine(canvasGroup, duration));
        }

        /// <summary>按钮悬停放大效果。</summary>
        public static void AddButtonHoverScale(Button button, float scaleMultiplier = 1.1f)
        {
            var trigger = button.gameObject.AddComponent<ButtonHoverScale>();
            trigger.hoverScale = scaleMultiplier;
        }

        /// <summary>列表项滑入效果。</summary>
        public static void SlideInFromLeft(RectTransform item, float duration = 0.3f)
        {
            var startPos = item.anchoredPosition;
            item.anchoredPosition = new Vector2(-Screen.width, startPos.y);
            var runner = item.GetComponent<CoroutineRunner>();
            if (runner == null) runner = item.gameObject.AddComponent<CoroutineRunner>();
            runner.StartCoroutine(SlideInCoroutine(item, startPos, duration));
        }

        private static System.Collections.IEnumerator FadeInCoroutine(CanvasGroup group, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                group.alpha = Mathf.Clamp01(t / duration);
                yield return null;
            }
            group.alpha = 1f;
        }

        private static System.Collections.IEnumerator FadeOutCoroutine(CanvasGroup group, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                group.alpha = 1f - Mathf.Clamp01(t / duration);
                yield return null;
            }
            group.alpha = 0f;
        }

        private static System.Collections.IEnumerator SlideInCoroutine(RectTransform item, Vector2 targetPos, float duration)
        {
            float t = 0f;
            Vector2 startPos = item.anchoredPosition;
            while (t < duration)
            {
                t += Time.deltaTime;
                item.anchoredPosition = Vector2.Lerp(startPos, targetPos, t / duration);
                yield return null;
            }
            item.anchoredPosition = targetPos;
        }
    }

    /// <summary>
    /// 按钮悬停缩放组件。
    /// </summary>
    public class ButtonHoverScale : MonoBehaviour
    {
        public float hoverScale = 1.1f;
        private Vector3 _originalScale;
        private Button _button;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _originalScale = transform.localScale;
        }

        private void OnMouseEnter()
        {
            if (_button != null && _button.interactable)
            {
                transform.localScale = _originalScale * hoverScale;
            }
        }

        private void OnMouseExit()
        {
            transform.localScale = _originalScale;
        }
    }

    /// <summary>
    /// MonoBehaviour扩展方法。
    /// </summary>
    public static class MonoBehaviourExtensions
    {
        public static void MonoStartCoroutine(this GameObject go, System.Collections.IEnumerator coroutine)
        {
            var runner = go.GetComponent<CoroutineRunner>();
            if (runner == null)
            {
                runner = go.AddComponent<CoroutineRunner>();
            }
            runner.StartCoroutine(coroutine);
        }
    }

    public class CoroutineRunner : MonoBehaviour { }
}
