using CommonDatabase;
using CommonDatabase.DTO;
using CommonDatabase.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StackExchange.Redis;
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text;

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

        // Shared connections map (thread-safe)
        private static readonly ConcurrentDictionary<string, ConnectionMetadata> ActiveConnections = new();

        private readonly IConnectionMultiplexer _redis;
        private readonly AppDbContext _context;
        private readonly ILogger<ExcelHub> _logger;

        // Redis key templates (centralised for easier change)
        private const string ClientDetailsKey = "clientDetails"; 
        private const string UserInstrumentKeyPrefix = "userInstrument:"; 

        public ExcelHub(IConnectionMultiplexer redis, AppDbContext context, ILogger<ExcelHub> logger)
        {
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
                ActiveConnections.TryRemove(connectionId, out _);
            }

            await base.OnDisconnectedAsync(ex);
        }



        public async Task Client(string room)
        {
            try
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, room);
                ActiveConnections[Context.ConnectionId] = new ConnectionMetadata
                {
                    ConnectedAt = DateTime.UtcNow,
                    Room = room
                };

                IDatabase db = _redis.GetDatabase();
                var contactDetailsRaw = await db.StringGetAsync(ClientDetailsKey);

                var contactList = JsonConvert.DeserializeObject<List<ClientDetailsDto>>(contactDetailsRaw!);
                var clientDetails = contactList?
                    .FirstOrDefault(c => string.Equals(c.ClientName, room, StringComparison.OrdinalIgnoreCase));

                if (clientDetails == null)
                {
                    await Clients.Caller.SendAsync("clientDetails", new { status = false });
                    return;
                }

                int newsLimit = Math.Max(0, clientDetails.AccessNoOfNews);
                int rateLimit = Math.Max(0, clientDetails.AccessNoOfRate);

                // order connections by connected time
                var roomConnections = ActiveConnections
                    .Where(kvp => kvp.Value.Room == room)
                    .OrderBy(kvp => kvp.Value.ConnectedAt)
                    .ToList();

                // सबसे recent R को active रखो
                var activeConnections = roomConnections.Skip(Math.Max(0, roomConnections.Count - rateLimit)).ToList();
                var inactiveConnections = roomConnections.Take(Math.Max(0, roomConnections.Count - rateLimit)).ToList();

                // Active users को role दो
                for (int i = 0; i < activeConnections.Count; i++)
                {
                    var connId = activeConnections[i].Key;
                    bool isNews = i < newsLimit; // पहले N को news
                    bool isRate = true;

                    var dto = new
                    {
                        clientDetails.Id,
                        Username = clientDetails.Username ?? room,
                        AccessNoOfNews = newsLimit,
                        AccessNoOfRate = rateLimit,
                        ClientName = room,
                        DeviceToken = clientDetails.DeviceToken ?? string.Empty,
                        IsActive = true,
                        IsNews = isNews,
                        IsRate = isRate,
                        clientDetails.NewsExpiredDate,
                        clientDetails.RateExpiredDate,
                        status = true
                    };

                    string json = JsonConvert.SerializeObject(dto);
                    var compressed = Compress(json);

                    
                    await Clients.Client(connId).SendAsync("clientDetails", new { status = true, data = compressed });
                }

               
                foreach (var kvp in inactiveConnections)
                {
                    await Clients.Client(kvp.Key).SendAsync("clientDetails", new { status = false });
                }

                // Instrument data (unchanged)
                var enrichedData = await GetSymbolRatesByClientIdAsync(room);
                if (enrichedData.IsSuccess)
                {
                    var instrumentPayload = System.Text.Json.JsonSerializer.Serialize(enrichedData.Data);
                    var compressed = Compress(instrumentPayload);
                    await Clients.Caller.SendAsync("userInstrument", new { data = compressed });
                }
                else
                {
                    await Clients.Caller.SendAsync("userInstrument", new { data = Array.Empty<byte>() });
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
                var userInstrumentKey = UserInstrumentKeyPrefix + username;
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
