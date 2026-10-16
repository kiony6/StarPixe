using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace StarPixelSurge.UI
{
    /// <summary>
    /// 词缀拾取提示：在屏幕顶部显示拾取的词缀，带动画效果。
    /// </summary>
    public class AffixToastView : MonoBehaviour
    {
        private RectTransform _container;
        private VerticalLayoutGroup _layout;
        private const float ToastDuration = 3f;
        private const int MaxToasts = 5;
        private readonly List<GameObject> _activeToasts = new List<GameObject>();

        public void Build(Transform parent)
        {
            // 顶部容器
            var go = new GameObject("AffixToastContainer");
            go.transform.SetParent(parent, false);
            _container = go.AddComponent<RectTransform>();
            UiFactory.SetAnchoredRect(_container, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(400f, 300f));

            _layout = go.AddComponent<VerticalLayoutGroup>();
            _layout.spacing = 8f;
            _layout.childAlignment = TextAnchor.UpperCenter;
            _layout.childControlHeight = true;
            _layout.childControlWidth = true;
        }

        /// <summary>显示词缀拾取提示。</summary>
        public void ShowAffixToast(string affixName, string description, Color color)
        {
            var toast = CreateToast(affixName, description, color);
            _activeToasts.Add(toast);

            // 限制最多显示数量
            while (_activeToasts.Count > MaxToasts)
            {
                var old = _activeToasts[0];
                _activeToasts.RemoveAt(0);
                Destroy(old);
            }

            // 自动消失
            Destroy(toast, ToastDuration);
        }

        private GameObject CreateToast(string name, string description, Color color)
        {
            var go = new GameObject("Toast_" + name);
            go.transform.SetParent(_container, false);

            // 背景
            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.12f, 0.2f, 0.9f);

            // 边框
            var outline = go.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(2f, 2f);

            // 词缀名称
            var nameText = UiFactory.CreateText(go.transform, "Name", name, 22, TextAnchor.MiddleCenter, color);
            UiFactory.Stretch(nameText.rectTransform, 10f);

            // 描述（小字）
            var descText = UiFactory.CreateText(go.transform, "Desc", description, 14, TextAnchor.LowerCenter, new Color(0.8f, 0.85f, 0.95f));
            UiFactory.SetAnchoredRect(descText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(380f, 20f));

            // 设置大小
            var rect = go.GetComponent<RectTransform>();
            UiFactory.SetAnchoredRect(rect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(380f, 50f));

            // 动画效果：淡入
            var canvasGroup = go.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            // 简单的淡入效果（不用LeanTween）
            StartCoroutine(FadeIn(canvasGroup, 0.3f));

            return go;
        }

        private System.Collections.IEnumerator FadeIn(CanvasGroup group, float duration)
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
    }
}
