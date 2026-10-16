using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace StarPixelSurge.Network.Simulation
{
    /// <summary>单条仿真断言结果。</summary>
    public struct SimCheck
    {
        public string Name;
        public bool Passed;
        public string Detail;

        public SimCheck(string name, bool passed, string detail)
        {
            Name = name;
            Passed = passed;
            Detail = detail ?? string.Empty;
        }
    }

    /// <summary>仿真报告：汇总全部断言与通过情况。</summary>
    public sealed class SimReport
    {
        public readonly List<SimCheck> Checks = new List<SimCheck>();

        public void Add(string name, bool passed, string detail = "")
        {
            Checks.Add(new SimCheck(name, passed, detail));
        }

        public bool AllPassed
        {
            get
            {
                for (var i = 0; i < Checks.Count; i++)
                {
                    if (!Checks[i].Passed)
                    {
                        return false;
                    }
                }

                return Checks.Count > 0;
            }
        }

        public int PassedCount
        {
            get
            {
                var count = 0;
                for (var i = 0; i < Checks.Count; i++)
                {
                    if (Checks[i].Passed)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public string BuildSummary()
        {
            var builder = new StringBuilder();
            builder.Append($"[NetworkSimulation] {PassedCount}/{Checks.Count} checks passed\n");
            for (var i = 0; i < Checks.Count; i++)
            {
                var check = Checks[i];
                builder.Append(check.Passed ? "  PASS  " : "  FAIL  ");
                builder.Append(check.Name);
                if (!string.IsNullOrEmpty(check.Detail))
                {
                    builder.Append(" - ");
                    builder.Append(check.Detail);
                }

                builder.Append('\n');
            }

            return builder.ToString();
        }
    }

    /// <summary>仿真参数。</summary>
    public sealed class NetworkSimulationOptions
    {
        public int PlayerCount = 3;
        public float Latency = 0.05f;
        public float Jitter;
        public float LossRate;
        public ulong Seed = 20261016UL;
        public int TickRate = 30;
        public int RunFrames = 300;
        public int InterpolationDelayFrames = 2;
        public bool KillHostAtEnd = true;
    }

    /// <summary>
    /// 本地多实例确定性仿真编排器。在单进程内以真实的房间、输入缓冲、协议序列化、
    /// 快照插值与房主迁移组件驱动多个节点，验证：名册收敛、房主权威世界、快照保真、
    /// 插值包络、输入送达与房主迁移接管。结果以结构化断言报告返回，供编辑器与 batchmode 使用。
    /// </summary>
    public sealed class NetworkSimulation
    {
        private const string ClassId = "thunder_mage";
        private const float PlayerMoveSpeed = 5f;

        private readonly NetworkSimulationOptions _options;
        private readonly SimNetwork _network;
        private readonly List<SimNode> _nodes = new List<SimNode>();
        private readonly HashSet<int> _dead = new HashSet<int>();

        private SimNode _host;

        public NetworkSimulation(NetworkSimulationOptions options)
        {
            _options = options ?? new NetworkSimulationOptions();
            _network = new SimNetwork(_options.Seed)
            {
                Latency = _options.Latency,
                Jitter = _options.Jitter,
                LossRate = _options.LossRate
            };
        }

        public SimReport Run()
        {
            var report = new SimReport();

            BuildNodes();
            _host.CreateRoom();
            for (var i = 1; i < _nodes.Count; i++)
            {
                _nodes[i].JoinRoom(_host.PlayerId);
            }

            PumpNetwork(0.02f, 10);

            for (var i = 0; i < _nodes.Count; i++)
            {
                _nodes[i].SetReady(true);
            }

            // 准备包会触发房主重发名册，多等几个往返以适应抖动与丢包。
            PumpNetwork(0.02f, 20);
            CheckRosterAgreement(report);

            // 再给一轮大厅同步时间，确保所有准备包在抖动丢包后仍被房主收到。
            PumpNetwork(0.02f, 10);

            var started = _host.TryStartMatch();
            report.Add("HostStartsMatch", started);

            PumpNetwork(0.02f, 20);
            CheckClientsPlaying(report);

            RunFrames();
            PumpNetwork(1f / _options.TickRate, 40);

            CheckHostAuthorityWorld(report);
            CheckSnapshotFidelity(report);
            CheckInterpolationEnvelope(report);
            CheckInputReachedHost(report);
            CheckTeamMechanics(report);

            if (_options.KillHostAtEnd)
            {
                CheckHostMigration(report);
            }

            return report;
        }

        private void BuildNodes()
        {
            _host = new SimNode(_network, 0, NetworkRole.Host, ClassId, _options.TickRate, (int)_options.Seed);
            _nodes.Add(_host);

            for (var i = 1; i < Mathf.Max(2, _options.PlayerCount); i++)
            {
                var client = new SimNode(_network, i, NetworkRole.Client, ClassId, _options.TickRate, (int)_options.Seed);
                client.Interp.InterpolationDelayFrames = _options.InterpolationDelayFrames;
                _nodes.Add(client);
            }
        }

        private void RunFrames()
        {
            var deltaTime = 1f / _options.TickRate;
            for (var frame = 0; frame < _options.RunFrames; frame++)
            {
                for (var i = 0; i < _nodes.Count; i++)
                {
                    var node = _nodes[i];
                    if (_dead.Contains(node.PlayerId))
                    {
                        continue;
                    }

                    node.SubmitLocalInput(DirectionFor(node.PlayerId, frame));
                }

                StepOnce(deltaTime);
            }
        }

        private void PumpNetwork(float deltaTime, int steps)
        {
            for (var i = 0; i < steps; i++)
            {
                StepOnce(deltaTime);
            }
        }

        private void StepOnce(float deltaTime)
        {
            for (var i = 0; i < _nodes.Count; i++)
            {
                var node = _nodes[i];
                if (_dead.Contains(node.PlayerId))
                {
                    continue;
                }

                node.Tick(deltaTime);
            }

            _network.Step(deltaTime);
        }

        /// <summary>脚本化输入：恒定向右偏移的旋转方向，保证净位移显著且可复现。</summary>
        private static Vector2 DirectionFor(int playerId, int frame)
        {
            var angle = frame * 0.15f + playerId * 1.7f;
            var direction = new Vector2(1f, Mathf.Sin(angle));
            return direction.normalized;
        }

        private void CheckRosterAgreement(SimReport report)
        {
            var reference = SortedMembers(_host.Room);
            var passed = true;
            var detail = "";

            for (var i = 1; i < _nodes.Count; i++)
            {
                var members = SortedMembers(_nodes[i].Room);
                if (!SameMembers(reference, members))
                {
                    passed = false;
                    detail = $"node {_nodes[i].PlayerId} 名册不一致";
                    break;
                }
            }

            report.Add("RosterAgreement", passed, detail);
        }

        private void CheckClientsPlaying(SimReport report)
        {
            var passed = true;
            var detail = "";
            for (var i = 0; i < _nodes.Count; i++)
            {
                if (_nodes[i].Role == NetworkRole.Client && !_nodes[i].Playing)
                {
                    passed = false;
                    detail = $"客户端 {_nodes[i].PlayerId} 未进入对局";
                    break;
                }
            }

            report.Add("ClientsEnterPlaying", passed, detail);
        }

        private void CheckHostAuthorityWorld(SimReport report)
        {
            var expected = _host.Room.Count;
            report.Add(
                "HostAuthorityWorld",
                _host.HostWorldCount == expected,
                $"world={_host.HostWorldCount} roster={expected}");
        }

        private void CheckSnapshotFidelity(SimReport report)
        {
            const float epsilon = 1e-4f;
            var passed = true;
            var detail = "";
            var checkedCount = 0;

            for (var i = 0; i < _nodes.Count && passed; i++)
            {
                var client = _nodes[i];
                if (client.Role != NetworkRole.Client)
                {
                    continue;
                }

                var frame = client.Interp.LatestFrame;
                if (frame < 0 || !client.Interp.TryGetBuffered(frame, out var clientSnapshot))
                {
                    passed = false;
                    detail = $"客户端 {client.PlayerId} 无可用快照";
                    break;
                }

                if (!_host.TryGetHistory(frame, out var hostSnapshot))
                {
                    continue;
                }

                for (var e = 0; e < clientSnapshot.Entities.Length; e++)
                {
                    var entity = clientSnapshot.Entities[e];
                    if (!TryFindEntity(hostSnapshot, entity.EntityId, out var hostEntity))
                    {
                        passed = false;
                        detail = $"帧 {frame} 缺少实体 {entity.EntityId}";
                        break;
                    }

                    checkedCount++;
                    if ((entity.Position - hostEntity.Position).sqrMagnitude > epsilon * epsilon)
                    {
                        passed = false;
                        detail = $"帧 {frame} 实体 {entity.EntityId} 位置偏差 "
                                 + $"{Vector2.Distance(entity.Position, hostEntity.Position):F5}";
                        break;
                    }
                }
            }

            report.Add("SnapshotFidelity", passed, passed ? $"比较 {checkedCount} 个实体位置" : detail);
        }

        private void CheckInterpolationEnvelope(SimReport report)
        {
            var delay = Mathf.Max(0, _options.InterpolationDelayFrames);
            var stepLength = PlayerMoveSpeed / Mathf.Max(1, _options.TickRate);

            // 零延迟时插值应精确等于最新快照；有延迟时给出覆盖丢包缺口与插值跨度的运动学上界。
            var tolerance = delay == 0 ? 1e-3f : (delay + 3f) * stepLength;
            var passed = true;
            var detail = "";

            for (var i = 0; i < _nodes.Count && passed; i++)
            {
                var client = _nodes[i];
                if (client.Role != NetworkRole.Client)
                {
                    continue;
                }

                var renderFrame = client.Interp.RenderFrame;
                if (renderFrame < 0f)
                {
                    passed = false;
                    detail = $"客户端 {client.PlayerId} 无渲染帧";
                    break;
                }

                var nearest = Mathf.Clamp(Mathf.FloorToInt(renderFrame), 0, _host.Frame);
                if (!_host.TryGetHistory(nearest, out var hostSnapshot))
                {
                    continue;
                }

                for (var p = 0; p <= 3; p++)
                {
                    if (!client.Interp.TryGet(p, out var state) || !TryFindEntity(hostSnapshot, p, out var hostEntity))
                    {
                        continue;
                    }

                    var distance = Vector2.Distance(state.Position, hostEntity.Position);
                    if (distance > tolerance)
                    {
                        passed = false;
                        detail = $"客户端 {client.PlayerId} 玩家 {p} 插值偏差 {distance:F3} > {tolerance:F3}";
                        break;
                    }
                }
            }

            report.Add("InterpolationEnvelope", passed, passed ? $"容差 {tolerance:F3}" : detail);
        }

        private void CheckInputReachedHost(SimReport report)
        {
            var passed = true;
            var detail = "";

            for (var i = 0; i < _nodes.Count; i++)
            {
                var node = _nodes[i];
                if (!_host.TryGetPlayerPosition(node.PlayerId, out var position))
                {
                    passed = false;
                    detail = $"房主世界缺少玩家 {node.PlayerId}";
                    break;
                }

                if (position.sqrMagnitude < 0.25f)
                {
                    passed = false;
                    detail = $"玩家 {node.PlayerId} 未因输入移动";
                    break;
                }
            }

            report.Add("InputReachedHost", passed, detail);
        }

        /// <summary>验证队友复活与跨玩家光环增益：构造倒地场景，断言附近队友可救起、远处队友不受影响。</summary>
        private void CheckTeamMechanics(SimReport report)
        {
            if (_nodes.Count < 3)
            {
                report.Add("TeammateRevival", false, "节点不足，无法构造复活场景");
                return;
            }

            // 冻结输入，令玩家 1 倒地、玩家 2 处于复活半径内、玩家 3 远离。
            _host.ClearInputs();
            _host.SetPlayerPosition(1, new Vector2(0f, 0f));
            _host.SetPlayerPosition(2, new Vector2(1f, 0f));
            _host.SetPlayerPosition(3, new Vector2(60f, 60f));
            _host.ApplyDamage(1, SimNode.MaxPlayerHealth * 10f);

            report.Add("TeammateDowned", !_host.IsAlive(1), _host.IsAlive(1) ? "玩家 1 未倒地" : string.Empty);

            var frames = Mathf.CeilToInt((_host.ReviveDuration + 0.5f) * _options.TickRate);
            PumpNetwork(1f / _options.TickRate, frames);

            _host.TryGetHealth(1, out var revivedHealth);
            var expected = SimNode.MaxPlayerHealth * _host.ReviveHealthFraction;
            var revived = _host.IsAlive(1) && Mathf.Abs(revivedHealth - expected) <= 0.5f;
            report.Add("TeammateRevived", revived, $"health={revivedHealth:F1} expected={expected:F1}");

            _host.TryGetHealth(3, out var farHealth);
            report.Add("FarTeammateUnaffected", _host.IsAlive(3) && farHealth >= SimNode.MaxPlayerHealth - 0.5f,
                $"health={farHealth:F1}");

            var nearbyBonus = _host.GetAuraBonus(2);
            var farBonus = _host.GetAuraBonus(3);
            report.Add("CrossPlayerAura", nearbyBonus >= 1 && farBonus == 0,
                $"near={nearbyBonus} far={farBonus}");
        }

        private void CheckHostMigration(SimReport report)
        {
            var oldHostId = _host.PlayerId;
            _host.Transport.Stop();
            _dead.Add(oldHostId);

            var wait = _host.Migration.HeartbeatTimeout + _host.Migration.ElectionWindow + 1f;
            PumpNetwork(0.05f, Mathf.CeilToInt(wait / 0.05f));

            SimNode newHost = null;
            var hostCount = 0;
            for (var i = 0; i < _nodes.Count; i++)
            {
                var node = _nodes[i];
                if (!_dead.Contains(node.PlayerId) && node.Role == NetworkRole.Host)
                {
                    hostCount++;
                    newHost = node;
                }
            }

            report.Add("SingleNewHost", hostCount == 1, $"hostCount={hostCount}");
            report.Add("ElectedLowestJoinOrder", newHost != null && newHost.PlayerId == 1,
                newHost != null ? $"newHost={newHost.PlayerId}" : "no host");

            var restored = newHost != null && newHost.Migration.HasConfirmedSnapshot && newHost.HostWorldCount > 0;
            report.Add("RestoredFromSnapshot", restored,
                newHost != null ? $"world={newHost.HostWorldCount} snapshot={newHost.Migration.HasConfirmedSnapshot}" : "no host");

            if (newHost == null)
            {
                return;
            }

            var converged = true;
            var detail = "";
            for (var i = 0; i < _nodes.Count; i++)
            {
                var node = _nodes[i];
                if (_dead.Contains(node.PlayerId) || node == newHost)
                {
                    continue;
                }

                if (node.Transport.HostId != newHost.PlayerId)
                {
                    converged = false;
                    detail = $"节点 {node.PlayerId} HostId={node.Transport.HostId}";
                    break;
                }
            }

            report.Add("ClientsConvergeOnNewHost", converged, detail);

            var beforeFrame = -1;
            for (var i = 0; i < _nodes.Count; i++)
            {
                var node = _nodes[i];
                if (!_dead.Contains(node.PlayerId) && node != newHost)
                {
                    beforeFrame = node.Interp.LatestFrame;
                    break;
                }
            }

            PumpNetwork(1f / _options.TickRate, 60);

            var afterFrame = -1;
            for (var i = 0; i < _nodes.Count; i++)
            {
                var node = _nodes[i];
                if (!_dead.Contains(node.PlayerId) && node != newHost)
                {
                    afterFrame = node.Interp.LatestFrame;
                    break;
                }
            }

            report.Add("NewHostResumesBroadcast", afterFrame > beforeFrame,
                $"before={beforeFrame} after={afterFrame}");
        }

        private static bool TryFindEntity(StateSnapshot snapshot, int entityId, out EntityState entity)
        {
            var entities = snapshot.Entities;
            if (entities != null)
            {
                for (var i = 0; i < entities.Length; i++)
                {
                    if (entities[i].EntityId == entityId)
                    {
                        entity = entities[i];
                        return true;
                    }
                }
            }

            entity = default;
            return false;
        }

        private static List<int> SortedMembers(RoomService room)
        {
            var ids = new List<int>(room.Count);
            for (var i = 0; i < room.Members.Count; i++)
            {
                ids.Add(room.Members[i].PlayerId);
            }

            ids.Sort();
            return ids;
        }

        private static bool SameMembers(List<int> a, List<int> b)
        {
            if (a.Count != b.Count)
            {
                return false;
            }

            for (var i = 0; i < a.Count; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
