using CommonDatabase;
using CommonDatabase.DTO;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Text.Json;

namespace DashboardExcelApi
{
    public interface IExcelDispatcher
    {
        Task PushUpdateAsync(string userId, string message);
    }
    public class ExcelDispatcher: IExcelDispatcher
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IConnectionMultiplexer _redis;
        //private readonly IExcelDispatcher _excelDispatcher;
        private const string ClientDetailsKey = "UserDetails";
        private readonly IHubContext<ExcelHub> _hubContext;

        public ExcelDispatcher(AppDbContext context, IConfiguration configuration, IConnectionMultiplexer redis, IHubContext<ExcelHub> hubContext)
        {
            _context = context;
            _configuration = configuration;
            _redis = redis;
            _hubContext = hubContext;
        }

        public async Task PushUpdateAsync(string userId, string message)
        {
            await _hubContext.Clients.User(userId).SendAsync("ReceiveUpdate", message);
        }
        public async Task GetDeviceAccessSummaryAsync(int ClientId)
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
                            HasNewsAccess = r.HasNewsAccess,
                            HasRateAccess = r.HasRateAccess
                        }
                    ).ToList()
                }).ToList();
                var userDetailsRaw = await db.StringSetAsync(ClientDetailsKey, JsonSerializer.Serialize(clientDto));
                //}
                await _hubContext.Clients.All.SendAsync("ReceiveMessage", JsonSerializer.Serialize(clientDto));

                //return result;
            }
            catch (Exception ex)
            {
                //Log.Error(ex, "Failed to fetch device access summary for ClientId {ClientId}", ClientId);
                //throw ex;
            }
        }
    }
}
