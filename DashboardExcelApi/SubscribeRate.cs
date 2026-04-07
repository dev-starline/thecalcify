using CommonDatabase.Services;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text.Json;

namespace DashboardExcelApi
{
    public class SubscribeRate : BackgroundService
    {

        private readonly ISubscriber _subscriber;
        private readonly IConnectionMultiplexer _redis;
        private readonly IDatabase _redisDb;
        private readonly IHubContext<ExcelHub> _hubContext;
        private readonly ILogger<SubscribeRate> _logger;
        private readonly ConcurrentDictionary<string, string> _latestTicks = new();
        public SubscribeRate(IHubContext<ExcelHub> hubContext, IConnectionMultiplexer redis, ILogger<SubscribeRate> logger)
        {
            _subscriber = redis.GetSubscriber();
            _hubContext = hubContext;
            _redis = redis;
            _redisDb = redis.GetDatabase();
            _logger = logger;
        }

        private void ProcessMessage(string message)
        {
            try
            {
                using var doc = JsonDocument.Parse(message);
                var root = doc.RootElement;
                if (root.TryGetProperty("i", out JsonElement symbolElement))
                {
                    var symbol = symbolElement.GetString();
                    if (!string.IsNullOrEmpty(symbol))
                    {
                        _latestTicks[symbol] = root.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("excelRate: " + ex.Message);
            }
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _subscriber.Subscribe("excel", (channel, message) =>
            {
                ProcessMessage((string)message!);
            });
            while (!stoppingToken.IsCancellationRequested)
            {
                var snapshot = _latestTicks.ToArray();
                var tasks = snapshot.Select(kv =>
                    _hubContext.Clients.Group(kv.Key)
                        .SendAsync("excelBase", kv.Value, cancellationToken: stoppingToken)

                );

                var tasks2 = snapshot.Select(kv =>
                   _hubContext.Clients.Group(kv.Key)
                       .SendAsync("excelRate", Compress(kv.Value), cancellationToken: stoppingToken)
               );

                snapshot.Select(kv =>
                   _redisDb.StringSetAsync(kv.Key, kv.Value)
                );
                await Task.WhenAll(tasks.Concat(tasks2));
                
                await Task.Delay(200, stoppingToken);
            }
        }
        public override Task StopAsync(CancellationToken cancellationToken)
        {

            return base.StopAsync(cancellationToken);

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

