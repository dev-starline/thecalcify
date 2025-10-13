using CommonDatabase.DTO;
using CommonDatabase.Interfaces;
using CommonDatabase.Models;
using FirebaseAdmin.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using StackExchange.Redis;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace DashboardExcelApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PublishController : ControllerBase
    {
        private readonly ICommonService _commonService;
        private readonly IConnectionMultiplexer _redis;
        private readonly IDatabase _redisDb;
        private readonly IHubContext<ExcelHub> _hubContext;
        private const string UserDetailsKey = "UserDetails";
        public readonly ConnectionStore _connectionStore;
        public PublishController(IHubContext<ExcelHub> hubContext, ICommonService commonService, IConnectionMultiplexer redis, ConnectionStore connectionStore)
        {
            _commonService = commonService;
            _redis = redis;
            _hubContext = hubContext;
            _redisDb = redis.GetDatabase();
            _connectionStore = connectionStore;
        }

        [HttpGet("PublishExcelData")]
        public async Task<IActionResult> PublishExcelData(string Username)
        {
            var userDetailsRaw = await _redisDb.StringGetAsync(UserDetailsKey);


            var userList = JsonConvert.DeserializeObject<List<ClientDto>>(userDetailsRaw!);
            //await _hubContext.Clients.All.SendAsync("ReceiveMessage", userList);
            //await _hubContext.Clients.All.SendAsync("ReceiveMessage", userList);
            //foreach (var item in userList)
            //{
            //    var getAllConnect = _connectionStore.GetAllConnectionIds();
            await _hubContext.Clients.All.SendAsync(
                   "ReceiveAllClient",
                   new
                   {
                       status = true,
                       data = userList.Select(x => new { x.Id, x.Username })
                   }
               );
            var connId = _connectionStore.GetConnectionId(Username);

            if (connId != null)
            {
                //await _hubContext.Clients.Client(connId).SendAsync(
                //    "ReceiveMessage", 
                //    new { 
                //        status = userList.Exists(x => x.Username == Username), 
                //        data = userList.Where(x => x.Username == Username) 
                //    }
                //);
                await _hubContext.Clients.Group(Username).SendAsync(
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
                if (item.IsActive)
                {
                    await _hubContext.Clients.Group(item.Username).SendAsync(
                        "ReceiveNewsNotification",
                        receiveNewsDto.NewsList
                    );
                }
            }
            return Ok();
        }

        [AllowAnonymous]
        [HttpGet("ActiveUsers/{username?}")]
        public async Task<IActionResult> ActiveUsers([FromRoute] string? username)
        {

            var userDetailsRaw = await _redisDb.StringGetAsync(UserDetailsKey);
            var userList = JsonConvert.DeserializeObject<List<ClientDto>>(userDetailsRaw!);
            if (username != null)
            {
                return Ok(userList.Where(x=> x.Username == username).FirstOrDefault());
            }
            return Ok(userList);
        }
    }
}
