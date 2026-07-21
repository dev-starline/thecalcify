using DashboardExcelApi;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text.Json;
using System.Threading.Channels;

namespace DashboardExcelApi
{
    public class SubscribeRate : BackgroundService
    {
        private readonly ISubscriber _subscriber;
        private readonly IDatabase _redisDb;
        private readonly IHubContext<ExcelHub> _hubContext;
        private readonly ILogger<SubscribeRate> _logger;

        private readonly ConcurrentDictionary<string, string> _latestTicks = new();
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

        public SubscribeRate(IHubContext<ExcelHub> hubContext,
                             IConnectionMultiplexer redis,
                             ILogger<SubscribeRate> logger)
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
                    if (string.IsNullOrEmpty(symbol)) return;

                    string json = root.ToString();
                    _latestTicks[symbol] = json;

                    // Fire-and-forget persistence
                    _ = _redisDb.StringSetAsync(symbol, json);

                    // Ensure semaphore exists
                    var sem = _locks.GetOrAdd(symbol, _ => new SemaphoreSlim(1, 1));

                    // Kick off send attempt
                    _ = Task.Run(() => SendLatest(symbol, sem, stoppingToken), stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing tick");
                }
            });
        }

        private async Task SendLatest(string symbol, SemaphoreSlim sem, CancellationToken ct)
        {
            // Only one sender per symbol at a time
            if (!await sem.WaitAsync(0, ct)) return; // someone else is sending

            try
            {
                while (_latestTicks.TryGetValue(symbol, out var latest))
                {
                    try
                    {
                        var compressed = Compress(latest);

                        // Fire-and-forget to avoid blocking ingestion
                        _ = _hubContext.Clients.Group(symbol)
                            .SendAsync("excelBase", latest, ct);

                        _ = _hubContext.Clients.Group(symbol)
                            .SendAsync("excelRate", compressed, ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error sending tick for {Symbol}", symbol);
                    }

                    // If a newer tick arrived during send, loop again
                    string current = latest;
                    if (_latestTicks.TryGetValue(symbol, out var newer) && newer != current)
                        continue;

                    break;
                }
            }
            finally
            {
                sem.Release();
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
