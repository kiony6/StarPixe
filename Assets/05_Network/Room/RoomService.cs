using System;
using System.Collections.Generic;

namespace StarPixelSurge.Network
{
    /// <summary>房间成员。</summary>
    public class RoomMember
    {
        public int PlayerId;
        public int JoinOrder;
        public bool Ready;
        public bool IsHost;
    }

    /// <summary>
    /// 房间服务：维护成员名册、加入顺序、准备态与房主标记，
    /// 并裁决开局条件（名册锁定）。
    /// </summary>
    public class RoomService
    {
        public const int MinPlayers = 2;
        public const int MaxPlayers = 4;

        private readonly List<RoomMember> _members = new List<RoomMember>();
        private int _nextJoinOrder;

        public IReadOnlyList<RoomMember> Members => _members;

        public bool RosterLocked { get; private set; }

        public event Action RosterChanged;

        public void Reset()
        {
            _members.Clear();
            _nextJoinOrder = 0;
            RosterLocked = false;
            RosterChanged?.Invoke();
        }

        public RoomMember Join(int playerId)
        {
            var existing = Get(playerId);
            if (existing != null)
            {
                return existing;
            }

            if (_members.Count >= MaxPlayers)
            {
                return null;
            }

            var member = new RoomMember
            {
                PlayerId = playerId,
                JoinOrder = _nextJoinOrder++,
                Ready = false,
                IsHost = false
            };

            _members.Add(member);
            RosterChanged?.Invoke();
            return member;
        }

        public void Leave(int playerId)
        {
            for (var i = 0; i < _members.Count; i++)
            {
                if (_members[i].PlayerId == playerId)
                {
                    _members.RemoveAt(i);
                    RosterChanged?.Invoke();
                    return;
                }
            }
        }

        public void SetReady(int playerId, bool ready)
        {
            var member = Get(playerId);
            if (member == null)
            {
                return;
            }

            member.Ready = ready;
            RosterChanged?.Invoke();
        }

        /// <summary>踢出成员，仅房主可执行。</summary>
        public bool Kick(int requesterId, int targetId)
        {
            var requester = Get(requesterId);
            if (requester == null || !requester.IsHost)
            {
                return false;
            }

            Leave(targetId);
            return true;
        }

        public void SetHost(int playerId)
        {
            for (var i = 0; i < _members.Count; i++)
            {
                _members[i].IsHost = _members[i].PlayerId == playerId;
            }

            RosterChanged?.Invoke();
        }

        /// <summary>
        /// 以房主权威名册对齐本地成员集合：补齐缺失成员、移除已离开成员并更新房主。
        /// 保留既有成员的加入顺序，保证选举顺序稳定。
        /// </summary>
        public void SyncMembers(int hostId, IReadOnlyList<int> memberIds)
        {
            if (memberIds == null)
            {
                return;
            }

            for (var i = 0; i < memberIds.Count; i++)
            {
                Join(memberIds[i]);
            }

            var remove = new List<int>();
            for (var i = 0; i < _members.Count; i++)
            {
                var id = _members[i].PlayerId;
                var found = false;
                for (var j = 0; j < memberIds.Count; j++)
                {
                    if (memberIds[j] == id)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    remove.Add(id);
                }
            }

            for (var i = 0; i < remove.Count; i++)
            {
                Leave(remove[i]);
            }

            SetHost(hostId);
        }

        public RoomMember Get(int playerId)
        {
            for (var i = 0; i < _members.Count; i++)
            {
                if (_members[i].PlayerId == playerId)
                {
                    return _members[i];
                }
            }

            return null;
        }

        public int Count => _members.Count;

        public bool AllReady()
        {
            if (_members.Count == 0)
            {
                return false;
            }

            for (var i = 0; i < _members.Count; i++)
            {
                if (!_members[i].Ready)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>房主且人数在 2-4 且全员准备时可开局。</summary>
        public bool CanStart(int localPlayerId)
        {
            var self = Get(localPlayerId);
            return !RosterLocked
                   && self != null
                   && self.IsHost
                   && _members.Count >= MinPlayers
                   && _members.Count <= MaxPlayers
                   && AllReady();
        }

        public void LockRoster()
        {
            RosterLocked = true;
            RosterChanged?.Invoke();
        }

        /// <summary>返回按加入顺序排列的成员 ID，用于房主选举。</summary>
        public List<int> MembersByJoinOrder()
        {
            var sorted = new List<RoomMember>(_members);
            sorted.Sort((a, b) => a.JoinOrder.CompareTo(b.JoinOrder));

            var result = new List<int>(sorted.Count);
            for (var i = 0; i < sorted.Count; i++)
            {
                result.Add(sorted[i].PlayerId);
            }

            return result;
        }

        public int HostId()
        {
            for (var i = 0; i < _members.Count; i++)
            {
                if (_members[i].IsHost)
                {
                    return _members[i].PlayerId;
                }
            }

            return -1;
        }
    }
}
