using CommonDatabase;
using CommonDatabase.DTO;
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
        private readonly IConnectionMultiplexer _redis;
        private readonly AppDbContext _context;
        private readonly ILogger<ExcelHub> _logger;

        // Redis key templates (centralised for easier change)
        private readonly string prefix = "";
        //private const string ClientDetailsKey = "clientDetails"; 
        private const string UserInstrumentKeyPrefix = "userInstrument:"; 
        private const string UserDetailsKey = "UserDetails";
        private readonly ConnectionStore _store;


        public ExcelHub(IConnectionMultiplexer redis, AppDbContext context, ILogger<ExcelHub> logger, ConnectionStore store, IConfiguration configuration)
        {
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _store = store;
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            prefix = _configuration["Redis:Prefix"];
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
                //ActiveConnections.TryRemove(connectionId, out _);
                // Remove from in-memory group
                if (ConnectionGroups.TryRemove(connectionId, out var groups))
                {
                    foreach (var group in groups.Distinct())
                    {
                        await Groups.RemoveFromGroupAsync(connectionId, group);
                    }

                }

                await Clients.All.SendAsync("UserDisconnected", Context.ConnectionId);
            }

            await base.OnDisconnectedAsync(ex);
        }

        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation($"Connected: {Context.ConnectionId}");
            _store.RemoveByConnection(Context.ConnectionId);

            await Clients.Client(Context.ConnectionId).SendAsync("UserConnected", Context.ConnectionId);
            await base.OnConnectedAsync();
        }

        public async Task Client(string room)
        {
            try
            {
                var groupName = GroupNameResolver.Resolve(room);

                var connectionId = Context.ConnectionId;

                // Remove from old room
                if (ConnectionGroups.TryRemove(connectionId, out var groups))
                {
                    foreach (var group in groups.Distinct())
                    {
                        await Groups.RemoveFromGroupAsync(connectionId, group);
                    }
                }

                await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
                ConnectionGroups.AddOrUpdate(
                    Context.ConnectionId,
                    new List<string> { groupName },
                    (_, existing) => { existing.Add(groupName); return existing; }
                );

                _store.Add(groupName, Context.ConnectionId);

                IDatabase db = _redis.GetDatabase();
                var userDetailsRaw = await db.StringGetAsync($"{prefix}_{UserDetailsKey}");

                var userList = JsonConvert.DeserializeObject<List<ClientDto>>(userDetailsRaw!);
                await Clients.Group(groupName).SendAsync(
                    "ReceiveMessage", 
                    new { 
                        status = userList.Exists(x => x.Username == room), 
                        data = userList.Where(x => x.Username == room) 
                    }
                );
                int ClientId = await _context.Client.Where(x => x.Username == room).Select(x => x.Id).FirstOrDefaultAsync();
                ////var identifiers = await _context.Instruments
                ////        .Where(a => a.ClientId == ClientId && a.IsMapped)
                ////        .Select(a => new { i = a.Identifier, n = a.Contract })
                ////        .ToListAsync();
                //var identifiers = await ( from d in
                //                    (from s in _context.Subscribe 
                //                  join i in _context.Instruments
                //                  on s.Identifier equals i.Identifier
                //                  where i.IsMapped == true 
                //                  select new { c= i.ClientId, i = i.Identifier, n = i.Contract }) 
                //                   join c in _context.Client
                //                        on d.c equals c.Id
                //                          where c.Id == ClientId
                //                          select new { i = d.i, n = d.n }

                //                  ).ToListAsync();

                ////var identifierList = (from d in identifiers 
                ////                     join c in _context.Client
                ////                        on d.c equals c.Id
                ////                    where c.Id == ClientId
                ////                     select new {  i = d.i, n = d.n }).ToList();
                ////string listOfSymbol = string.Join(",", identifiers);
                ///
                // var rawUserResults = await _context.ClientWiseInstrumentList
                //.FromSqlRaw(
                //    "EXEC dbo.usp_ClientWiseInstumentList @ClientId", ClientId
                //)
                //.ToListAsync();
                var rawUserResults = await _context.ClientWiseInstrumentList
                 .FromSqlInterpolated($"EXEC dbo.usp_ClientWiseInstumentList {ClientId}")
                 .ToListAsync();


                var identifiers = rawUserResults.OrderBy(x => x.RowId).Select(r => new { i = r.Identifier, n = r.Contract }).ToList();
                await Clients.Group(groupName).SendAsync("UserListOfSymbol", identifiers);
            }
            catch (Exception ex)
                {
                _logger.LogError(ex, "Error in Client() method");
                await Clients.Caller.SendAsync("error", "Something went wrong while processing your request.");
            }
        }

        public async Task ClientWithDevice(string room, string deviceId)
        {
            try
            {
                var groupName = GroupNameResolver.Resolve(room);
                var connectionId = Context.ConnectionId;

                //// Remove from old room
                if (ConnectionGroups.TryRemove(connectionId, out var groups))
                {
                    foreach (var group in groups.Distinct())
                    {
                        await Groups.RemoveFromGroupAsync(connectionId, group);
                    }
                }

                await Groups.AddToGroupAsync(Context.ConnectionId, $"{groupName}_{deviceId}" );
                ConnectionGroups.AddOrUpdate(
                    Context.ConnectionId,
                    new List<string> { $"{groupName}_{deviceId}" },
                    (_, existing) => { existing.Add($"{groupName}_{deviceId}"); return existing; }
                );

                IDatabase db = _redis.GetDatabase();
                var userDetailsRaw = await db.StringGetAsync($"{prefix}_{UserDetailsKey}");

                var userList = JsonConvert.DeserializeObject<List<ClientDto>>(userDetailsRaw!);
                await Clients.Client(connectionId).SendAsync(
                    "ReceiveMessage",
                    new
                    {
                        status = userList.Exists(x => x.Username == room),
                        data = userList
                                .Where(x => x.Username == room)
                                .Select(y => new
                                {
                                    RateExpireDate = y.RateExpireDate,
                                    NewsExpireDate = y.NewsExpireDate,
                                    Username = y.Username,
                                    Keywords = y.Keywords,
                                    DeviceAccess = y.DeviceAccess.Where(i => i.DeviceId == deviceId),
                                    IsActive = y.IsActive,
                                    Id = y.Id,
                                    Topics = y.Topics
                                })
                    }
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Client() method");
                await Clients.Caller.SendAsync("error", "Something went wrong while processing your request.");
            }
        }
        public async Task GetAllClient()
        {
            try
            {
                var groupName = GroupNameResolver.Resolve("CalcifyAllClient");
                var connId = Context.ConnectionId;
                IDatabase db = _redis.GetDatabase();
                var userDetailsRaw = await db.StringGetAsync($"{prefix}_{UserDetailsKey}");
                //_store.Add("AllClient", Context.ConnectionId);
                //// Remove from old room
                if (ConnectionGroups.TryRemove(connId, out var groups))
                {
                    foreach (var group in groups.Distinct())
                    {
                        await Groups.RemoveFromGroupAsync(connId, group);
                    }
                }
                await Groups.AddToGroupAsync(connId, groupName);
                ConnectionGroups.AddOrUpdate(
                    Context.ConnectionId,
                    new List<string> { groupName },
                    (_, existing) => { existing.Add(groupName); return existing; }
                );
                var userList = JsonConvert.DeserializeObject<List<ClientDto>>(userDetailsRaw!);
                await Clients.Group(groupName).SendAsync(
                    "ReceiveAllClient",
                    new
                    {
                       status = true,
                       data = userList.Select(x => new { x.Id, x.Username })
                    }
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Client() method");
                await Clients.Caller.SendAsync("error", "Something went wrong while processing your request.");
            }
        }

        public async Task SymbolLastTick(List<string> symbols)
        {
            try
            {
                var connId = Context.ConnectionId;
                IDatabase db = _redis.GetDatabase();
                foreach (var symbol in symbols)
                {
                    var symbolData = "";
                    if (symbol == "CDUTY")
                    {
                        symbolData = await db.StringGetAsync($"{prefix}_{symbol}");
                    }
                    else
                    {
                        symbolData = await db.StringGetAsync(symbol);
                    }
                    
                    await Clients.Client(connId).SendAsync("excelRate", Compress(symbolData));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Client() method");
                await Clients.Caller.SendAsync("error", "Something went wrong while processing your request.");
            }
        }
        public async Task SubscribeSymbols(List<string> symbols)
        {
            if (symbols == null || symbols.Count == 0)
                return;

            foreach (var symbol in symbols.Where(s => !string.IsNullOrWhiteSpace(s)))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, symbol.Trim());
            }
        }

        public async Task SubscribeUsers(string user)
        {
            if (string.IsNullOrWhiteSpace(user)) return;
            await Groups.AddToGroupAsync(Context.ConnectionId, user.Trim());
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
                IDatabase db = _redis.GetDatabase();               
                var userInstrumentKey = $"{prefix}_{UserInstrumentKeyPrefix}" + username;
                var userInstrumentJson = await db.StringGetAsync(userInstrumentKey);
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
                var values = await db.StringGetAsync(keys);
              
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
