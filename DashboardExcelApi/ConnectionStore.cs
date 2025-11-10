using System.Collections.Concurrent;

namespace DashboardExcelApi
{
    public class ConnectionStore
    {
        private readonly ConcurrentDictionary<string, string> _userConnections = new();

        public void Add(string userId, string connectionId)
        {
            _userConnections[userId] = connectionId;
        }

        public void RemoveByConnection(string connectionId)
        {
            var user = _userConnections.FirstOrDefault(x => x.Value == connectionId).Key;
            if (user != null) _userConnections.TryRemove(user, out _);
        }

        public string? GetConnectionId(string userId)
        {
            return _userConnections.TryGetValue(userId, out var connId) ? connId : null;
        }

        public IEnumerable<string> GetAllConnectionIds() => _userConnections.Values;
    }

}
