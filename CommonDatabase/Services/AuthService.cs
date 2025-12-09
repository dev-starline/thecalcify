using Azure.Core;
using CommonDatabase.DTO;
using CommonDatabase.Interfaces;
using CommonDatabase.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;
using StackExchange.Redis;
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace CommonDatabase.Services
{
    public class AuthService : IAuthService
    {
        public enum UserRole
        {
            Admin,Client
        }
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IConnectionMultiplexer _redis;
        private readonly ICommonService _commonService;
        // Redis key templates (centralised for easier change)
        private const string ClientDetailsKey = "UserDetails";
        public AuthService(AppDbContext context, IConfiguration configuration, IConnectionMultiplexer redis, ICommonService commonService)
        {
            _context = context;
            _configuration = configuration;
            _redis = redis;
            _commonService = commonService;
        }

        public async Task<ApiResponse> LoginAsync(AdminLogin login)
        {
            var admin = await _context.AdminLogin.FirstOrDefaultAsync(a => a.Username == login.Username);

            if (admin == null || admin.Password != login.Password)
                return ApiResponse.Fail("Invalid AdminId or Password. Please try again with valid UserName & Password.");

            var token = GenerateJwtToken(admin.Id, admin.Username, UserRole.Admin);
            return ApiResponse.Ok(new { Token = token }, "Login successful.");
        }

        public async Task<ApiResponse> ValidateClientLogin(ClientAuth login)
        {
            //var user = await _context.Client.FirstOrDefaultAsync(u => u.Username == login.Username);
            var user = await _context.Client.FromSqlInterpolated($@"
                                SELECT * FROM Client 
                                WHERE Username = {login.Username} COLLATE Latin1_General_CS_AS")
                            .FirstOrDefaultAsync();

            var expiryCutoff = DateTime.UtcNow.AddDays(-365);

            if (user == null || user.Password != login.Password)
                return ApiResponse.Fail("Invalid credentials");

            if (!user.IsActive)
            {
                return ApiResponse.Fail("User is not active");
            }

            if (!string.IsNullOrWhiteSpace(login.DeviceToken))
            {
                var device = _context.ClientDevices
                            .FirstOrDefault(x => 
                                x.ClientId == user.Id &&
                                x.DeviceToken == login.DeviceToken && 
                                x.DeviceType == login.DeviceType && 
                                x.DeviceId == login.DeviceId
                             );
                var clientDevices = new ClientDevices();
                if (device == null)
                {
                    clientDevices.ClientId = user.Id;
                    clientDevices.DeviceId = login.DeviceId;
                    clientDevices.DeviceToken = login.DeviceToken;
                    clientDevices.DeviceType = login.DeviceType;
                    clientDevices.CreatedDate = DateTime.Now;
                    clientDevices.UpdatedDate = DateTime.Now;
                    clientDevices.LastLogin = DateTime.Now;
                    clientDevices.IsActive = true;
                    clientDevices.IsDND = false;
                    _context.ClientDevices.AddAsync(clientDevices);
                }
                else
                {
                    device.LastLogin = device.UpdatedDate;
                    device.UpdatedDate = DateTime.Now;
                    device.IsLogout = false;
                    _context.Attach(device);
                    _context.Entry(device).State = EntityState.Modified;
                }
                await _context.SaveChangesAsync();
            }
            
            //await GetDeviceAccessSummaryAsync(user.Id);
            Task.Run(async () => await _commonService.GetDeviceAccessSummaryAsync(user.Id, user.Username)).Wait();


            var token = GenerateJwtToken(user.Id, user.Username, UserRole.Client, login.DeviceId, login.DeviceType, user.IsNews,user.IsRate, login.DeviceToken, user.RateExpiredDate,user.NewsExpiredDate);
            return ApiResponse.Ok(new
            {
                Token = token,DeviceToken = login.DeviceToken,
                IsNews = user.IsNews,
                expireTime = user.RateExpiredDate ?? new DateTime(2025, 10, 20, 0, 0, 0),
            }, "Login successful");
        }



        private string GenerateJwtToken(int id, string userName, UserRole role, string deviceId = null, string deviceType = null, bool isNews = false,
            bool isRate = false, string deviceToken = null, DateTime? rateExpiredDate = null,
            DateTime? newsExpiredDate = null)
        {
            var jwtSettings = _configuration.GetSection("Jwt");
            var key = jwtSettings["Key"];
            var issuer = jwtSettings["Issuer"];
            var audience = jwtSettings["Audience"];
            var subject = jwtSettings["Subject"];
            var expiresDays = int.TryParse(jwtSettings["Expires"], out var days) ? days : 365;

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, subject ?? userName),
                new Claim("Id", id.ToString()),
                new Claim("userName", userName),
                new Claim(ClaimTypes.Role, role.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            if (role == UserRole.Client)
            {
                claims.Add(new Claim("IsNews", isNews.ToString()));
                claims.Add(new Claim("IsRate", isRate.ToString()));
                if (!string.IsNullOrWhiteSpace(deviceToken))
                    claims.Add(new Claim("DeviceToken", deviceToken));
                if (!string.IsNullOrWhiteSpace(deviceId))
                    claims.Add(new Claim("DeviceId", deviceId));
                if (!string.IsNullOrWhiteSpace(deviceType))
                    claims.Add(new Claim("DeviceType", deviceType));
                if (rateExpiredDate.HasValue)
                    claims.Add(new Claim("RateExpiredDate", rateExpiredDate.Value.ToString("o")));
                if (newsExpiredDate.HasValue)
                    claims.Add(new Claim("NewsExpiredDate", newsExpiredDate.Value.ToString("o")));
                var maxDate = (newsExpiredDate > rateExpiredDate ? newsExpiredDate : rateExpiredDate);
                TimeSpan duration = DateTime.Parse(maxDate.ToString()) - DateTime.Now;
                int totalDays = duration.Days;
            }

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddDays(expiresDays),
                signingCredentials: credentials
            );
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public async Task<ApiResponse> ClientLogout(LogoutRequest request)
        {
            var device = await _context.ClientDevices.FirstOrDefaultAsync(a => a.ClientId == request.UserId && a.DeviceId == request.DeviceId);
            if (device == null)
            {
                return ApiResponse.Ok("Device not found.");
            }
            var client = await _context.Client.FirstOrDefaultAsync(a => a.Id == request.UserId);
            _context.ClientDevices.Remove(device);
            await _context.SaveChangesAsync();
            Task.Run(async () => await _commonService.GetDeviceAccessSummaryAsync(request.UserId, client.Username)).Wait();
            return ApiResponse.Ok( "Logout successful.");
        }

        public async Task<ApiResponse> UpdateStatusDnd(StatusDnd status)
        {
            var device = await _context.ClientDevices.FirstOrDefaultAsync(a => a.ClientId == status.UserId && a.DeviceId == status.DeviceId);
            if (device == null)
            {
                return ApiResponse.Ok("Device not found.");
            }
            var client = await _context.Client.FirstOrDefaultAsync(a => a.Id == status.UserId);
            //_context.ClientDevices.Remove(device);
            device.UpdatedDate = DateTime.Now;
            device.IsDND = status.IsDND;
            _context.Attach(device);
            _context.Entry(device).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            Task.Run(async () => await _commonService.GetDeviceAccessSummaryAsync(status.UserId, client.Username)).Wait();
            return ApiResponse.Ok("DND updated successful.");
        }

        public async Task<ApiResponse> UpdateTopicKeyword(TopicKeyword status)
        {
            var device = await _context.Client.FirstOrDefaultAsync(a => a.Id == status.UserId);
            if (device == null)
            {
                return ApiResponse.Ok("Device not found.");
            }
            var client = await _context.Client.FirstOrDefaultAsync(a => a.Id == status.UserId);
            //_context.ClientDevices.Remove(device);
            device.UpdateDate = DateTime.Now;
            if (status.IsTopic)
            {
                device.Topics = status.TopicOrKeyword;
            }
            else
            {
                device.Keywords = status.TopicOrKeyword;
            }

            _context.Attach(device);
            _context.Entry(device).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            Task.Run(async () => await _commonService.GetDeviceAccessSummaryAsync(status.UserId, client.Username)).Wait();
            if (status.IsTopic)
            {
                return ApiResponse.Ok("Topics updated successful.");
            }
            else
            {
                return ApiResponse.Ok("Keywords updated successful.");
            }
            
        }
    }
}
