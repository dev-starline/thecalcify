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
        private readonly IDatabase _redisDb;
        private readonly IHubContext<ExcelHub> _hubContext;
        private readonly ILogger<SubscribeRate> _logger;
        private readonly ConcurrentDictionary<string, string> _latestTicks = new();

        public SubscribeRate(IHubContext<ExcelHub> hubContext, IConnectionMultiplexer redis, ILogger<SubscribeRate> logger)
        {
            _subscriber = redis.GetSubscriber();
            _redisDb = redis.GetDatabase();
            _hubContext = hubContext;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await _subscriber.SubscribeAsync("excel", async (channel, message) =>
            {
                try
                {
                    using var doc = JsonDocument.Parse((string)message!);
                    var root = doc.RootElement;
                    var symbol = root.GetProperty("i").GetString();

                    if (!string.IsNullOrEmpty(symbol))
                    {
                        string json = root.ToString();
                        _latestTicks[symbol] = json;

                        // persist latest tick (fire-and-forget if you want speed)
                        _ = _redisDb.StringSetAsync(symbol, json);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing tick");
                }
            });
            // Background loop flushes only latest ticks
            while (!stoppingToken.IsCancellationRequested)
            {
                foreach (var kvp in _latestTicks)
                {
                    var symbol = kvp.Key;
                    var json = kvp.Value;

                    _ = _hubContext.Clients.Group(symbol)
                        .SendAsync("excelBase", json, cancellationToken: stoppingToken);

                    _ = _hubContext.Clients.Group(symbol)
                        .SendAsync("excelRate", Compress(json), cancellationToken: stoppingToken);
                }

                await Task.Delay(200, stoppingToken); // adjust cadence
            }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("SubscribeRate service stopping...");
            return base.StopAsync(cancellationToken);
        }

        private byte[] Compress(string json)
        {
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionMode.Compress))
            using (var writer = new StreamWriter(gzip))
                writer.Write(json);

            return output.ToArray();
        }
    }
}
