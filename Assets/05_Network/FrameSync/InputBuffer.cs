using System.Collections.Generic;

namespace StarPixelSurge.Network
{
    /// <summary>
    /// 输入缓冲：按玩家与 Frame_Index 存放上报输入，房主按逻辑帧消费。
    /// 使用嵌套字典避免每帧产生大量分配，并定期裁剪过期帧。
    /// </summary>
    public class InputBuffer
    {
        private readonly Dictionary<int, Dictionary<int, PlayerInputPacket>> _byPlayer =
            new Dictionary<int, Dictionary<int, PlayerInputPacket>>();

        private readonly List<PlayerInputPacket> _scratch = new List<PlayerInputPacket>();

        /// <summary>缓冲保留的最近帧数，超出窗口的旧帧在裁剪时丢弃。</summary>
        public int RetainFrames { get; set; } = 120;

        /// <summary>提交一帧输入。同帧重复提交以最后一次为准。</summary>
        public void Submit(PlayerInputPacket packet)
        {
            if (!_byPlayer.TryGetValue(packet.PlayerId, out var frames))
            {
                frames = new Dictionary<int, PlayerInputPacket>();
                _byPlayer[packet.PlayerId] = frames;
            }

            frames[packet.FrameIndex] = packet;
        }

        /// <summary>读取指定玩家在指定帧的输入。</summary>
        public bool TryGet(int playerId, int frameIndex, out PlayerInputPacket packet)
        {
            if (_byPlayer.TryGetValue(playerId, out var frames) && frames.TryGetValue(frameIndex, out packet))
            {
                return true;
            }

            packet = default;
            return false;
        }

        /// <summary>
        /// 收集指定帧所有已提交的输入。缺失输入的玩家不在结果中，
        /// 由调用方决定是否补空输入。
        /// </summary>
        public IReadOnlyList<PlayerInputPacket> CollectForFrame(int frameIndex)
        {
            _scratch.Clear();
            foreach (var frames in _byPlayer.Values)
            {
                if (frames.TryGetValue(frameIndex, out var packet))
                {
                    _scratch.Add(packet);
                }
            }

            return _scratch;
        }

        /// <summary>
        /// 收集每位玩家不晚于指定帧的最近一次输入。相比精确帧匹配，
        /// 该方式对丢包与乱序更健壮：输入晚到时仍会在随后的逻辑帧被消费。
        /// </summary>
        public IReadOnlyList<PlayerInputPacket> CollectLatest(int frameIndex)
        {
            _scratch.Clear();
            foreach (var frames in _byPlayer.Values)
            {
                var found = false;
                var bestFrame = int.MinValue;
                var best = default(PlayerInputPacket);
                foreach (var pair in frames)
                {
                    if (pair.Key <= frameIndex && pair.Key > bestFrame)
                    {
                        bestFrame = pair.Key;
                        best = pair.Value;
                        found = true;
                    }
                }

                if (found)
                {
                    _scratch.Add(best);
                }
            }

            return _scratch;
        }

        /// <summary>裁剪早于指定帧窗口的旧帧，控制内存占用。</summary>
        public void PruneBefore(int frameIndex)
        {
            var cutoff = frameIndex - RetainFrames;
            if (cutoff <= 0)
            {
                return;
            }

            foreach (var frames in _byPlayer.Values)
            {
                var stale = new List<int>();
                foreach (var frame in frames.Keys)
                {
                    if (frame < cutoff)
                    {
                        stale.Add(frame);
                    }
                }

                for (var i = 0; i < stale.Count; i++)
                {
                    frames.Remove(stale[i]);
                }
            }
        }

        /// <summary>移除指定玩家（离开房间时调用）。</summary>
        public void RemovePlayer(int playerId)
        {
            _byPlayer.Remove(playerId);
        }

        public void Clear()
        {
            _byPlayer.Clear();
            _scratch.Clear();
        }
    }
}
