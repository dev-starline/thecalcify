using CommonDatabase.DTO;
using CommonDatabase.Interfaces;
using CommonDatabase.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

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

        public AuthService(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
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
            var user = await _context.Client.FirstOrDefaultAsync(u => u.Username == login.Username && u.IsActive);
            var expiryCutoff = DateTime.UtcNow.AddDays(-365);

            if (user == null || user.Password != login.Password)
                return ApiResponse.Fail("Invalid credentials");

            if (!string.IsNullOrWhiteSpace(login.DeviceToken))
            {
                user.DeviceToken = login.DeviceToken;
                _context.Client.Update(user);
                await _context.SaveChangesAsync();
            }
            var token = GenerateJwtToken(user.Id, user.Username, UserRole.Client, user.IsNews,user.IsRate,user.DeviceToken,user.RateExpiredDate,user.NewsExpiredDate);
            return ApiResponse.Ok(new
            {
                Token = token,DeviceToken = user.DeviceToken,IsNews = user.IsNews,
                expireTime = user.RateExpiredDate ?? new DateTime(2025, 10, 20, 0, 0, 0),
            }, "Login successful");
        }


        private string GenerateJwtToken(int id, string userName, UserRole role, bool isNews = false,
            bool isRate = false,string deviceToken = null,DateTime? rateExpiredDate = null,
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
                if (rateExpiredDate.HasValue)
                    claims.Add(new Claim("RateExpiredDate", rateExpiredDate.Value.ToString("o")));
                if (newsExpiredDate.HasValue)
                    claims.Add(new Claim("NewsExpiredDate", newsExpiredDate.Value.ToString("o")));
               
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

    }
}
