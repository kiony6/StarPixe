using StarPixelSurge.GameCore;
using UnityEngine;

namespace StarPixelSurge.Map
{
    /// <summary>
    /// 相机跟随玩家。未指定 target 时自动查找玩家。
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        public Transform Target;
        public float Smooth = 8f;
        public float ZOffset = -10f;

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

            var current = (Vector2)transform.position;
            var destination = (Vector2)Target.position;
            var t = 1f - Mathf.Exp(-Smooth * Time.unscaledDeltaTime);
            var next = Vector2.Lerp(current, destination, t);
            transform.position = new Vector3(next.x, next.y, ZOffset);
        }
    }
}
