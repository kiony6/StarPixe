using System.Collections.Generic;
using UnityEngine;

namespace StarPixelSurge.Network
{
    /// <summary>
    /// 房主输入回放器。订阅帧同步服务的逻辑帧推进事件，
    /// 按帧把各玩家上报的输入应用到对应权威分身，再统一推进分身位置，
    /// 使快照广播的实体状态与全部客户端输入一致。
    /// </summary>
    public class NetworkInputReplay : MonoBehaviour
    {
        private FrameSyncService _frame;
        private NetworkPlayerRegistry _registry;
        private float _tickDelta = 1f / 30f;
        private bool _bound;

        public void Configure(FrameSyncService frame, NetworkPlayerRegistry registry)
        {
            Unbind();
            _frame = frame;
            _registry = registry;
            _tickDelta = 1f / Mathf.Max(1, frame != null ? frame.TickRate : 30);
            Bind();
        }

        public void Shutdown()
        {
            Unbind();
            _frame = null;
            _registry = null;
        }

        private void OnEnable()
        {
            Bind();
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void Bind()
        {
            if (_frame == null || _bound)
            {
                return;
            }

            _frame.FrameAdvanced += OnFrameAdvanced;
            _bound = true;
        }

        private void Unbind()
        {
            if (_frame != null && _bound)
            {
                _frame.FrameAdvanced -= OnFrameAdvanced;
            }

            _bound = false;
        }

        private void OnFrameAdvanced(int frameIndex, IReadOnlyList<PlayerInputPacket> inputs)
        {
            if (_registry == null)
            {
                return;
            }

            // 应用本帧收到的输入；未提交输入的玩家沿用上一次输入，缓解丢包导致的抖动。
            if (inputs != null)
            {
                for (var i = 0; i < inputs.Count; i++)
                {
                    var input = inputs[i];
                    var avatar = _registry.Get(input.PlayerId);
                    if (avatar != null)
                    {
                        avatar.ApplyInput(input.MoveDir);
                    }
                }
            }

            _registry.Tick(_tickDelta);
        }
    }
}
