using System;
using System.Collections.Generic;
using StarPixelSurge.Framework;
using UnityEngine;

namespace StarPixelSurge.Network
{
    public enum RoomState
    {
        Idle = 0,
        Lobby = 1,
        Playing = 2
    }

    /// <summary>
    /// 联机管理器骨架。V1.0 单机版本仅保留房间与名册 API 结构，
    /// 帧同步与房主迁移在 V2.0 接入 UDP 传输后实现。
    /// </summary>
    public class NetManager : MonoSingleton<NetManager>
    {
        private readonly List<int> _players = new List<int>();
        private readonly HashSet<int> _ready = new HashSet<int>();

        public RoomState State { get; private set; } = RoomState.Idle;

        public bool IsHost { get; private set; }

        public string RoomId { get; private set; }

        public int LocalPlayerId { get; private set; } = -1;

        public IReadOnlyList<int> Players => _players;

        public event Action RoomUpdated;

        public void CreateRoom()
        {
            RoomId = Guid.NewGuid().ToString("N").Substring(0, 8);
            IsHost = true;
            LocalPlayerId = 0;
            _players.Clear();
            _ready.Clear();
            _players.Add(LocalPlayerId);
            State = RoomState.Lobby;
            RoomUpdated?.Invoke();
        }

        public bool JoinRoom(string roomId)
        {
            if (string.IsNullOrEmpty(roomId))
            {
                return false;
            }

            RoomId = roomId;
            IsHost = false;
            LocalPlayerId = _players.Count;
            if (!_players.Contains(LocalPlayerId))
            {
                _players.Add(LocalPlayerId);
            }

            State = RoomState.Lobby;
            RoomUpdated?.Invoke();
            return true;
        }

        public void LeaveRoom()
        {
            _players.Clear();
            _ready.Clear();
            RoomId = null;
            IsHost = false;
            LocalPlayerId = -1;
            State = RoomState.Idle;
            RoomUpdated?.Invoke();
        }

        public void SetReady(bool ready)
        {
            if (LocalPlayerId < 0)
            {
                return;
            }

            if (ready)
            {
                _ready.Add(LocalPlayerId);
            }
            else
            {
                _ready.Remove(LocalPlayerId);
            }

            RoomUpdated?.Invoke();
        }

        public bool CanStartMatch()
        {
            return IsHost && _players.Count >= 2 && _players.Count <= 4 && _ready.Count == _players.Count;
        }

        public void StartMatch()
        {
            if (!CanStartMatch())
            {
                return;
            }

            State = RoomState.Playing;
            RoomUpdated?.Invoke();
            if (GameManager.HasInstance)
            {
                GameManager.Instance.StartMatch(new MatchConfig(MatchMode.Multiplayer, null, UnityEngine.Random.Range(int.MinValue, int.MaxValue)));
            }
        }
    }
}
