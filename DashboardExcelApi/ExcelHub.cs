using CommonDatabase;
using CommonDatabase.DTO;
using CommonDatabase.Enum;
using CommonDatabase.Models;
using FirebaseAdmin.Messaging;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StackExchange.Redis;
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace DashboardExcelApi
{
    public class ExcelHub : Hub
    {
        // DTO used for sending client details to callers
        public class ClientDetailsDto
        {
            public int Id { get; set; }
            public string Username { get; set; } = string.Empty;
            public int AccessNoOfNews { get; set; }
            public int AccessNoOfRate { get; set; }
            public string ClientName { get; set; } = string.Empty;
            public string DeviceToken { get; set; } = string.Empty;
            public bool IsActive { get; set; }
            public bool IsNews { get; set; }
            public bool IsRate { get; set; }
            public DateTime NewsExpiredDate { get; set; }
            public DateTime RateExpiredDate { get; set; }
        }

        // Metadata kept per connection
        private class ConnectionMetadata
        {
            public DateTime ConnectedAt { get; set; }
            public string Room { get; set; } = string.Empty;
        }
        private readonly IConfiguration _configuration;
        // Shared connections map (thread-safe)
        private static readonly ConcurrentDictionary<string, ConnectionMetadata> ActiveConnections = new();
        private static readonly ConcurrentDictionary<string, List<string>> ConnectionGroups = new();
        //private readonly IConnectionMultiplexer _redis;
        private readonly AppDbContext _context;
        private readonly ILogger<ExcelHub> _logger;

        // Redis key templates (centralised for easier change)
        private readonly string prefix = "";
        //private const string ClientDetailsKey = "clientDetails"; 
        private const string UserInstrumentKeyPrefix = "userInstrument:";
        private const string UserDetailsKey = "UserDetails";
        private const string ClientInstrumentListKey = "ClientInstrumentList";
        private readonly ConnectionStore _store;
        private readonly HubNotifier _hubNotifier;
        private readonly IDatabase _db;

        public ExcelHub(IConnectionMultiplexer redis, AppDbContext context, ILogger<ExcelHub> logger, ConnectionStore store, IConfiguration configuration, HubNotifier hubNotifier)
        {
            //_redis = redis ?? throw new ArgumentNullException(nameof(redis));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _store = store;
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            prefix = _configuration["Redis:Prefix"];
            _hubNotifier = hubNotifier;
            _db = redis.GetDatabase();
        }


        public byte[] Compress(string json)
        {
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionMode.Compress))
            using (var writer = new StreamWriter(gzip)) writer.Write(json);
            return output.ToArray();
        }

        public override async Task OnDisconnectedAsync(Exception ex)
        {
            var connectionId = Context?.ConnectionId;
            if (!string.IsNullOrEmpty(connectionId))
            {
                _store.RemoveConnection(connectionId);
                await _hubNotifier.RemoveAllGroupsAsync(ConnectionGroups, Groups, connectionId);
                //await _hubNotifier.SendToAllAsync(HubMethodName.UserDisconnected, connectionId);
            }

            await base.OnDisconnectedAsync(ex);
        }

        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation($"Connected: {Context.ConnectionId}");
            

            await _hubNotifier.SendToClientAsync(Context.ConnectionId, HubMethodName.UserConnected, Context.ConnectionId);
            await base.OnConnectedAsync();
        }

        public async Task Client(string room)
        {
            try
            {
                var groupName = GroupNameResolver.Resolve(room);
                var connectionId = Context.ConnectionId;

                //await _hubNotifier.RemoveAllGroupsAsync(ConnectionGroups, Groups, connectionId);
                await _hubNotifier.AddConnectionToGroupAsync(Context, groupName);
                //await _hubNotifier.AddOrUpdateGroup(ConnectionGroups, connectionId, groupName);
                _store.AddToGroup(connectionId, groupName);

                var userDetailsRaw = await _db.StringGetAsync($"{prefix}_{UserDetailsKey}");
                var userList = string.IsNullOrEmpty(userDetailsRaw)
                    ? new List<ClientDto>()
                    : JsonConvert.DeserializeObject<List<ClientDto>>(userDetailsRaw);

                var clientDevices = new
                {
                    status = userList.Exists(x => x.Username == room),
                    data = userList.Where(x => x.Username == room)
                };

                await _hubNotifier.SendToGroupAsync(groupName, HubMethodName.ReceiveMessage, clientDevices);

                bool exists = _db.KeyExists($"{prefix}_{ClientInstrumentListKey}");
                var rawUserResults = new List<ClientWiseInstrumentList>();
                if (exists)
                {
                    var getClientInstument = await _db.StringGetAsync($"{prefix}_{ClientInstrumentListKey}");
                    rawUserResults = string.IsNullOrEmpty(getClientInstument)
                        ? new List<ClientWiseInstrumentList>()
                        : JsonConvert.DeserializeObject<List<ClientWiseInstrumentList>>(getClientInstument);
                }
                else
                {
                    //int clientId = await _context.Client
                    //      .Where(x => x.Username == room)
                    //      .Select(x => x.Id)
                    //      .FirstOrDefaultAsync();

                    //if (clientId == 0)
                    //{
                    //    await _hubNotifier.SendToCallerAsync(Context, HubMethodName.Error, "Client not found.");
                    //    return;
                    //}
                    int clientId = 0;
                    rawUserResults = await _context.ClientWiseInstrumentList
                    .FromSqlInterpolated($"EXEC dbo.usp_ClientWiseInstumentList {clientId}")
                    .ToListAsync();
                    await _db.StringSetAsync($"{prefix}_{ClientInstrumentListKey}", System.Text.Json.JsonSerializer.Serialize(rawUserResults));
                }
                    
                var identifiers = rawUserResults.Where(x => x.Username == room)
                    .OrderBy(x => x.RowId)
                    .Select(r => new { i = r.Identifier, n = r.Contract, sc = r.SubContract })
                    .ToList();

                await _hubNotifier.SendToGroupAsync(groupName, HubMethodName.UserListOfSymbol, identifiers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Client() method");
                await _hubNotifier.SendToCallerAsync(Context, HubMethodName.Error, "Something went wrong while processing your request.");
            }
        }

        public async Task ClientWithDevice(string room, string deviceId)
        {
            try
            {
                var groupName = GroupNameResolver.Resolve(room);
                var connectionId = Context.ConnectionId;

                // Remove all groups for this connection
                await _hubNotifier.RemoveAllGroupsAsync(ConnectionGroups, Groups, connectionId);

                // Add to device-specific group
                var deviceGroup = $"{groupName}_{deviceId}";
                await _hubNotifier.AddConnectionToGroupAsync(Context, deviceGroup);
                await _hubNotifier.AddOrUpdateGroup(ConnectionGroups, connectionId, deviceGroup);

                // Load user details from Redis
                var userDetailsRaw = await _db.StringGetAsync($"{prefix}_{UserDetailsKey}");
                var userList = string.IsNullOrEmpty(userDetailsRaw)
                    ? new List<ClientDto>()
                    : JsonConvert.DeserializeObject<List<ClientDto>>(userDetailsRaw);

                var clientDevices = new
                {
                    status = userList.Exists(x => x.Username == room),
                    data = userList
                    .Where(x => x.Username == room)
                    .Select(y => new
                    {
                        y.RateExpireDate,
                        y.NewsExpireDate,
                        y.Username,
                        y.Keywords,
                        DeviceAccess = y.DeviceAccess.FirstOrDefault(i => i.DeviceId == deviceId),
                        y.IsActive,
                        y.Id,
                        y.Topics
                    })
                };

                await _hubNotifier.SendToClientAsync(connectionId, HubMethodName.ReceiveMessage, clientDevices);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ClientWithDevice() method");
                await _hubNotifier.SendToCallerAsync(Context, HubMethodName.Error, "Something went wrong while processing your request.");
            }
        }
        public async Task GetAllClient()
        {
            try
            {
                var groupName = GroupNameResolver.Resolve("CalcifyAllClient");
                var connId = Context.ConnectionId;

                // Remove all groups for this connection
                await _hubNotifier.RemoveAllGroupsAsync(ConnectionGroups, Groups, connId);

                // Add to group
                await _hubNotifier.AddConnectionToGroupAsync(Context, groupName);
                await _hubNotifier.AddOrUpdateGroup(ConnectionGroups, connId, groupName);

                // Load user details from Redis
                var userDetailsRaw = await _db.StringGetAsync($"{prefix}_{UserDetailsKey}");
                var userList = string.IsNullOrEmpty(userDetailsRaw)
                    ? new List<ClientDto>()
                    : JsonConvert.DeserializeObject<List<ClientDto>>(userDetailsRaw);

                var clientDevices = new
                {
                    status = userList.Any(),
                    data = userList.Select(x => new { x.Id, x.Username })
                };

                await _hubNotifier.SendToGroupAsync(groupName, HubMethodName.ReceiveAllClient, clientDevices);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetAllClient() method");
                await _hubNotifier.SendToCallerAsync(Context, HubMethodName.Error, "Something went wrong while processing your request.");
            }
        }

        public async Task SymbolLastTick(List<string> symbols)
        {
            var connId = Context.ConnectionId;
            try
            {
                foreach (var symbol in symbols)
                {
                    var symbolData = "";
                    if (symbol == "CDUTY")
                    {
                        symbolData = await _db.StringGetAsync($"{prefix}_{symbol}");
                    }
                    else
                    {
                        symbolData = await _db.StringGetAsync(symbol);
                    }

                    await _hubNotifier.SendToClientAsync(connId, HubMethodName.excelRate, Compress(symbolData));
                    await _hubNotifier.SendToClientAsync(connId, HubMethodName.excelBase, symbolData);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Client() method");
                await _hubNotifier.SendToCallerAsync(Context, HubMethodName.Error, "Something went wrong while processing your request.");
            }
        }
        public async Task SubscribeSymbols(List<string> symbols)
        {
            if (symbols == null || symbols.Count == 0)
                return;

            foreach (var symbol in symbols.Where(s => !string.IsNullOrWhiteSpace(s)))
            {
                await _hubNotifier.AddConnectionToGroupAsync(Context, symbol.Trim());
            }
        }

        public async Task SubscribeUsers(string user)
        {
            if (string.IsNullOrWhiteSpace(user)) return;
            await _hubNotifier.AddConnectionToGroupAsync(Context, user.Trim());
        }

        /// <summary>
        /// Retrieves instrument/rate payloads for a given username by reading Redis and the DB.
        /// </summary>
        public async Task<ApiResponse> GetSymbolRatesByClientIdAsync(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return ApiResponse.Fail("Username is required");

            try
            {
                var client = await _context.Client.FirstOrDefaultAsync(c => c.Username == username);
                if (client == null)
                    return ApiResponse.Fail("Client not found");

                int clientId = client.Id;
                var userInstrumentKey = $"{prefix}_{UserInstrumentKeyPrefix}" + username;
                var userInstrumentJson = await _db.StringGetAsync(userInstrumentKey);
                if (userInstrumentJson.IsNullOrEmpty)
                    return ApiResponse.Fail("No instruments found for this user in redis");
                Dictionary<string, List<string>>? userMap;
                try
                {
                    userMap = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(userInstrumentJson!);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to deserialize userInstrument JSON");
                    return ApiResponse.Fail("Invalid instrument data format");
                }

                if (userMap == null || !userMap.ContainsKey(username))
                    return ApiResponse.Fail("No instruments found for this user");

                var identifiers = userMap[username] ?? new List<string>();
                if (identifiers.Count == 0)
                    return ApiResponse.Fail("No instrument identifiers for this user");


                var keys = identifiers.Select(id => (RedisKey)id).ToArray();
                var values = await _db.StringGetAsync(keys);

                var dbInstruments = await _context.Instruments.Where(i => i.ClientId == clientId && identifiers.Contains(i.Identifier) && i.IsMapped)
                    .Select(i => new SubscribeInstrumentView { Identifier = i.Identifier, Contract = i.Contract }).ToListAsync();
                var dbInstrumentsMap = dbInstruments.DistinctBy(i => i.Identifier, StringComparer.OrdinalIgnoreCase).ToDictionary(i => i.Identifier, i => i, StringComparer.OrdinalIgnoreCase);

                var updatedValues = new List<Dictionary<string, object>>();

                for (int idx = 0; idx < values.Length; idx++)
                {
                    var val = values[idx];
                    if (val.IsNullOrEmpty) continue;

                    try
                    {
                        var obj = JObject.Parse(val);
                        var identifierInRedis = obj["i"]?.ToString() ?? string.Empty;

                        if (!string.IsNullOrEmpty(identifierInRedis) && dbInstrumentsMap.TryGetValue(identifierInRedis, out var dbInstrument))
                        {
                            obj["n"] = dbInstrument.Contract ?? obj["n"] ?? string.Empty;
                        }

                        var dict = obj.ToObject<Dictionary<string, object>>();
                        if (dict != null) updatedValues.Add(dict);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Skipping invalid json value for identifier at index {Index}", idx);
                    }
                }

                var redisIdentifiers = updatedValues.Select(x => x.GetValueOrDefault("i")?.ToString()).Where(x => !string.IsNullOrEmpty(x)).ToHashSet(StringComparer.OrdinalIgnoreCase);

                // Add placeholders for mapped instruments that were missing from redis
                var missingFromRedis = dbInstrumentsMap.Where(kv => !redisIdentifiers.Contains(kv.Key))
                    .Select(kv => new Dictionary<string, object>
                    {
                        ["n"] = kv.Value.Contract ?? string.Empty,
                        ["i"] = kv.Key,
                        ["b"] = "--",
                        ["a"] = "--",
                        ["ltp"] = "--",
                        ["h"] = "--",
                        ["l"] = "--",
                        ["t"] = "N/A",
                        ["o"] = "--",
                        ["c"] = "--",
                        ["d"] = "--",
                        ["v"] = "--"
                    })
                    .ToList();

                updatedValues.AddRange(missingFromRedis);

                return ApiResponse.Ok(updatedValues);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while retrieving symbol rates for user {Username}", username);
                return ApiResponse.Fail("Unexpected error retrieving instruments");
            }
        }
    }

    public class QueryStringUserIdProvider : IUserIdProvider
    {
        private readonly string[] _allowedOrigins;
        private readonly string _mobileAuthKey;

        public QueryStringUserIdProvider(string[] allowedOrigins, string mobileAuthKey = "Starline@1008")
        {
            _allowedOrigins = allowedOrigins ?? Array.Empty<string>();
            _mobileAuthKey = mobileAuthKey;
        }

        public string? GetUserId(HubConnectionContext connection)
        {
            var query = connection.GetHttpContext()?.Request.Query;
            var user = query?["user"].ToString();
            var auth = query?["auth"].ToString();
            var type = query?["type"].ToString();

            if (string.Equals(type, "web", StringComparison.OrdinalIgnoreCase))
            {
                var httpContext = connection.GetHttpContext();
                var origin = httpContext?.Request.Headers["Origin"].ToString();
                if (!string.IsNullOrEmpty(origin) && _allowedOrigins.Contains(origin))
                {
                    return user;
                }

                return null;
            }
            else if (string.Equals(type, "mobile", StringComparison.OrdinalIgnoreCase) && auth == _mobileAuthKey)
            {
                return user;
            }

            return null;
        }
    }
}
