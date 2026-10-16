using System.Collections.Generic;
using StarPixelSurge.Framework;
using UnityEngine;

namespace StarPixelSurge.Network
{
    /// <summary>
    /// 联机队伍机制执行器（房主权威）。订阅逻辑帧推进事件，按帧计算队友复活进度与
    /// 跨玩家光环增益：附近有存活队友的倒地玩家累积复活进度，进度满则以部分生命救起；
    /// 存活队友彼此靠近时获得基于范围内同伴有数量的临时伤害/移速/暴击加成。
    /// 计算逻辑复用纯实现 <see cref="ReviveTracker"/> 与 <see cref="TeamAura"/>，
    /// 与离线仿真保持一致。
    /// </summary>
    public class NetworkTeamDirector : MonoBehaviour
    {
        [SerializeField] private float reviveRadius = 2.5f;
        [SerializeField] private float reviveDuration = 3f;
        [SerializeField] private float reviveHealthFraction = 0.5f;
        [SerializeField] private float auraRadius = 4f;
        [SerializeField] private float auraDamagePerAlly = 0.08f;
        [SerializeField] private float auraMoveSpeedPerAlly = 0.4f;
        [SerializeField] private float auraCritPerAlly = 0.05f;

        private readonly ReviveTracker _revive = new ReviveTracker();
        private readonly List<TeamPlayerState> _players = new List<TeamPlayerState>();
        private readonly List<int> _revived = new List<int>();

        private FrameSyncService _frame;
        private NetworkPlayerRegistry _registry;
        private float _tickDelta = 1f / 30f;
        private bool _bound;
        private bool _wipeReported;

        public ReviveTracker Revive => _revive;

        public void Configure(FrameSyncService frame, NetworkPlayerRegistry registry)
        {
            Unbind();
            _frame = frame;
            _registry = registry;
            _tickDelta = 1f / Mathf.Max(1, frame != null ? frame.TickRate : 30);
            _revive.ReviveRadius = reviveRadius;
            _revive.ReviveDuration = reviveDuration;
            _revive.ReviveHealthFraction = reviveHealthFraction;
            _wipeReported = false;
            Bind();
        }

        public void Shutdown()
        {
            Unbind();
            _frame = null;
            _registry = null;
            _revive.Clear();
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
            if (_frame == null || _registry == null || _bound)
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
            Tick(_tickDelta);
        }

        /// <summary>按当前名册推进一帧队伍机制。</summary>
        public void Tick(float deltaTime)
        {
            if (_registry == null || _registry.Count == 0)
            {
                return;
            }

            _players.Clear();
            var avatars = _registry.Avatars;
            for (var i = 0; i < avatars.Count; i++)
            {
                var avatar = avatars[i];
                if (avatar == null || avatar.Stats == null)
                {
                    continue;
                }

                _players.Add(new TeamPlayerState(avatar.PlayerId, avatar.transform.position, !avatar.Stats.IsDead));
            }

            if (_players.Count == 0)
            {
                return;
            }

            _revive.Tick(_players, deltaTime, _revived);
            for (var i = 0; i < _revived.Count; i++)
            {
                var avatar = _registry.Get(_revived[i]);
                if (avatar != null && avatar.Stats != null)
                {
                    avatar.Stats.Revive(avatar.Stats.MaxHealth * _revive.ReviveHealthFraction);
                }
            }

            for (var i = 0; i < _players.Count; i++)
            {
                var state = _players[i];
                var avatar = _registry.Get(state.PlayerId);
                if (avatar == null || avatar.Stats == null || avatar.Stats.IsDead)
                {
                    continue;
                }

                var allies = TeamAura.NearbyAllyCount(_players, state.PlayerId, auraRadius);
                avatar.Stats.SetTeamBonus(
                    allies * auraDamagePerAlly,
                    allies * auraMoveSpeedPerAlly,
                    allies * auraCritPerAlly);
            }

            CheckTeamWipe();
        }

        /// <summary>全员倒地时结束对局（联机模式所有玩家均可复活，故以团灭为失败条件）。</summary>
        private void CheckTeamWipe()
        {
            for (var i = 0; i < _players.Count; i++)
            {
                if (_players[i].Alive)
                {
                    _wipeReported = false;
                    return;
                }
            }

            if (_wipeReported || !GameManager.HasInstance)
            {
                return;
            }

            _wipeReported = true;
            GameManager.Instance.CompleteMatch();
        }
    }
}
