using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StarPixelSurge.UI
{
    /// <summary>
    /// 按钮高亮效果：悬停时变亮。
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class ButtonHighlight : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Color normalColor = Color.gray;
        public Color highlightColor = Color.white;

        private Image _image;

        private void Awake()
        {
            _image = GetComponent<Image>();
            normalColor = _image.color;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_image != null)
            {
                _image.color = highlightColor;
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_image != null)
            {
                _image.color = normalColor;
            }
        }
    }
}
