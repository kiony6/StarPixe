using System.Collections.Generic;
using UnityEngine;

namespace StarPixelSurge.Network
{
    /// <summary>快照入缓存的三种结果。</summary>
    public enum SnapshotApply
    {
        /// <summary>重复或过期快照，已忽略。</summary>
        Duplicate = 0,

        /// <summary>已缓存但未推进最新帧（乱序到达的旧帧）。</summary>
        Buffered = 1,

        /// <summary>已缓存且推进了最新帧。</summary>
        Advanced = 2
    }

    /// <summary>
    /// 快照插值缓冲的纯实现。房主权威快照到达后被缓存，消费端按渲染时间轴
    /// 在两帧之间插值，得到平滑的远端实体状态。帧同步服务与离线仿真共用该类，
    /// 使仿真对客户端插值行为的验证等同于验证真实运行代码。
    /// </summary>
    public class SnapshotInterpolator
    {
        private readonly SortedDictionary<int, StateSnapshot> _snapshots =
            new SortedDictionary<int, StateSnapshot>();

        private readonly List<EntityState> _interpolated = new List<EntityState>();

        private StateSnapshot _renderFrom;
        private StateSnapshot _renderTo;
        private float _renderFrame = -1f;
        private float _renderT;
        private bool _hasRenderPair;

        public SnapshotInterpolator(int snapshotRetain = 32, int interpolationDelayFrames = 2)
        {
            SnapshotRetain = snapshotRetain;
            InterpolationDelayFrames = interpolationDelayFrames;
        }

        /// <summary>缓冲保留的最近快照数。</summary>
        public int SnapshotRetain { get; set; }

        /// <summary>渲染时间轴相对最新确认快照的回退帧数。</summary>
        public int InterpolationDelayFrames { get; set; }

        /// <summary>最新确认快照帧号，无快照时为 -1。</summary>
        public int LatestFrame { get; private set; } = -1;

        /// <summary>当前渲染时间轴（帧，含小数）。用于离线仿真核对插值位置。</summary>
        public float RenderFrame => _renderFrame;

        public int Count => _snapshots.Count;

        /// <summary>读取指定帧的原始缓冲快照（离线仿真校验用）。</summary>
        public bool TryGetBuffered(int frame, out StateSnapshot snapshot)
        {
            return _snapshots.TryGetValue(frame, out snapshot);
        }

        /// <summary>当前插值后的实体状态列表。</summary>
        public IReadOnlyList<EntityState> Entities => _interpolated;

        public void Clear()
        {
            _snapshots.Clear();
            _interpolated.Clear();
            _renderFrame = -1f;
            _hasRenderPair = false;
            LatestFrame = -1;
        }

        /// <summary>收到快照时调用。重复快照被忽略，乱序旧帧仅缓存。</summary>
        public SnapshotApply Apply(StateSnapshot snapshot)
        {
            if (snapshot.FrameIndex <= LatestFrame && _snapshots.ContainsKey(snapshot.FrameIndex))
            {
                return SnapshotApply.Duplicate;
            }

            _snapshots[snapshot.FrameIndex] = snapshot;

            if (snapshot.FrameIndex > LatestFrame)
            {
                LatestFrame = snapshot.FrameIndex;
                Prune();
                return SnapshotApply.Advanced;
            }

            return SnapshotApply.Buffered;
        }

        /// <summary>推进渲染时间轴并重建插值结果。</summary>
        public void Advance(float deltaTime, int tickRate)
        {
            if (_snapshots.Count == 0)
            {
                return;
            }

            if (_renderFrame < 0f)
            {
                _renderFrame = LatestFrame - InterpolationDelayFrames;
            }

            _renderFrame += deltaTime * tickRate;

            var minFrame = FirstFrame();
            var maxFrame = LatestFrame;
            if (_renderFrame < minFrame)
            {
                _renderFrame = minFrame;
            }

            var target = Mathf.FloorToInt(_renderFrame);
            if (target > maxFrame)
            {
                target = maxFrame;
            }

            if (!_snapshots.TryGetValue(target, out var from))
            {
                from = ClosestAtOrBefore(target, out _);
            }

            if (!_snapshots.TryGetValue(target + 1, out var to))
            {
                to = ClosestAfter(target, out var found);
                if (!found)
                {
                    to = from;
                }
            }

            _renderFrom = from;
            _renderTo = to;

            var span = Mathf.Max(1, to.FrameIndex - from.FrameIndex);
            _renderT = Mathf.Clamp01((_renderFrame - from.FrameIndex) / span);
            _hasRenderPair = true;

            Rebuild();
        }

        /// <summary>取指定实体的插值状态。</summary>
        public bool TryGet(int entityId, out EntityState state)
        {
            for (var i = 0; i < _interpolated.Count; i++)
            {
                if (_interpolated[i].EntityId == entityId)
                {
                    state = _interpolated[i];
                    return true;
                }
            }

            state = default;
            return false;
        }

        private void Rebuild()
        {
            _interpolated.Clear();
            if (!_hasRenderPair)
            {
                return;
            }

            var entities = StateSnapshot.Interpolate(_renderFrom, _renderTo, _renderT);
            for (var i = 0; i < entities.Length; i++)
            {
                _interpolated.Add(entities[i]);
            }
        }

        private int FirstFrame()
        {
            foreach (var frame in _snapshots.Keys)
            {
                return frame;
            }

            return LatestFrame;
        }

        private StateSnapshot ClosestAtOrBefore(int frame, out bool found)
        {
            StateSnapshot best = default;
            found = false;
            foreach (var pair in _snapshots)
            {
                if (pair.Key <= frame)
                {
                    best = pair.Value;
                    found = true;
                }
                else
                {
                    break;
                }
            }

            if (!found && _snapshots.Count > 0)
            {
                best = _renderFrom;
            }

            return best;
        }

        private StateSnapshot ClosestAfter(int frame, out bool found)
        {
            StateSnapshot best = default;
            found = false;
            foreach (var pair in _snapshots)
            {
                if (pair.Key > frame)
                {
                    best = pair.Value;
                    found = true;
                    break;
                }
            }

            return best;
        }

        private void Prune()
        {
            var cutoff = LatestFrame - Mathf.Max(2, SnapshotRetain);
            var stale = new List<int>();
            foreach (var frame in _snapshots.Keys)
            {
                if (frame < cutoff)
                {
                    stale.Add(frame);
                }
                else
                {
                    break;
                }
            }

            for (var i = 0; i < stale.Count; i++)
            {
                _snapshots.Remove(stale[i]);
            }
        }
    }
}
