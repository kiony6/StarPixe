using System;

namespace StarPixelSurge.Network
{
    /// <summary>
    /// 传输层抽象。屏蔽底层实现（本地回环、UDP 插件等），
    /// 上层仅依赖该接口收发不透明的字节载荷。
    /// </summary>
    public interface ITransport
    {
        int LocalPlayerId { get; }

        bool IsRunning { get; }

        /// <summary>当前房主的玩家 ID。客户端据此决定上报目标。</summary>
        int HostId { get; set; }

        /// <summary>收到来自 <c>senderId</c> 的载荷。</summary>
        event Action<int, byte[]> MessageReceived;

        void Start(int localPlayerId);

        void Stop();

        /// <summary>发送给房主（房主自身调用时为空操作）。</summary>
        void SendToHost(byte[] payload);

        /// <summary>广播给除自己以外的所有成员。</summary>
        void Broadcast(byte[] payload);

        /// <summary>由上层驱动的时间推进，用于投递延迟消息。</summary>
        void Pump(float deltaTime);
    }
}
