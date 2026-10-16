using System;
using System.IO;
using System.Text;
using StarPixelSurge.GameCore;
using StarPixelSurge.Network;
using StarPixelSurge.Network.Simulation;
using UnityEngine;

namespace StarPixelSurge.Framework
{
    /// <summary>
    /// 真实 UDP 双进程联机测试的运行期驱动。仅当命令行包含 -netRole 时激活，
    /// 其余情况完全不介入正常游戏流程。
    ///
    /// 该驱动在构建出的 Player（或编辑器 Play）中自动完成：
    /// 配置 UDP 传输 → 创建/加入房间 → 选择职业 → 准备 → 开局 → 采样快照与实体复制
    /// → （客户端）等待房主退出后完成房主迁移。每个进程把结构化结果写入 -netResult 指定文件，
    /// 由编辑器侧的 <c>UdpTwoProcessTestRunner</c> 汇总判定。
    ///
    /// 命令行参数：
    /// -netRole host|client      角色（必填，缺失则本驱动不激活）
    /// -netHost 127.0.0.1        房主地址（客户端使用）
    /// -netPort 7777             房主端口
    /// -netRoom TEST             房间号
    /// -netClass thunder_mage    所选职业
    /// -netDuration 20           战斗采样时长（秒）；房主到点后退出以触发迁移
    /// -netExpectMigration       客户端是否等待并校验房主迁移
    /// -netWaitMigration 8       客户端等待迁移的上限（秒）
    /// -netResult path           结果文件路径
    /// </summary>
    public class NetworkPlaytestDriver : MonoBehaviour
    {
        private enum Phase
        {
            WaitBootstrap,
            WaitMatch,
            Running,
            Migration,
            Done
        }

        private const float EnterRoomTimeout = 20f;
        private const float WaitMatchTimeout = 20f;
        private const float MigrationSettle = 1.2f;

        private readonly SimReport _report = new SimReport();

        private string _role;
        private string _host;
        private int _port;
        private string _room;
        private string _classId;
        private float _duration;
        private bool _expectMigration;
        private float _waitMigration;
        private string _resultPath;

        private Phase _phase = Phase.WaitBootstrap;
        private float _phaseElapsed;
        private float _lifeElapsed;
        private float _maxLifetime = 120f;
        private int _exceptions;
        private bool _finished;

        private NetManager _net;
        private int _startFrame = -1;
        private int _lastSnapshotFrame = -1;
        private int _maxEntities;
        private bool _started;
        private bool _migrationElected;
        private int _migrationFrame;
        private bool _migrationBroadcast;

        /// <summary>命令行存在 -netRole 时创建驱动；否则直接返回，不影响正常启动。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var args = Environment.GetCommandLineArgs();
            var role = GetArg(args, "-netRole", null);
            if (string.IsNullOrEmpty(role))
            {
                return;
            }

            var go = new GameObject("NetworkPlaytestDriver");
            DontDestroyOnLoad(go);
            var driver = go.AddComponent<NetworkPlaytestDriver>();
            driver.Configure(args, role);
        }

        private void Configure(string[] args, string role)
        {
            _role = role.ToLowerInvariant();
            _host = GetArg(args, "-netHost", "127.0.0.1");
            _port = ParseInt(GetArg(args, "-netPort", "7777"), 7777);
            _room = GetArg(args, "-netRoom", "TEST");
            _classId = GetArg(args, "-netClass", "thunder_mage");
            _duration = ParseFloat(GetArg(args, "-netDuration", "20"), 20f);
            _expectMigration = HasFlag(args, "-netExpectMigration");
            _waitMigration = ParseFloat(GetArg(args, "-netWaitMigration", "8"), 8f);
            _resultPath = GetArg(args, "-netResult", Path.Combine(Application.persistentDataPath, $"udp_playtest_{_role}.txt"));
            _maxLifetime = _duration + _waitMigration + EnterRoomTimeout + WaitMatchTimeout + 20f;

            Application.logMessageReceived += OnLog;
            Debug.Log($"[Playtest:{_role}] 启动 端口={_port} 房主={_host} 房间={_room} 时长={_duration}s 迁移={_expectMigration}");
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Exception)
            {
                _exceptions++;
                Debug.LogError($"[Playtest:{_role}] 捕获异常: {condition}");
            }
        }

        private void Update()
        {
            if (_finished)
            {
                return;
            }

            _lifeElapsed += Time.deltaTime;
            _phaseElapsed += Time.deltaTime;

            if (_lifeElapsed > _maxLifetime)
            {
                _report.Add("WithinLifetime", false, $"超过 {_maxLifetime:F0}s 未完成");
                Finish();
                return;
            }

            switch (_phase)
            {
                case Phase.WaitBootstrap:
                    TickWaitBootstrap();
                    break;
                case Phase.WaitMatch:
                    TickWaitMatch();
                    break;
                case Phase.Running:
                    TickRunning();
                    break;
                case Phase.Migration:
                    TickMigration();
                    break;
            }
        }

        private void TickWaitBootstrap()
        {
            if (GameBootstrap.Instance == null || !NetManager.HasInstance || !GameManager.HasInstance)
            {
                return;
            }

            _net = NetManager.Instance;
            _net.UseUdp = true;
            _net.HostPort = _port;
            _net.RemoteHost = _host;
            _net.RemotePort = _port;

            if (IsHost)
            {
                _net.CreateRoom();
                _report.Add("HostRoomCreated", _net.IsHost && _net.State == RoomState.Lobby, $"state={_net.State}");
            }
            else
            {
                var joined = _net.JoinRoom(_room);
                _report.Add("ClientJoinRequestAccepted", joined, $"room={_room}");
            }

            _net.SelectClass(_classId);
            _net.SetReady(true);
            _phase = Phase.WaitMatch;
            _phaseElapsed = 0f;
        }

        private void TickWaitMatch()
        {
            // 房主：人数与准备态齐备后开局。控制类消息的重发由 NetManager 在大厅内兜底。
            if (IsHost && !_started && _net.CanStartMatch() && _net.Room.Count >= 2)
            {
                _started = true;
                _report.Add("HostSawClient", _net.Room.Count >= 2, $"count={_net.Room.Count}");
                _report.Add("AllMembersReady", _net.Room.AllReady(), $"count={_net.Room.Count}");
                _net.StartMatch();
            }

            if (GameManager.Instance.State == GameState.Playing
                && GameManager.Instance.CurrentMatch.Mode == MatchMode.Multiplayer)
            {
                _startFrame = _net.Frame != null ? _net.Frame.CurrentFrameIndex : 0;
                _lastSnapshotFrame = _net.Frame != null ? _net.Frame.LatestSnapshotFrame : -1;
                _phase = Phase.Running;
                _phaseElapsed = 0f;
                _report.Add("EnteredMatch", true, $"class={_classId}");
                return;
            }

            if (_phaseElapsed > WaitMatchTimeout)
            {
                _report.Add("EnteredMatch", false, $"超时未进入对局 (state={GameManager.Instance.State}, role={_role})");
                Finish();
            }
        }

        private void TickRunning()
        {
            SampleWorld();

            if (IsHost)
            {
                if (_phaseElapsed >= _duration)
                {
                    _report.Add("WorldProgressed", _maxEntities > 0 || Kills() > 0 || WaveLayer() > 0,
                        $"entities={_maxEntities} kills={Kills()} wave={WaveLayer()}");
                    _report.Add("HostSampledFrames", FrameAdvancedFromStart() > 0, $"frames+={FrameAdvancedFromStart()}");
                    Debug.Log($"[Playtest:host] 采样完成，退出以触发房主迁移");
                    Finish();
                }

                return;
            }

            // 客户端：先在战斗期采样，再决定是否等待迁移。
            if (_phaseElapsed >= _duration)
            {
                _report.Add("SnapshotsAdvancing", _lastSnapshotFrame > _startFrame, $"start={_startFrame} last={_lastSnapshotFrame}");
                _report.Add("EntitiesReplicated", _maxEntities > 0, $"maxEntities={_maxEntities}");
                _report.Add("WorldProgressed", _maxEntities > 0 || Kills() > 0 || WaveLayer() > 0,
                    $"entities={_maxEntities} kills={Kills()} wave={WaveLayer()}");

                if (_expectMigration)
                {
                    _phase = Phase.Migration;
                    _phaseElapsed = 0f;
                }
                else
                {
                    Finish();
                }
            }
        }

        private void TickMigration()
        {
            if (!_migrationElected && _net.IsHost && _net.LocalPlayerId >= 0)
            {
                _migrationElected = true;
                _migrationFrame = _net.Frame != null ? _net.Frame.CurrentFrameIndex : 0;
                _report.Add("HostMigrationSucceeded", true, $"newHost={_net.LocalPlayerId}");
                Debug.Log($"[Playtest:client] 已接管为房主，帧={_migrationFrame}，等待广播恢复");
            }

            if (_migrationElected)
            {
                var frame = _net.Frame != null ? _net.Frame.CurrentFrameIndex : 0;
                if (!_migrationBroadcast && frame > _migrationFrame)
                {
                    _migrationBroadcast = true;
                    _report.Add("NewHostResumedBroadcast", true, $"frames+={frame - _migrationFrame}");
                }

                if (_phaseElapsed > MigrationSettle)
                {
                    Finish();
                }

                return;
            }

            if (_phaseElapsed > _waitMigration)
            {
                _report.Add("HostMigrationSucceeded", false, $"等待 {_waitMigration:F0}s 未完成迁移");
                Finish();
            }
        }

        private void SampleWorld()
        {
            if (_net.Frame != null)
            {
                _lastSnapshotFrame = _net.Frame.LatestSnapshotFrame;
                var entities = _net.Frame.GetInterpolatedEntities();
                if (entities != null && entities.Count > _maxEntities)
                {
                    _maxEntities = entities.Count;
                }
            }
        }

        private bool IsHost => _role == "host";

        private int FrameAdvancedFromStart()
        {
            if (_net == null || _net.Frame == null || _startFrame < 0)
            {
                return 0;
            }

            return _net.Frame.CurrentFrameIndex - _startFrame;
        }

        private static int Kills()
        {
            return GameManager.HasInstance ? GameManager.Instance.Kills : 0;
        }

        private static int WaveLayer()
        {
            return GameManager.HasInstance ? GameManager.Instance.WaveLayer : 0;
        }

        private void Finish()
        {
            if (_finished)
            {
                return;
            }

            _finished = true;
            _phase = Phase.Done;
            _report.Add("NoUnhandledExceptions", _exceptions == 0, $"exceptions={_exceptions}");

            var passed = _report.AllPassed;
            WriteResult(passed);
            Debug.Log($"[Playtest:{_role}] {_report.BuildSummary()}");

            Application.Quit(passed ? 0 : 1);
        }

        private void WriteResult(bool passed)
        {
            try
            {
                var builder = new StringBuilder();
                builder.Append("ROLE=").Append(_role).Append('\n');
                builder.Append("STATUS=").Append(passed ? "PASS" : "FAIL").Append('\n');
                builder.Append("PASSED=").Append(_report.PassedCount).Append('\n');
                builder.Append("TOTAL=").Append(_report.Checks.Count).Append('\n');
                for (var i = 0; i < _report.Checks.Count; i++)
                {
                    var check = _report.Checks[i];
                    builder.Append("CHECK|").Append(check.Name).Append('|')
                        .Append(check.Passed ? "PASS" : "FAIL").Append('|')
                        .Append(check.Detail).Append('\n');
                }

                var directory = Path.GetDirectoryName(_resultPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(_resultPath, builder.ToString());
            }
            catch (Exception e)
            {
                Debug.LogError($"[Playtest:{_role}] 写结果失败: {e.Message}");
            }
        }

        private static string GetArg(string[] args, string key, string fallback)
        {
            if (args == null)
            {
                return fallback;
            }

            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == key)
                {
                    return args[i + 1];
                }
            }

            return fallback;
        }

        private static bool HasFlag(string[] args, string key)
        {
            if (args == null)
            {
                return false;
            }

            for (var i = 0; i < args.Length; i++)
            {
                if (args[i] == key)
                {
                    return true;
                }
            }

            return false;
        }

        private static int ParseInt(string value, int fallback)
        {
            return int.TryParse(value, out var parsed) ? parsed : fallback;
        }

        private static float ParseFloat(string value, float fallback)
        {
            return float.TryParse(value, out var parsed) ? parsed : fallback;
        }
    }
}
