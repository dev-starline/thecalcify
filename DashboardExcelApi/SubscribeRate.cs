using CommonDatabase.Services;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;
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

        public SubscribeRate(IHubContext<ExcelHub> hubContext, IConnectionMultiplexer redis, ILogger<SubscribeRate> logger)
        {
            _subscriber = redis.GetSubscriber();
            _redisDb = redis.GetDatabase();
            _hubContext = hubContext;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Subscribe once to the Redis channel
            await _subscriber.SubscribeAsync("excel", async (channel, message) =>
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
                            // 1️⃣ Persist latest tick into Redis
                            await _redisDb.StringSetAsync(symbol, root.ToString());
                            // Broadcast immediately when a new tick arrives
                            await _hubContext.Clients.Group(symbol)
                                .SendAsync("excelBase", root.ToString(), cancellationToken: stoppingToken);

                            await _hubContext.Clients.Group(symbol)
                                .SendAsync("excelRate", Compress(root.ToString()), cancellationToken: stoppingToken);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing tick message");
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
