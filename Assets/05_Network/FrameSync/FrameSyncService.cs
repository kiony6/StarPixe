using System;
using System.Collections.Generic;
using StarPixelSurge.Framework;
using UnityEngine;

namespace StarPixelSurge.Network
{
    /// <summary>联机角色。</summary>
    public enum NetworkRole
    {
        None = 0,
        Host = 1,
        Client = 2
    }

    /// <summary>
    /// 帧同步服务。房主以固定逻辑帧率推进时间轴、消费输入并广播权威快照；
    /// 客户端上报本地输入，对快照做抖动缓冲与插值渲染。
    /// </summary>
    public class FrameSyncService : MonoSingleton<FrameSyncService>
    {
        [SerializeField] private int tickRate = 30;
        [SerializeField] private int snapshotRetain = 32;
        [SerializeField] private int interpolationDelayFrames = 2;
        [SerializeField] private bool gateOnGameState = true;

        private ITransport _transport;
        private float _tickAccumulator;
        private float _renderFrame = -1f;

        private readonly SortedDictionary<int, StateSnapshot> _snapshots = new SortedDictionary<int, StateSnapshot>();
        private readonly List<EntityState> _interpolated = new List<EntityState>();

        private StateSnapshot _renderFrom;
        private StateSnapshot _renderTo;
        private float _renderT;
        private bool _hasRenderPair;

        public NetworkRole Role { get; private set; }

        public int CurrentFrameIndex { get; private set; }

        public int LatestSnapshotFrame { get; private set; } = -1;

        public uint RngState { get; set; }

        public InputBuffer Inputs { get; } = new InputBuffer();

        public int TickRate => tickRate;

        /// <summary>房主提供当前权威状态快照的回调。</summary>
        public Func<StateSnapshot> SnapshotProvider { get; set; }

        /// <summary>房主每推进一帧触发，携带该帧消费到的输入。</summary>
        public event Action<int, IReadOnlyList<PlayerInputPacket>> FrameAdvanced;

        /// <summary>客户端应用快照后触发。</summary>
        public event Action<StateSnapshot> SnapshotApplied;

        /// <summary>客户端最新确认帧变化时触发（单调递增）。</summary>
        public event Action<int> LatestFrameChanged;

        /// <summary>客户端收到任意快照时触发，供房主迁移服务缓存最后确认状态。</summary>
        public event Action<StateSnapshot> SnapshotReceived;

        public void Configure(ITransport transport, NetworkRole role, int startFrame = 0)
        {
            _transport = transport;
            Role = role;
            CurrentFrameIndex = startFrame;
            LatestSnapshotFrame = -1;
            _renderFrame = -1f;
            _tickAccumulator = 0f;
            _hasRenderPair = false;
            _snapshots.Clear();
            _interpolated.Clear();
            Inputs.Clear();
        }

        public void Shutdown()
        {
            Role = NetworkRole.None;
            _transport = null;
            _snapshots.Clear();
            _interpolated.Clear();
            Inputs.Clear();
        }

        /// <summary>客户端上报本地输入到房主。</summary>
        public void SubmitInput(PlayerInputPacket input)
        {
            if (Role != NetworkRole.Client || _transport == null)
            {
                return;
            }

            _transport.SendToHost(NetworkSerializer.SerializeInput(input));
        }

        /// <summary>客户端便捷上报：以最新确认帧的下一位作为输入帧序号。</summary>
        public void SubmitLocalInput(Vector2 moveDir, int skillSlot = -1, bool cast = false)
        {
            if (Role != NetworkRole.Client || _transport == null)
            {
                return;
            }

            var frame = LatestSnapshotFrame >= 0 ? LatestSnapshotFrame + 1 : 0;
            SubmitInput(new PlayerInputPacket(_transport.LocalPlayerId, frame, moveDir, skillSlot, cast));
        }

        /// <summary>房主接收并缓存客户端输入。</summary>
        public void OnInputReceived(byte[] payload)
        {
            if (Role != NetworkRole.Host)
            {
                return;
            }

            if (NetworkSerializer.TryDeserializeInput(payload, out var input))
            {
                Inputs.Submit(input);
            }
        }

        /// <summary>房主广播权威快照。</summary>
        public void BroadcastSnapshot(StateSnapshot snapshot)
        {
            if (_transport == null)
            {
                return;
            }

            _transport.Broadcast(NetworkSerializer.SerializeSnapshot(snapshot));
        }

        /// <summary>客户端接收并缓存快照，忽略过期帧以保持时间轴单调。</summary>
        public void OnSnapshotReceived(byte[] payload)
        {
            if (Role != NetworkRole.Client)
            {
                return;
            }

            if (!NetworkSerializer.TryDeserializeSnapshot(payload, out var snapshot))
            {
                return;
            }

            ApplySnapshot(snapshot);
        }

        public void ApplySnapshot(StateSnapshot snapshot)
        {
            if (snapshot.FrameIndex <= LatestSnapshotFrame && _snapshots.ContainsKey(snapshot.FrameIndex))
            {
                return;
            }

            _snapshots[snapshot.FrameIndex] = snapshot;
            SnapshotReceived?.Invoke(snapshot);

            if (snapshot.FrameIndex > LatestSnapshotFrame)
            {
                LatestSnapshotFrame = snapshot.FrameIndex;
                LatestFrameChanged?.Invoke(LatestSnapshotFrame);
                PruneSnapshots();
            }

            SnapshotApplied?.Invoke(snapshot);
        }

        /// <summary>取当前插值后的实体状态列表（客户端渲染用）。</summary>
        public IReadOnlyList<EntityState> GetInterpolatedEntities()
        {
            return _interpolated;
        }

        public bool TryGetInterpolated(int entityId, out EntityState state)
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

        private void Update()
        {
            if (_transport == null || Role == NetworkRole.None)
            {
                return;
            }

            if (gateOnGameState && GameManager.HasInstance && GameManager.Instance.State != GameState.Playing)
            {
                return;
            }

            if (Role == NetworkRole.Host)
            {
                if (SnapshotProvider != null)
                {
                    TickHost(Time.deltaTime);
                }
            }
            else
            {
                TickClient(Time.deltaTime);
            }
        }

        private void TickHost(float deltaTime)
        {
            var interval = 1f / Mathf.Max(1, tickRate);
            _tickAccumulator += deltaTime;

            // 限制单帧追帧数量，避免长时间卡顿后一次性推进过多逻辑帧。
            var maxCatchUp = 5;
            while (_tickAccumulator >= interval && maxCatchUp-- > 0)
            {
                _tickAccumulator -= interval;
                CurrentFrameIndex++;
                RngState++;

                var inputs = Inputs.CollectForFrame(CurrentFrameIndex);
                FrameAdvanced?.Invoke(CurrentFrameIndex, inputs);

                var snapshot = SnapshotProvider.Invoke();
                snapshot.FrameIndex = CurrentFrameIndex;
                BroadcastSnapshot(snapshot);

                Inputs.PruneBefore(CurrentFrameIndex);
            }
        }

        private void TickClient(float deltaTime)
        {
            if (_snapshots.Count == 0)
            {
                return;
            }

            if (_renderFrame < 0f)
            {
                _renderFrame = LatestSnapshotFrame - interpolationDelayFrames;
            }

            _renderFrame += deltaTime * tickRate;

            var minFrame = FirstFrame();
            var maxFrame = LatestSnapshotFrame;
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
                from = GetClosestAtOrBefore(target, out _);
            }

            StateSnapshot to;
            if (!_snapshots.TryGetValue(target + 1, out to))
            {
                to = GetClosestAfter(target, out var found);
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

            RebuildInterpolated();
        }

        private void RebuildInterpolated()
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

            return LatestSnapshotFrame;
        }

        private StateSnapshot GetClosestAtOrBefore(int frame, out bool found)
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

        private StateSnapshot GetClosestAfter(int frame, out bool found)
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

        private void PruneSnapshots()
        {
            var cutoff = LatestSnapshotFrame - Mathf.Max(2, snapshotRetain);
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
