using CommonDatabase.Services;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;
using System.IO.Compression;
using System.Text.Json;
using System.Threading.Channels;

namespace DashboardExcelApi
{
    public class SubscribeRate : BackgroundService
    {

        private readonly IHubContext<ExcelHub> _hubContext;
        private readonly ISubscriber _subscriber;

        public SubscribeRate(IHubContext<ExcelHub> hubContext, IConnectionMultiplexer redis)
        {
            _subscriber = redis.GetSubscriber();
            _hubContext = hubContext;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Publish(stoppingToken);
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        private Task Publish(CancellationToken token)
        {
            _subscriber.Subscribe("excel", async (channel, message) =>
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
                            _ = _hubContext.Clients.Group(symbol).SendAsync("excelRate", Compress(root.ToString()), token);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Send Error] {ex.Message}");
                }
            });

            return Task.CompletedTask;
        }

        public byte[] Compress(string json)
        {
            using
            var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionMode.Compress)) using (var writer = new StreamWriter(gzip)) writer.Write(json);
            return output.ToArray();
        }
    }
}

