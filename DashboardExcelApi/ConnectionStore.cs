using System.Collections.Concurrent;

namespace DashboardExcelApi
{
    public class ConnectionStore
    {
        private readonly ConcurrentDictionary<string, HashSet<string>> _connections =
            new ConcurrentDictionary<string, HashSet<string>>();

        public void AddConnection(string connectionId)
        {
            _connections.TryAdd(connectionId, new HashSet<string>());
        }

        public void RemoveConnection(string connectionId)
        {
            _connections.TryRemove(connectionId, out _);
        }

        public void AddToGroup(string connectionId, string groupName)
        {
            var groups = _connections.GetOrAdd(connectionId, _ => new HashSet<string>());
            lock (groups) { groups.Add(groupName); }
        }

        public void RemoveFromGroup(string connectionId, string groupName)
        {
            if (_connections.TryGetValue(connectionId, out var groups))
            {
                lock (groups) { groups.Remove(groupName); }
            }
        }

        public int TotalConnections => _connections.Count;
        public int TotalGroups => _connections.Values.SelectMany(g => g).Distinct().Count();

        public Dictionary<string, IEnumerable<string>> Snapshot()
        {
            return _connections.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.AsEnumerable());
        }
    }


}
