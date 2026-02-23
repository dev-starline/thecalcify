using CommonDatabase;
using CommonDatabase.DTO;
using CommonDatabase.Enum;
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
        private readonly IDatabase _redisDb;
        private readonly IHubContext<ExcelHub> _hubContext;
        private const string UserDetailsKey = "UserDetails";
        public readonly ConnectionStore _connectionStore;
        private readonly string prefix = "";
        private readonly AppDbContext _context;
        private readonly HubNotifier _hubNotifier;
        public PublishController(IHubContext<ExcelHub> hubContext, ICommonService commonService, IConnectionMultiplexer redis, ConnectionStore connectionStore, IConfiguration configuration, AppDbContext context, HubNotifier hubNotifier)
        {
            _hubContext = hubContext;
            _redisDb = redis.GetDatabase();
            _connectionStore = connectionStore;
            _configuration = configuration;
            prefix = _configuration["Redis:Prefix"];
            _context = context;
            _hubNotifier = hubNotifier;
        }

        [HttpGet("PublishExcelData")]
        public async Task<IActionResult> PublishExcelData(string Username)
        {
            var groupName = GroupNameResolver.Resolve(Username);
            var userDetailsRaw = await _redisDb.StringGetAsync($"{prefix}_{UserDetailsKey}");
            var userList = JsonConvert.DeserializeObject<List<ClientDto>>(userDetailsRaw!);
            var groupNameAll = GroupNameResolver.Resolve("CalcifyAllClient");

            var ClientList = new
            {
                status = true,
                data = userList.Select(x => new { x.Id, x.Username })
            };
            await _hubNotifier.SendToGroupAsync(groupNameAll, HubMethodName.ReceiveAllClient, ClientList);
            
            var ClientDevices = new
            {
                status = userList.Exists(x => x.Username == Username),
                data = userList.Where(x => x.Username == Username)
            };
            await _hubNotifier.SendToGroupAsync(groupName, HubMethodName.ReceiveMessage, ClientDevices);

            var client = await _context.Client.Where(x => x.Username == Username).FirstOrDefaultAsync();
            if (client != null)
            {
                if (client.Puid != "0")
                {
                    var parentClient = await _context.Client.Where(x => x.Id.ToString() == client.Puid).FirstOrDefaultAsync();
                    var parentGroupName = GroupNameResolver.Resolve(parentClient.Username);
                    
                    var ParentClientDevices = new
                    {
                        status = userList.Exists(x => x.Username == parentClient.Username),
                        data = userList.Where(x => x.Username == parentClient.Username)
                    };
                    await _hubNotifier.SendToGroupAsync(parentGroupName, HubMethodName.ReceiveMessage, ParentClientDevices);
                }
                else
                {
                    var SubClientList = await _context.Client.Where(x => x.Puid == client.Id.ToString()).ToListAsync();
                    foreach (var subClient in SubClientList)
                    {
                        var subGroupName = GroupNameResolver.Resolve(subClient.Username);
                        
                        var SubClientDevices = new
                        {
                            status = userList.Exists(x => x.Username == subClient.Username),
                            data = userList.Where(x => x.Username == subClient.Username)
                        };

                        await _hubNotifier.SendToGroupAsync(subGroupName, HubMethodName.ReceiveMessage, SubClientDevices);
                    }
                }
            }
            return Ok();
        }
        [AllowAnonymous]
        [HttpPost("ReceiveNews")]
        public async Task<IActionResult> ReceiveNews([FromBody] ReceiveNewsDto receiveNewsDto)
        {
            foreach (var item in receiveNewsDto.ClientList)
            {
                var groupName = GroupNameResolver.Resolve(item.Username);
                await _hubNotifier.SendToGroupAsync(groupName, HubMethodName.ReceiveNewsNotification, receiveNewsDto.NewsList);
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
            await _hubNotifier.SendToGroupAsync(symbol, HubMethodName.excelRate, Compress(symboljson.ToString()));
            return Ok();
        }

        [AllowAnonymous]
        [HttpGet("PublishUserListOfSymbol/{username?}")]
        public async Task<IActionResult> PublishUserListOfSymbol([FromRoute] string? username)
        {
            var groupName = GroupNameResolver.Resolve(username);
            int ClientId = await _context.Client.Where(x => x.Username == username).Select(x => x.Id).FirstOrDefaultAsync();

            var rawUserResults = await _context.ClientWiseInstrumentList
                .FromSqlInterpolated($"EXEC dbo.usp_ClientWiseInstumentList {ClientId}")
                .ToListAsync();

            var identifiers = rawUserResults.OrderBy(x => x.RowId).Select(r => new { i = r.Identifier, n = r.Contract, sc = r.SubContract }).ToList();
            await _hubNotifier.SendToGroupAsync(groupName, HubMethodName.UserListOfSymbol, identifiers);
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
