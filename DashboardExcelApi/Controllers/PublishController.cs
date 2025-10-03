using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using StackExchange.Redis;
using System.Data;
using System.IO.Compression;

namespace DashboardExcelApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PublishController : ControllerBase
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly IDatabase _redisDb;
        private readonly IHubContext<ExcelHub> _hubContext;
        public PublishController(IHubContext<ExcelHub> hubContext, IConnectionMultiplexer redis)
        {
            _redis = redis;
            _hubContext = hubContext;
            _redisDb = redis.GetDatabase();
        }
        [HttpGet("publish-subscriber")]
        public async Task<IActionResult> PublishSubscriber(string symbol, string symboljson)
        {
            await _hubContext.Clients.Group(symbol).SendAsync("excelRate", Compress(symboljson.ToString()));

            return Ok();
        }
        private byte[] Compress(string json)
        {
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionMode.Compress))
            using (var writer = new StreamWriter(gzip)) writer.Write(json);
            return output.ToArray();
        }
    }
}
