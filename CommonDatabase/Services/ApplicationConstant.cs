using CommonDatabase.DTO;
using CommonDatabase.Models;
using CommonDatabase.Utility;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StackExchange.Redis;
using System;
using System.Data;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CommonDatabase.Services
{
    public class ApplicationConstant
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IConnectionMultiplexer _redis; // Use interface for DI
        private readonly IDatabase _redisDb;
        private readonly HttpClient _httpClient;
        private readonly string _adminNodeUrl;
        private readonly string _rateAlertNodeUrl;
        private const string UserInstrumentKeyPrefix = "userInstrument:";
        public ApplicationConstant(AppDbContext context, IConfiguration configuration, IConnectionMultiplexer redis)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
            _adminNodeUrl = _configuration["adminNodeUrl"] ?? throw new ArgumentNullException("adminNodeUrl config is missing");
            _rateAlertNodeUrl = _configuration["rateAlertNodeUrl"] ?? throw new ArgumentNullException("rateAlertNodeUrl config is missing");
            _redisDb = _redis.GetDatabase();
            _httpClient = new HttpClient();
        }

        /// <summary>
        /// Sets a string value in Redis by key.
        /// </summary>
        private async Task SetValueInRedisAsync(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key must not be empty.", nameof(key));

            await _redisDb.StringSetAsync(key, value);
        }

        public async Task SetSelfSubscriberToRedis(SelfSubscribe s)
        {
            var json = ConvertSelfSubscriberToRedisJson(s);
            await _redisDb.StringSetAsync(s.Identifier, json);
        }

        public async Task RemoveSelfSubscriberFromRedis(SelfSubscribe subscriber)
        {
            await _redisDb.KeyDeleteAsync(subscriber.Identifier);
        }

        private bool IsEmptyJsonObject(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return true;

            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    return !doc.RootElement.EnumerateObject().Any();

                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    return !doc.RootElement.EnumerateArray().Any();

                return true;
            }
            catch
            {
                return true;
            }
        }



        public class alertBody
        {
            public string? user { get; set; }
            public string? title { get; set; }
            public object? message { get; set; }
            public string? bit { get; set; }
        }

        public async Task SetRateAlertToRedis(int clientId)
        {
            try
            {
                using (var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                {
                    await connection.OpenAsync();

                    using (var command = new SqlCommand("GetRateAlertsByClient", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.CommandTimeout = 5;
                        command.Parameters.AddWithValue("@ClientId", clientId);
                        using (var adapter = new SqlDataAdapter(command))
                        {
                            var ds = new DataSet();
                            adapter.Fill(ds);

                            if (ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                            {

                                var strRateAlerts = DatasetConverter.DatasetToJson(ds);

                                var values = new Dictionary<string, string> { { "data", strRateAlerts } };

                                using (var client = new HttpClient())
                                {
                                    var content = new FormUrlEncodedContent(values);
                                    var response = await client.PostAsync(_rateAlertNodeUrl + "/setRateAlert", content);
                                    response.EnsureSuccessStatusCode();
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Rate Alert Redis Push Failed: {ex.Message}");
            }
        }

        internal async Task SetIdentifireRedisAsync()
        {
            string? jsonString;

            using (var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                await connection.OpenAsync();

                using (var command = new SqlCommand("usp_GetUserIdentifierMap", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            jsonString = reader["UserIdentifierMap"]?.ToString();
                        }
                        else
                        {
                            jsonString = null;
                        }
                    }
                }

                if (IsEmptyJsonObject(jsonString))
                {
                    await _redisDb.KeyDeleteAsync("userInstrument");
                }
                else
                {
                    await SetValueInRedisAsync("userInstrument", jsonString!);
                }
            }


        }

        //internal async Task PushClientDetails()
        //{
        //    string? jsonString = null;

        //    await using var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
        //    await connection.OpenAsync();

        //    await using var command = new SqlCommand("usp_GetUserDetails", connection)
        //    {
        //        CommandType = CommandType.StoredProcedure
        //    };

        //    await using var reader = await command.ExecuteReaderAsync();
        //    if (await reader.ReadAsync())
        //    {
        //        jsonString = reader.GetString(0);
        //    }

        //    if (IsEmptyJsonObject(jsonString))
        //    {
        //        await _redisDb.KeyDeleteAsync("clientDetails");
        //    }
        //    else
        //    {
        //        await SetValueInRedisAsync("clientDetails", jsonString!);
        //    }
        //}

        internal async Task PushClientDetails()
        {
            string jsonString = string.Empty;

            await using var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await connection.OpenAsync();

            await using var command = new SqlCommand("usp_GetUserDetails", connection)
            {
                CommandType = CommandType.StoredProcedure
            };

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                jsonString += reader.GetString(0);  
            }

            if (!string.IsNullOrEmpty(jsonString))
            {
                await SetValueInRedisAsync("clientDetails", jsonString);
            }
        }



        public async Task SetSubscriberToRedis()
        {
            try
            {
                using (var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                {
                    await connection.OpenAsync();

                    using (var command = new SqlCommand("usp_tbl_getSubscribeData", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.CommandTimeout = 5;

                        using (var adapter = new SqlDataAdapter(command))
                        {
                            var ds = new DataSet();
                            adapter.Fill(ds);

                            if (ds.Tables.Count > 3) {
                                var strSubscribe = DatasetConverter.DatasetToJson(ds);


                                var values = new Dictionary<string, string>
                                  {
                                     { "data" , strSubscribe },
                                  };

                                using (var client = new HttpClient())
                                {
                                    var content = new FormUrlEncodedContent(values);
                                    var response = await client.PostAsync(_adminNodeUrl + "/Subscribe", content);
                                    response.EnsureSuccessStatusCode();
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {

                Console.WriteLine($"Redis Push Failed: {ex.Message}");
            }
        }        

        public async Task<object> GetSymbolRatesByClientIdAsync(string username, int clientId)
        {
            var userInstrumentJson = await _redisDb.StringGetAsync("userInstrument");
            if (userInstrumentJson.IsNullOrEmpty)
                return ApiResponse.Fail("No instruments found for this user");

            Dictionary<string, List<string>>? userMap;
            try
            {
                userMap = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(userInstrumentJson!);
            }
            catch
            {
                return ApiResponse.Fail("Invalid instrument data format");
            }

            if (userMap == null || !userMap.ContainsKey(username))
                return ApiResponse.Fail("No instruments found for this user");

            var identifiers = userMap[username];
            var keys = identifiers.Select(id => (RedisKey)id).ToArray();
            var values = await _redisDb.StringGetAsync(keys);
            var dbInstruments = (await _context.Instruments.Where(i => i.ClientId == clientId && identifiers.Contains(i.Identifier) && i.IsMapped == true)
                    .Select(i => new SubscribeInstrumentView { Identifier = i.Identifier, Contract = i.Contract })
                    .ToListAsync()).DistinctBy(i => i.Identifier).ToDictionary(i => i.Identifier, i => i, StringComparer.OrdinalIgnoreCase);

         
            var updatedValues = values.Where(val => !val.IsNullOrEmpty)
                .Select(val =>
                {
                    JObject obj = JObject.Parse(val!);
                    string identifierInRedis = obj["i"]?.ToString() ?? "";

                    if (!string.IsNullOrEmpty(identifierInRedis) &&
                        dbInstruments.TryGetValue(identifierInRedis, out var dbInstrument))
                    {
                        obj["n"] = dbInstrument.Contract; 
                    }
                    return obj.ToObject<Dictionary<string, object>>();
                }).Where(dict => dict != null).ToList();  
            
            var redisIdentifiers = updatedValues.Select(x => x["i"]?.ToString()).Where(x => !string.IsNullOrEmpty(x)).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var missingFromRedis = dbInstruments.Where(kv => !redisIdentifiers.Contains(kv.Key))
                .Select(kv => new Dictionary<string, object>
                {
                    ["n"] = kv.Value.Contract,  ["i"] = kv.Key, ["b"] = "--", ["a"] = "--", ["ltp"] = "--", ["h"] = "--", ["l"] = "--", ["t"] = "N/A", ["o"] = "--", ["c"] = "--",
                    ["d"] = "--",  ["v"] = "--"
                }).ToList();          
            updatedValues.AddRange(missingFromRedis);
            return ApiResponse.Ok(updatedValues);
        }


        private string ConvertSelfSubscriberToRedisJson(SelfSubscribe s)
        {
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                n = s.Name,i = s.Identifier,b = s.Bid,a = s.Ask,ltp = s.Ltp,h = s.High,l = s.Low,t = s.Mdate?.ToString("hh:mm:ss tt"),o = s.Open,c = s.Close,
                d = "--",v = "SELF"
            });
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

                // Expect per-user redis key like "userInstrument:someuser". 
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
                        throw ex; // Log or handle parsing error as needed  
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
                
                return ApiResponse.Fail("Unexpected error retrieving instruments");
            }
        }


    }
}
