using UnityEngine;

namespace StarPixelSurge.Network
{
    /// <summary>
    /// 玩家位移的纯函数实现。房主权威分身与离线仿真共用同一段积分逻辑，
    /// 确保仿真的收敛结论对真实运行代码成立。
    /// </summary>
    public static class AvatarMotion
    {
        /// <summary>把输入限制在单位圆内，避免斜向移动获得额外速度。</summary>
        public static Vector2 ClampInput(Vector2 input)
        {
            return input.sqrMagnitude > 1f ? input.normalized : input;
        }

        /// <summary>按速度与时间步长积分下一位置。</summary>
        public static Vector2 Integrate(Vector2 position, Vector2 input, float speed, float deltaTime)
        {
            var step = ClampInput(input) * speed * Mathf.Max(0f, deltaTime);
            return position + step;
        }
    }
}
