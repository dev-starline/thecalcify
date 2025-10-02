using CommonDatabase.Services;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;
using System.IO.Compression;
using System.Text.Json;

namespace DashboardExcelApi
{
    public class SubscribeRate: BackgroundService
    {
        
        private readonly ISubscriber _subscriber;
         private readonly IConnectionMultiplexer _redis;
        private readonly IDatabase _redisDb;
        private readonly IHubContext<ExcelHub> _hubContext;
        public SubscribeRate(IHubContext<ExcelHub> hubContext, IConnectionMultiplexer redis)
        {
            _subscriber = redis.GetSubscriber();
            _hubContext = hubContext;
            _redis = redis;
            _redisDb = redis.GetDatabase();
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _subscriber.Subscribe("excel", (channel, message) =>
            {
                _ = Task.Run(async () =>
                {
                    try
                    {

                        using var doc = JsonDocument.Parse((string)message!);
                        var root = doc.RootElement;
                        if (root.TryGetProperty("i", out JsonElement symbolElement))
                        {
                            var symbol = symbolElement.GetString();
                            if (!string.IsNullOrEmpty(symbol))
                            {
                                await _redisDb.StringSetAsync(symbol, root.ToString());
                                await _hubContext.Clients.Group(symbol).SendAsync("excelRate", Compress(root.ToString()));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("excelRate: " + ex.Message);
                    }
                });
            });
            return Task.CompletedTask;
        }

        public byte[] Compress(string json)
        {
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionMode.Compress))
            using (var writer = new StreamWriter(gzip)) writer.Write(json);
            return output.ToArray();
        }
    }
}

