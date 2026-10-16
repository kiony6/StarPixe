using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace StarPixelSurge.UI
{
    /// <summary>
    /// 运行时构建 uGUI 元素的工厂。用于在无预制体的情况下搭建原型界面。
    /// </summary>
    internal static class UiFactory
    {
        private static Font _font;
        private static bool _fontResolved;

        public static Font DefaultFont
        {
            get
            {
                if (!_fontResolved)
                {
                    _fontResolved = true;
                    _font = LoadFont();
                }

                return _font;
            }
        }

        public static Canvas CreateCanvas(string name, int sortOrder)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<EventSystem>() != null)
            {
                return;
            }

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        public static RectTransform CreateRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            return rect;
        }

        public static Image CreateImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;

            var image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        public static Text CreateText(Transform parent, string name, string content, int fontSize, TextAnchor anchor, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;

            var text = go.GetComponent<Text>();
            text.font = DefaultFont;
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        public static Image CreateBar(Transform parent, string name, Color trackColor, Color fillColor, out Text label, string initialLabel = "")
        {
            var track = CreateImage(parent, name, trackColor);
            var fill = CreateImage(track.transform, "Fill", fillColor);
            Stretch(fill.rectTransform);

            label = CreateText(track.transform, "Label", initialLabel, 20, TextAnchor.MiddleCenter, Color.white);
            Stretch(label.rectTransform);

            return fill;
        }

        public static Button CreateButton(Transform parent, string name, string label, Color background, UnityAction onClick)
        {
            var image = CreateImage(parent, name, background);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }

            var text = CreateText(image.transform, "Label", label, 24, TextAnchor.MiddleCenter, Color.white);
            Stretch(text.rectTransform);
            return button;
        }

        public static InputField CreateInputField(Transform parent, string name, string placeholder, Color background)
        {
            var image = CreateImage(parent, name, background);
            var field = image.gameObject.AddComponent<InputField>();

            var text = CreateText(image.transform, "Text", string.Empty, 26, TextAnchor.MiddleLeft, Color.white);
            Stretch(text.rectTransform, 16f);
            text.supportRichText = false;

            var hint = CreateText(image.transform, "Placeholder", placeholder, 26, TextAnchor.MiddleLeft, new Color(0.6f, 0.62f, 0.7f));
            Stretch(hint.rectTransform, 16f);

            field.textComponent = text;
            field.placeholder = hint;
            field.lineType = InputField.LineType.SingleLine;
            field.targetGraphic = image;
            return field;
        }

        public static Slider CreateSlider(Transform parent, string name, Color fillColor)
        {
            var bg = CreateImage(parent, name, new Color(0.15f, 0.18f, 0.25f));
            var slider = bg.gameObject.AddComponent<Slider>();

            var fillArea = CreateImage(bg.transform, "Fill Area", new Color(0f, 0f, 0f, 0f));
            Stretch(fillArea.rectTransform, 4f);

            var fill = CreateImage(fillArea.transform, "Fill", fillColor);
            Stretch(fill.rectTransform);

            slider.fillRect = fill.rectTransform;
            slider.targetGraphic = fill;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;

            return slider;
        }

        public static void Stretch(RectTransform rect, float padding = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
        }

        /// <summary>为图形添加发光描边，营造霓虹轮廓。</summary>
        public static Outline AddOutline(Graphic graphic, Color color, float distance = 3f)
        {
            var outline = graphic.gameObject.AddComponent<Outline>();
            outline.effectColor = Framework.NeonPalette.WithAlpha(color, 0.85f);
            outline.effectDistance = new Vector2(distance, -distance);
            outline.useGraphicAlpha = false;
            return outline;
        }

        /// <summary>为图形添加外发光阴影，营造霓虹光晕。</summary>
        public static Shadow AddGlow(Graphic graphic, Color color, float distance = 4f, bool useGraphicAlpha = false)
        {
            var glow = graphic.gameObject.AddComponent<Shadow>();
            glow.effectColor = color;
            glow.effectDistance = new Vector2(distance, -distance);
            glow.useGraphicAlpha = useGraphicAlpha;
            return glow;
        }

        /// <summary>创建带霓虹描边的面板。</summary>
        public static Image CreateNeonPanel(Transform parent, string name, Color fill, Color border, float borderWidth = 3f)
        {
            var panel = CreateImage(parent, name, fill);
            AddOutline(panel, border, borderWidth);
            return panel;
        }

        /// <summary>创建带发光效果的文本。</summary>
        public static Text CreateNeonText(Transform parent, string name, string content, int fontSize, TextAnchor anchor, Color color, float glow = 3f)
        {
            var text = CreateText(parent, name, content, fontSize, anchor, color);
            AddGlow(text, Framework.NeonPalette.WithAlpha(color, 0.55f), glow);
            return text;
        }

        public static void SetAnchoredRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private static Font LoadFont()
        {
            try
            {
                var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (font != null)
                {
                    return font;
                }
            }
            catch
            {
                // ignored
            }

            try
            {
                return Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            catch
            {
                return null;
            }
        }
    }
}
