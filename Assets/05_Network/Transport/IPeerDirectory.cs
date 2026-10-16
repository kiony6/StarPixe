using System;

namespace StarPixelSurge.Network
{
    /// <summary>
    /// 具备真实网络端点概念的传输层扩展能力。进程内回环传输无需实现本接口，
    /// 上层通过 <c>_transport as IPeerDirectory</c> 探测后按需使用，缺失时全部降级为无端点行为。
    ///
    /// 该能力解决房主迁移后的寻址问题：新房主是原客户端，它的套接字此前只认识旧房主，
    /// 无从得知其余成员的地址。通过名册下发端点 + 观测来包端点，任一成员都能在迁移后
    /// 立即重建全量寻址表。
    /// </summary>
    public interface IPeerDirectory
    {
        /// <summary>观测到某成员的来源端点（每收到一个数据报即触发），用于刷新房主端点。</summary>
        event Action<int, string, int> PeerObserved;

        /// <summary>登记/更新某成员的端点，供广播寻址。</summary>
        void RegisterPeer(int playerId, string address, int port);

        /// <summary>查询某成员的端点。</summary>
        bool TryGetPeer(int playerId, out string address, out int port);

        /// <summary>重定向 <c>SendToHost</c> 的目标端点（房主迁移后客户端使用）。</summary>
        bool TrySetHost(string address, int port);
    }
}
