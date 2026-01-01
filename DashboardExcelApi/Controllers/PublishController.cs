using CommonDatabase;
using CommonDatabase.DTO;
using CommonDatabase.Interfaces;
using CommonDatabase.Models;
using FirebaseAdmin.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using StackExchange.Redis;
using System.IO.Compression;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace DashboardExcelApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PublishController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ICommonService _commonService;
        private readonly IConnectionMultiplexer _redis;
        private readonly IDatabase _redisDb;
        private readonly IHubContext<ExcelHub> _hubContext;
        private const string UserDetailsKey = "UserDetails";
        public readonly ConnectionStore _connectionStore;
        private readonly string prefix = "";
        private readonly AppDbContext _context;
        public PublishController(IHubContext<ExcelHub> hubContext, ICommonService commonService, IConnectionMultiplexer redis, ConnectionStore connectionStore, IConfiguration configuration, AppDbContext context)
        {
            _commonService = commonService;
            _redis = redis;
            _hubContext = hubContext;
            _redisDb = redis.GetDatabase();
            _connectionStore = connectionStore;
            _configuration = configuration;
            prefix = _configuration["Redis:Prefix"];
            _context = context;
        }

        [HttpGet("PublishExcelData")]
        public async Task<IActionResult> PublishExcelData(string Username)
        {
            var groupName = GroupNameResolver.Resolve(Username);
            var userDetailsRaw = await _redisDb.StringGetAsync($"{prefix}_{UserDetailsKey}");


            var userList = JsonConvert.DeserializeObject<List<ClientDto>>(userDetailsRaw!);
            //await _hubContext.Clients.All.SendAsync("ReceiveMessage", userList);
            //await _hubContext.Clients.All.SendAsync("ReceiveMessage", userList);
            //foreach (var item in userList)
            //{
            //    var getAllConnect = _connectionStore.GetAllConnectionIds();
            var groupNameAll = GroupNameResolver.Resolve("CalcifyAllClient");
            await _hubContext.Clients.Group(groupNameAll).SendAsync(
                   "ReceiveAllClient",
                   new
                   {
                       status = true,
                       data = userList.Select(x => new { x.Id, x.Username })
                   }
               );
            var connId = _connectionStore.GetConnectionId(groupName);

            if (connId != null)
            {
                //await _hubContext.Clients.Client(connId).SendAsync(
                //    "ReceiveMessage", 
                //    new { 
                //        status = userList.Exists(x => x.Username == Username), 
                //        data = userList.Where(x => x.Username == Username) 
                //    }
                //);
                await _hubContext.Clients.Group(groupName).SendAsync(
                    "ReceiveMessage",
                    new
                    {
                        status = userList.Exists(x => x.Username == Username),
                        data = userList.Where(x => x.Username == Username)
                    }
                );

            }
            //}


            return Ok();
        }
        [AllowAnonymous]
        [HttpPost("ReceiveNews")]
        public async Task<IActionResult> ReceiveNews([FromBody] ReceiveNewsDto receiveNewsDto)
        {

            foreach (var item in receiveNewsDto.ClientList)
            {
                var groupName = GroupNameResolver.Resolve(item.Username);
                //if (item.DeviceId is not null)
                //{
                //    await _hubContext.Clients.Group($"{item.Username}_{item.DeviceId}").SendAsync(
                //        "ReceiveNewsNotification",
                //        receiveNewsDto.NewsList
                //    );
                //}
                //else
                //{
                await _hubContext.Clients.Group(groupName).SendAsync(
                       "ReceiveNewsNotification",
                       receiveNewsDto.NewsList
                   );
                //}
            }
            return Ok();
        }

        [AllowAnonymous]
        [HttpGet("ActiveUsers/{username?}")]
        public async Task<IActionResult> ActiveUsers([FromRoute] string? username)
        {

            var userDetailsRaw = await _redisDb.StringGetAsync($"{prefix}_{UserDetailsKey}");
            var userList = JsonConvert.DeserializeObject<List<ClientDto>>(userDetailsRaw!);
            if (username != null)
            {
                return Ok(userList.Where(x=> x.Username == username).FirstOrDefault());
            }
            return Ok(userList);
        }
        [HttpGet("publish-subscriber")]
        public async Task<IActionResult> PublishSubscriber(string symbol, string symboljson)
        {
            //var groupName = GroupNameResolver.Resolve(symbol);
            await _hubContext.Clients.Group(symbol).SendAsync("excelRate", Compress(symboljson.ToString()));

            return Ok();
        }

        [AllowAnonymous]
        [HttpGet("PublishUserListOfSymbol/{username?}")]
        public async Task<IActionResult> PublishUserListOfSymbol([FromRoute] string? username)
        {
            var groupName = GroupNameResolver.Resolve(username);
            int ClientId = await _context.Client.Where(x => x.Username == username).Select(x => x.Id).FirstOrDefaultAsync();
            //var identifiers = await _context.Instruments
            //            .Where(a => a.ClientId == ClientId && a.IsMapped)
            //            .Select(a => new { i = a.Identifier, n = a.Contract })
            //            .ToListAsync();

            ////string listOfSymbol = string.Join(",", identifiers);
            //await _hubContext.Clients.Group(username).SendAsync(
            //        "UserListOfSymbol", identifiers
            //    );
            var rawUserResults = await _context.ClientWiseInstrumentList
                .FromSqlInterpolated($"EXEC dbo.usp_ClientWiseInstumentList {ClientId}")
                .ToListAsync();


            var identifiers = rawUserResults.OrderBy(x => x.RowId).Select(r => new { i = r.Identifier, n = r.Contract }).ToList();
            await _hubContext.Clients.Group(groupName).SendAsync("UserListOfSymbol", identifiers);
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
