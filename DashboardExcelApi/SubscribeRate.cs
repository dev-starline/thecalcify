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

        //private readonly ConcurrentDictionary<string, string> _latestTicks = new();
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

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

                        // ensure per-symbol sequential sending
                        var gate = _locks.GetOrAdd(symbol, _ => new SemaphoreSlim(1, 1));

                        _ = Task.Run(async () =>
                        {
                            await gate.WaitAsync(stoppingToken);
                            try
                            {
                                // snapshot latest tick at send time
                                if (_latestTicks.TryGetValue(symbol, out var latestJson))
                                {
                                    await _hubContext.Clients.Group(symbol)
                                        .SendAsync("excelBase", latestJson, stoppingToken);

                                    await _hubContext.Clients.Group(symbol)
                                        .SendAsync("excelRate", Compress(latestJson), stoppingToken);
                                }
                            }
                            finally
                            {
                                gate.Release();
                            }
                        }, stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing tick");
                }
            });
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
