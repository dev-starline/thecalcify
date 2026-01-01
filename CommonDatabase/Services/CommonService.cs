using CommonDatabase.DTO;
using CommonDatabase.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CommonDatabase.Services
{
    public class CommonService : ICommonService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IConnectionMultiplexer _redis;
        private const string UserDetailsKey = "UserDetails";
        private readonly HttpClient _httpClient;
        private readonly string prefix = "";
        public CommonService(AppDbContext context, IConfiguration configuration, IConnectionMultiplexer redis, IHttpClientFactory factory)
        {
            _context = context;
            _configuration = configuration;
            _redis = redis;
            _httpClient = factory.CreateClient("MyApi");
            prefix = _configuration["Redis:Prefix"];

        }
        public async Task GetDeviceAccessSummaryAsync(int ClientId, string Username)
        {
            try
            {
                IDatabase db = _redis.GetDatabase();
                //var result = new List<DeviceAccessDto>();

                var rawUserResults = await _context.ClientAccessModel
                .FromSqlRaw(
                    "EXEC dbo.usp_UserDetails"
                )
                .ToListAsync();


                var userResults = rawUserResults.Select(r => new ClientAccessModel
                {
                    Id = r.Id,
                    AccessNoOfNews = r.AccessNoOfNews,
                    AccessNoOfRate = r.AccessNoOfRate,
                    ClientName = r.ClientName,
                    DeviceToken = r.DeviceToken,
                    IsActive = r.IsActive,
                    IsNews = r.IsNews,
                    IsRate = r.IsRate,
                    NewsExpiredDate = r.NewsExpiredDate,
                    RateExpiredDate = r.RateExpiredDate,
                    Username = r.Username,
                    Keywords = r.Keywords,
                    Topics = r.Topics
                }).ToList();

                var rawResults = await _context.DeviceAccessRawDto
                   .FromSqlRaw(
                       "EXEC dbo.usp_ClientDevices_GetDevices"
                   )
                   .ToListAsync();

                //string Username = userResults.FirstOrDefault(x => x.Id == ClientId)?.Username ?? "";
                var clientDto = userResults.Select(x => new ClientDto()
                {
                    Id = x.Id,
                    Username = x.Username,
                    IsActive = x.IsActive,
                    NewsExpireDate = x.NewsExpiredDate,
                    RateExpireDate = x.RateExpiredDate,
                    Topics = x.Topics,
                    Keywords = x.Keywords,
                    DeviceAccess = rawResults
                    .Where(r => r.ClientId == x.Id)
                    .Select(
                        r => new DeviceAccessDto()
                        {
                            DeviceToken = r.DeviceToken,
                            DeviceType = r.DeviceType,
                            DeviceId = r.DeviceId,
                            IsDND = r.IsDND,
                            HasNewsAccess = !x.IsNews? x.IsNews : r.HasNewsAccess,
                            HasRateAccess = !x.IsRate ? x.IsRate : r.HasRateAccess
                        }
                    ).ToList()
                }).ToList();
                var userDetailsRaw = await db.StringSetAsync($"{prefix}_{UserDetailsKey}", JsonSerializer.Serialize(clientDto));
                //}
                await _httpClient.GetAsync($"api/Publish/PublishExcelData?Username={Username}");

                //return result;
            }
            catch (Exception ex)
            {
                //Log.Error(ex, "Failed to fetch device access summary for ClientId {ClientId}", ClientId);
                //throw ex;
            }
        }
        public async Task GetUserListOfSymbolAsync(int ClientId, string Username)
        {
            try
            {
                await _httpClient.GetAsync($"api/Publish/PublishUserListOfSymbol/{Username}");
            }
            catch (Exception ex)
            {
                //Log.Error(ex, "Failed to fetch device access summary for ClientId {ClientId}", ClientId);
                //throw ex;
            }
        }
    }
}
