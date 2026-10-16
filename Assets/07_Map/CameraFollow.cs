using StarPixelSurge.Effect;
using StarPixelSurge.GameCore;
using UnityEngine;

namespace StarPixelSurge.Map
{
    /// <summary>
    /// 相机跟随玩家。未指定 target 时自动查找玩家，
    /// 并在平滑跟随结果上叠加 <see cref="CameraFeedback"/> 的震屏位移。
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        public Transform Target;
        public float Smooth = 8f;
        public float ZOffset = -10f;

        private Vector2 _basePosition;
        private bool _hasBase;

        private CameraFeedback _feedback;

        private void Awake()
        {
            _feedback = GetComponent<CameraFeedback>();
        }

        private void LateUpdate()
        {
            if (Target == null)
            {
                if (PlayerController.Instance != null)
                {
                    Target = PlayerController.Instance.transform;
                }
                else
                {
                    return;
                }
            }

            if (!_hasBase)
            {
                _basePosition = transform.position;
                _hasBase = true;
            }

            var destination = (Vector2)Target.position;
            var t = 1f - Mathf.Exp(-Smooth * Time.unscaledDeltaTime);
            _basePosition = Vector2.Lerp(_basePosition, destination, t);

            var offset = _feedback != null ? _feedback.Offset : Vector2.zero;
            transform.position = new Vector3(
                _basePosition.x + offset.x,
                _basePosition.y + offset.y,
                ZOffset);
        }
    }
}
