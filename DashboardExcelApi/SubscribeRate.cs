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
        private readonly IConnectionMultiplexer _redis;
        private readonly IDatabase _redisDb;
        //private readonly Channel<(string group, string message)> _messageQueue = Channel.CreateUnbounded<(string, string)>(new UnboundedChannelOptions
        //{
        //    SingleWriter = false,
        //    SingleReader = true
        //});
        private readonly Channel<(string group, string message)> _messageQueue = Channel.CreateBounded<(string, string)>(new BoundedChannelOptions(1)
        {
            SingleWriter = false,
            SingleReader = true,
            FullMode = BoundedChannelFullMode.DropOldest
        });
        private readonly string[] _instruments = { "GOLD_I", "GOLD_II" };
        public SubscribeRate(IHubContext<ExcelHub> hubContext, IConnectionMultiplexer redis)
        {
            _subscriber = redis.GetSubscriber();
            _hubContext = hubContext;
            _redis = redis;
            _redisDb = redis.GetDatabase();
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _ = Task.Run(async () => await Publish(stoppingToken), stoppingToken);
            _ = Task.Run(async () => await Connection(stoppingToken), stoppingToken);
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        private async Task Publish(CancellationToken token)
        {
            await foreach (var (group, message) in _messageQueue.Reader.ReadAllAsync(token))
            {
                try
                {
                    await _hubContext.Clients.Group(group).SendAsync("excelRate", Compress(message), token);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Send Error] {ex.Message}");
                }
            }
        }



        private async Task Connection(CancellationToken token)
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
                                //await _hubContext.Clients.Group(symbol).SendAsync("excelRate", Compress(root.ToString()));
                                //int sep = message.IndexOf('|');
                                //string group = sep > 0 ? message[..sep] : "UNKNOWN";
                                _messageQueue.Writer.TryWrite((symbol, root.ToString()));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("excelRate: " + ex.Message);
                    }
                });
                //RegisterSignalREvents();
            });
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

