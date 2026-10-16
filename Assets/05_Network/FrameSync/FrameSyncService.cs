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
        [SerializeField] private int inputLeadFrames = 2;
        [SerializeField] private bool gateOnGameState = true;

        private ITransport _transport;
        private float _tickAccumulator;
        private readonly SnapshotInterpolator _interpolator = new SnapshotInterpolator();

        public NetworkRole Role { get; private set; }

        public int CurrentFrameIndex { get; private set; }

        public int LatestSnapshotFrame => _interpolator.LatestFrame;

        public uint RngState { get; set; }

        public InputBuffer Inputs { get; } = new InputBuffer();

        public int TickRate => tickRate;

        /// <summary>输入提前量：本地输入提交到未来多少帧，给网络往返留出缓冲。</summary>
        public int InputLeadFrames => Mathf.Max(1, inputLeadFrames);

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
            _tickAccumulator = 0f;
            _interpolator.SnapshotRetain = snapshotRetain;
            _interpolator.InterpolationDelayFrames = interpolationDelayFrames;
            _interpolator.Clear();
            Inputs.Clear();
        }

        public void Shutdown()
        {
            Role = NetworkRole.None;
            _transport = null;
            _interpolator.Clear();
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

        /// <summary>
        /// 便捷上报本地输入。客户端发送到房主；房主直接把输入写入逻辑帧输入缓冲，
        /// 交由输入回放器在目标帧统一消费，使房主与客户端走同一套确认路径。
        /// </summary>
        public void SubmitLocalInput(Vector2 moveDir, int skillSlot = -1, bool cast = false)
        {
            if (_transport == null)
            {
                return;
            }

            if (Role == NetworkRole.Client)
            {
                var frame = LatestSnapshotFrame >= 0 ? LatestSnapshotFrame + InputLeadFrames : InputLeadFrames;
                _transport.SendToHost(NetworkSerializer.SerializeInput(
                    new PlayerInputPacket(_transport.LocalPlayerId, frame, moveDir, skillSlot, cast)));
                return;
            }

            if (Role == NetworkRole.Host)
            {
                var frame = CurrentFrameIndex + InputLeadFrames;
                Inputs.Submit(new PlayerInputPacket(_transport.LocalPlayerId, frame, moveDir, skillSlot, cast));
            }
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
            var result = _interpolator.Apply(snapshot);
            if (result == SnapshotApply.Duplicate)
            {
                return;
            }

            SnapshotReceived?.Invoke(snapshot);

            if (result == SnapshotApply.Advanced)
            {
                LatestFrameChanged?.Invoke(_interpolator.LatestFrame);
            }

            SnapshotApplied?.Invoke(snapshot);
        }

        /// <summary>取当前插值后的实体状态列表（客户端渲染用）。</summary>
        public IReadOnlyList<EntityState> GetInterpolatedEntities()
        {
            return _interpolator.Entities;
        }

        public bool TryGetInterpolated(int entityId, out EntityState state)
        {
            return _interpolator.TryGet(entityId, out state);
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
                _interpolator.Advance(Time.deltaTime, tickRate);
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

                var inputs = Inputs.CollectLatest(CurrentFrameIndex);
                FrameAdvanced?.Invoke(CurrentFrameIndex, inputs);

                var snapshot = SnapshotProvider.Invoke();
                snapshot.FrameIndex = CurrentFrameIndex;
                BroadcastSnapshot(snapshot);

                Inputs.PruneBefore(CurrentFrameIndex);
            }
        }
    }
}
