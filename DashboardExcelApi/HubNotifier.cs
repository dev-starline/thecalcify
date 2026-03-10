using CommonDatabase.Enum;
using CommonDatabase.Models;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace DashboardExcelApi
{
    public class HubNotifier
    {
        private readonly IHubContext<ExcelHub> _hubContext;

        public HubNotifier(IHubContext<ExcelHub> hubContext)
        {
            _hubContext = hubContext;
        }
        // <summary>
        /// Adds the current connection to a group.
        /// </summary>
        public async Task AddConnectionToGroupAsync(HubCallerContext context, string groupName)
        {
            if (string.IsNullOrWhiteSpace(groupName))
                throw new ArgumentException("Group name cannot be null or empty.", nameof(groupName));

            await _hubContext.Groups.AddToGroupAsync(context.ConnectionId, groupName.Trim());
        }

        /// <summary>
        /// Removes the current connection from a group.
        /// </summary>
        public async Task RemoveConnectionFromGroupAsync(HubCallerContext context, string groupName)
        {
            if (string.IsNullOrWhiteSpace(groupName))
                throw new ArgumentException("Group name cannot be null or empty.", nameof(groupName));

            await _hubContext.Groups.RemoveFromGroupAsync(context.ConnectionId, groupName.Trim());
        }

        /// <summary>
        /// Sends a message to a specific SignalR group.
        /// </summary>
        /// <summary>
        /// Sends a message to all connected clients.
        /// </summary>
        public async Task SendToAllAsync(HubMethodName method, string connectionId)
        {
            await _hubContext.Clients.All.SendAsync(method.ToString(), connectionId);
        }

        /// <summary>
        /// Sends a message to a specific group.
        /// </summary>
        public async Task SendToGroupAsync(string groupName, HubMethodName method, object message)
        {
            await _hubContext.Clients.Group(groupName).SendAsync(method.ToString(), message);
        }

        /// <summary>
        /// Sends a message to a specific client.
        /// </summary>
        public async Task SendToClientAsync(string connectionId, HubMethodName method, object message)
        {
            await _hubContext.Clients.Client(connectionId).SendAsync(method.ToString(), message);
        }

        /// <summary>
        /// Send a message to the caller (the client who invoked the hub method).
        /// </summary>
        public async Task SendToCallerAsync(HubCallerContext context, HubMethodName method, object message)
        {
            await _hubContext.Clients.Client(context.ConnectionId).SendAsync(method.ToString(), message);
        }
        /// <summary>
        /// Adds or updates a connection's group list with the given groupName + deviceId.
        /// </summary>
        public async Task AddOrUpdateGroup(ConcurrentDictionary<string, List<string>> connectionGroups, string connectionId, string groupKey)
        {
            //var groupKey = $"{groupName}_{deviceId}";

            connectionGroups.AddOrUpdate(
                connectionId,
                new List<string> { groupKey },
                (_, existing) =>
                {
                    if (!existing.Contains(groupKey))
                    {
                        existing.Add(groupKey);
                    }
                    return existing;
                });
        }

        /// <summary>
        /// Removes a group from a connection's list.
        /// </summary>
        public async Task RemoveGroup(ConcurrentDictionary<string, List<string>> connectionGroups, string connectionId,string groupKey)
        {
            //var groupKey = $"{groupName}_{deviceId}";

            if (connectionGroups.TryGetValue(connectionId, out var existing))
            {
                existing.Remove(groupKey);
                if (existing.Count == 0)
                {
                    connectionGroups.TryRemove(connectionId, out _);
                }
            }
        }
        /// <summary>
        /// Removes a connection from all groups in both the dictionary and SignalR.
        /// </summary>
        public async Task RemoveAllGroupsAsync(
            ConcurrentDictionary<string, List<string>> connectionGroups,
            IGroupManager groups,
            string connectionId)
        {
            if (connectionGroups.TryRemove(connectionId, out var groupsList))
            {
                foreach (var group in groupsList.Distinct())
                {
                    await groups.RemoveFromGroupAsync(connectionId, group);
                }
            }
        }

    }
}
