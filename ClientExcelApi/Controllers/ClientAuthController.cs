using CommonDatabase;
using CommonDatabase.DTO;
using CommonDatabase.Interfaces;
using CommonDatabase.Models;
using CommonDatabase.Services;
using DashboardExcelApi;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Reuters.Repositories;
using System.IdentityModel.Tokens.Jwt;
using System.IO.Compression;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using static ClientExcelApi.Controllers.ClientAuthController;

namespace ClientExcelApi.Controllers
{
    [Authorize]
    public class ClientAuthController : Controller
    {
        private readonly IAuthService _authService;
        private readonly IInstrumentsService _instrumentsService;
        private readonly IClientService _clientService;
        private readonly IJwtBlacklistService _jwtBlacklistService;
        private readonly AppDbContext _context;
        private readonly ReutersService _reutersService;
        private readonly ApplicationConstant _constatnt;
        private readonly IHubContext<ExcelHub> _hubContext;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;


        public byte[] Compress(string json)
        {
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionMode.Compress))
            using (var writer = new StreamWriter(gzip)) writer.Write(json);
            return output.ToArray();
        }

        public ClientAuthController(IAuthService authService, IInstrumentsService instrumentsService, IClientService clientService, ApplicationConstant constatnt, IHttpClientFactory httpClientFactory, IHubContext<ExcelHub> hubContext, AppDbContext context, ReutersService reutersService, IJwtBlacklistService JwtService, IConfiguration configuration)
        {
            _hubContext = hubContext; _authService = authService; _instrumentsService = instrumentsService; _clientService = clientService; _constatnt = constatnt; _httpClientFactory = httpClientFactory;
            _reutersService = reutersService; _jwtBlacklistService = JwtService;
            _context = context;
            _configuration = configuration;
        }
        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] ClientAuth model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (string.IsNullOrWhiteSpace(model.Username) || string.IsNullOrWhiteSpace(model.Password))
                return BadRequest(ApiResponse.Fail("Username or password cannot be empty."));

            if (!_configuration.GetSection("deviceType").GetChildren().Select(x => x.Value).Contains(model.DeviceType))
            { 
                return BadRequest(ApiResponse.Fail("Invalid device type."));
            }
            var result = await _authService.ValidateClientLogin(model);
            //return result.IsSuccess ? Ok(result) : Unauthorized(result);
            return Ok(result);
        }

        [HttpPost("update-topic-keyword")]
        public async Task<IActionResult> UpdateTopicKeyword([FromBody] TopicKeyword model)
        {
            if (string.IsNullOrWhiteSpace(model.UserId.ToString()) || model.UserId <= 0)
                return BadRequest(ApiResponse.Fail("UserId is missing."));

            var result = await _authService.UpdateTopicKeyword(model);
            return result.IsSuccess ? Ok(result) : Unauthorized(result);
        }

        [HttpPost("update-status-dnd")]
        public async Task<IActionResult> UpdateStatusDND([FromBody] StatusDnd model)
        {
            if (string.IsNullOrWhiteSpace(model.UserId.ToString())|| model.UserId <= 0)
                return BadRequest(ApiResponse.Fail("UserId is missing."));

            if (string.IsNullOrWhiteSpace(model.DeviceId.ToString()))
                return BadRequest(ApiResponse.Fail("DeviceId is missing."));

            var result = await _authService.UpdateStatusDnd(model);
            return result.IsSuccess ? Ok(result) : Unauthorized(result);
        }

        [Authorize(Roles = "Client")]
        [HttpPost("AlertNotification")]
        public async Task<IActionResult> SendNotification([FromBody] NotificationAlert input)
        {
            var clientIdClaim = User.FindFirst("Id")?.Value;
            if (string.IsNullOrEmpty(clientIdClaim) || !int.TryParse(clientIdClaim, out int clientId) || clientId <= 0)
            {
                return BadRequest(ApiResponse.Fail("Invalid or missing ClientId in token."));
            }
            var typeValue = input.Type?.Trim();
            if (typeValue == "0") input.Type = "Bid";
            else if (typeValue == "1") input.Type = "Ask";
            else if (typeValue == "2") input.Type = "Ltp";
            else return BadRequest(ApiResponse.Fail("Type must be 0, 1, or 2."));
            input.ClientId = clientId;
            var result = await _clientService.CreateAndSendAlert(input);
            return Ok(result);
        }

        [Authorize(Roles = "Client")]
        [HttpGet("GetNotifications")]
        public async Task<IActionResult> GetNotifications()
        {
            var clientIdClaim = User.FindFirst("Id")?.Value;

            if (string.IsNullOrEmpty(clientIdClaim) || !int.TryParse(clientIdClaim, out int clientId) || clientId <= 0)
            {
                return BadRequest(ApiResponse.Fail("Invalid or missing ClientId in token."));
            }

            var result = await _clientService.GetNotificationsAsync(clientId);
            return Ok(result);
        }

        [HttpPost("ratePassed")]
        public async Task<IActionResult> MarkAlertPassed([FromBody] MarkPassedInput input)
        {
            if (input == null || input.ClientId <= 0 || string.IsNullOrWhiteSpace(input.Symbol))
            {
                return BadRequest(ApiResponse.Fail("Invalid input."));
            }
            input.Type = input.Type?.Trim().ToLower() switch { "bid" => "0", "ask" => "1", "ltp" => "2", _ => input.Type };
            var result = await _clientService.MarkRateAlertPassedAsync(input.ClientId, input.Symbol, input.Id);
            if (result.IsSuccess)
            {
                var payload = new
                {
                    Username = input.Username,
                    Data = new
                    {
                        input.ClientId,
                        input.Symbol,
                        input.Id,
                        input.Type,
                        input.Condition,
                        input.Flag,
                        input.Rate
                    }
                };
                var compressed = Compress(JsonSerializer.Serialize(payload));
                await _hubContext.Clients.User(payload.Username).SendAsync("rateAlertNotification", compressed);
            }
            return Ok(result);
        }

        [Authorize(Roles = "Client")]
        [HttpGet("getInstrument")]
        public async Task<IActionResult> GetInstrumentRate()
        {
            var username = User.FindFirst("userName")?.Value;
            var clientIdStr = User.FindFirst("Id")?.Value;
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(clientIdStr) || !int.TryParse(clientIdStr, out var clientId))
                return BadRequest(ApiResponse.Fail("Invalid token claims."));

            var result = await _constatnt.GetSymbolRatesByClientIdAsync(username, clientId);
            return Ok(result);
        }

        [Authorize(Roles = "Client")]
        [HttpPost("AddWatch")]
        public async Task<IActionResult> WatchInstrument([FromBody] WatchInstrument watchInstrument)
        {

            var clientIdStr = User.FindFirst("Id")?.Value;
            if (string.IsNullOrEmpty(clientIdStr) || !int.TryParse(clientIdStr, out var clientId))
                return BadRequest(ApiResponse.Fail("Invalid token claims."));
            var result = await _clientService.AddWatchInstrumentAsync(watchInstrument, clientId);
            return Ok(result);
        }

        [Authorize(Roles = "Client")]
        [HttpGet("GetWatchList")]
        public async Task<IActionResult> GetWatchInstrument()
        {
            var clientIdStr = User.FindFirst("Id")?.Value;
            if (string.IsNullOrEmpty(clientIdStr) || !int.TryParse(clientIdStr, out var clientId))
                return BadRequest(ApiResponse.Fail("Invalid token claims."));

            var result = await _clientService.GetWatchInstrumentAsync(clientId);
            return Ok(result);
        }

        [Authorize(Roles = "Client")]
        [HttpGet("setup")]
        public IActionResult DownloadSetup()
        {
            var filePath = @"C:\Applications\DowloadFile\thecalcify.zip";

            if (!System.IO.File.Exists(filePath))
                return NotFound("File not found.");

            var bytes = System.IO.File.ReadAllBytes(filePath);
            return File(bytes, "application/zip", "thecalcify.zip");
        }

        [AllowAnonymous]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] LogoutRequest model)
        {
            if (string.IsNullOrWhiteSpace(model.UserId.ToString()))
                return BadRequest(ApiResponse.Fail("UserId is required."));
            if (string.IsNullOrWhiteSpace(model.DeviceId))
                return BadRequest(ApiResponse.Fail("DeviceId is required."));
           
            //var principal = ValidateJwtToken(model.Token);
            //if (principal == null)
            //    return Unauthorized(ApiResponse.Fail("Invalid or expired token."));

            //await _jwtBlacklistService.AddToBlacklistAsync(model.Token);
            var result = await _authService.ClientLogout(model);
            return Ok(result);
        }


        //public class LogoutRequest
        //{
        //    public string Token { get; set; }
        //}


        private ClaimsPrincipal? ValidateJwtToken(string token)
        {
            try
            {
                var tokenHandler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();       
                var jwtSettings = _configuration.GetSection("Jwt");
                var key = jwtSettings["Key"];
                if (string.IsNullOrEmpty(key)) return null;
                var keyBytes = Encoding.UTF8.GetBytes(key);

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ClockSkew = TimeSpan.Zero
                };
                          
                var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);             
                if (!(validatedToken is JwtSecurityToken jwtToken) ||!jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                    return null;

                return principal;
            }
            catch
            {
                return null; 
            }
        }



    }
}