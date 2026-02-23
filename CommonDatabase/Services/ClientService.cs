using Azure.Core;
using CommonDatabase.DTO;
using CommonDatabase.Interfaces;
using CommonDatabase.Models;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using StackExchange.Redis;
using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace CommonDatabase.Services {
    public class ClientService : IClientService
    {
        private readonly AppDbContext _context;
        private readonly ApplicationConstant _constant;      
        private readonly IConfiguration _configuration;
        private readonly System.Net.Http.IHttpClientFactory _httpClientFactory;
        private readonly ICommonService _commonService;
        public ClientService(AppDbContext context, ApplicationConstant constant, IConfiguration configuration, System.Net.Http.IHttpClientFactory httpClientFactory, ICommonService commonService)
        {
            _context = context;
            _constant = constant;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _commonService = commonService;
        }

        public async Task<IEnumerable<ClientUser>> GetClientListAsync()
        {
            return await _context.Client.ToListAsync();
        }
        public async Task<IEnumerable<ClientListDto>> GetClientListDtoAsync()
        {
            var Clients = await _context.Client
                        .Select(x => new ClientListDto()
                        {
                            Id = x.Id,
                            Username = x.Username,
                            Password = x.Password,
                            FirmName = x.FirmName,
                            ClientName = x.ClientName,
                            MobileNo = x.MobileNo,
                            City = x.City,
                            IsActive = x.IsActive,
                            UpdateDate = x.UpdateDate,
                            AccessNoOfNews = x.AccessNoOfNews,
                            AccessNoOfRate = x.AccessNoOfRate,
                            IsNews = x.IsNews,
                            IsRate = x.IsRate,
                            NewsExpiredDate = x.NewsExpiredDate,
                            RateExpiredDate = x.RateExpiredDate,
                            Puid = x.Puid,
                            SubClient = new List<ClientListDto>(),
                            SubClientLimit = x.SubClientLimit,
                            PendingSubClient = x.SubClientLimit - (_context.Client.Where(c => c.Puid == x.Id.ToString()).Count())
                        }).ToListAsync();

            var builder = new HierarchyBuilder();
            var hierarchy = builder.BuildHierarchy(Clients);
            return hierarchy;
        }
        public async Task<ApiResponse> AddClientAsync(ClientUser client, string ipAddress, DateTime rateExpiredDate, DateTime newsExpiredDate)
        {
            var duplicate = await _context.Client
                .Where(c => c.Username == client.Username || c.MobileNo == client.MobileNo)
                .Select(c => new { c.Username, c.MobileNo })
                .FirstOrDefaultAsync();

            if (duplicate != null)
            {
                if (duplicate.Username == client.Username)
                    return ApiResponse.Fail("Username already exists.");
                if (duplicate.MobileNo == client.MobileNo)
                    return ApiResponse.Fail("Mobile number already exists.");
            }

            client.IPAddress = ipAddress;
            client.RateExpiredDate = rateExpiredDate;
            client.NewsExpiredDate = newsExpiredDate;

            if (!string.IsNullOrEmpty(client.Puid) && client.Puid != "0")
            {
                var parentClient = await _context.Client.FirstOrDefaultAsync(x => x.Id.ToString() == client.Puid);
                if (parentClient == null)
                    return ApiResponse.Fail("Parent client not found.");

                int parentInstruments = await _context.Instruments
                    .CountAsync(x => x.ClientId == Convert.ToInt32(client.Puid));

                if (parentInstruments <= 0)
                    return ApiResponse.Fail("Add instrument in parent before creating subclient.");

                client.RateExpiredDate = parentClient.RateExpiredDate;
                client.NewsExpiredDate = parentClient.NewsExpiredDate;
            }

            await _context.Client.AddAsync(client);
            await _context.SaveChangesAsync();

            if (!string.IsNullOrEmpty(client.Puid) && client.Puid != "0")
            {
                bool hasSubClientInstruments = await _context.Instruments
                    .AnyAsync(x => x.ClientId == client.Id);

                if (!hasSubClientInstruments)
                {
                    await _context.Database.ExecuteSqlInterpolatedAsync(
                        $@"usp_BulkInsertInstrumentForSubClient {client.Puid}, {client.Id}");
                }
            }

            await _constant.PushClientDetails();
            await _commonService.GetDeviceAccessSummaryAsync(client.Id, client.Username);

            var newData = await GetClientListDtoAsync();
            return client.Puid == "0"
                ? ApiResponse.Ok(newData.Where(x => x.Id == client.Id), "Client added successfully.")
                : ApiResponse.Ok(newData.Where(x => x.Id.ToString() == client.Puid), "Client added successfully.");
        }

        public async Task<ApiResponse> UpdateClientAsync(ClientUser client, string ipAddress, DateTime rateExpiredDate, DateTime newsExpiredDate)
        {
            var existing = await _context.Client.FindAsync(client.Id);
            if (existing == null)
                return ApiResponse.Fail("Client not found.");

            var duplicate = await _context.Client
                .Where(c => (c.Username == client.Username || c.MobileNo == client.MobileNo) && c.Id != client.Id)
                .Select(c => new { c.Username, c.MobileNo })
                .FirstOrDefaultAsync();

            if (duplicate != null)
            {
                if (duplicate.Username == client.Username)
                    return ApiResponse.Fail("Username is already in use by another client.");
                if (duplicate.MobileNo == client.MobileNo)
                    return ApiResponse.Fail("Mobile number is already in use by another client.");
            }

            if (!string.IsNullOrEmpty(client.Puid) && client.Puid != "0")
            {
                var parentClient = await _context.Client.FirstOrDefaultAsync(x => x.Id.ToString() == client.Puid);
                if (parentClient == null)
                    return ApiResponse.Fail("Parent client not found.");

                if (!parentClient.IsRate && client.IsRate)
                    return ApiResponse.Fail("Parent does not have rate access.");
                if (!parentClient.IsNews && client.IsNews)
                    return ApiResponse.Fail("Parent does not have news access.");
                if (!parentClient.IsActive && client.IsActive)
                    return ApiResponse.Fail("Parent is not active.");
            }

            existing.Username = client.Username;
            existing.Password = client.Password;
            existing.FirmName = client.FirmName;
            existing.ClientName = client.ClientName;
            existing.MobileNo = client.MobileNo;
            existing.City = client.City;
            existing.IsActive = client.IsActive;
            existing.IPAddress = ipAddress;
            existing.IsNews = client.IsNews;
            existing.AccessNoOfNews = client.AccessNoOfNews;
            existing.IsRate = client.IsRate;
            existing.AccessNoOfRate = client.AccessNoOfRate;
            existing.NewsExpiredDate = newsExpiredDate;
            existing.RateExpiredDate = rateExpiredDate;
            existing.UpdateDate = DateTime.Now;
            existing.SubClientLimit = client.SubClientLimit;
            _context.Client.Update(existing);
            await _context.SaveChangesAsync();
            if (existing.Puid == "0")
            {
                await _context.Client
                .Where(b => b.Puid == existing.Id.ToString())
                .ExecuteUpdateAsync(setters => setters
                    // Always update expired dates
                    .SetProperty(n => n.NewsExpiredDate, newsExpiredDate)
                    .SetProperty(n => n.RateExpiredDate, rateExpiredDate)

                    // Conditional updates for flags
                    .SetProperty(
                        n => n.IsNews,
                        n => !client.IsNews ? client.IsNews : n.IsNews
                    )
                    .SetProperty(
                        n => n.IsRate,
                        n => !client.IsRate ? client.IsRate : n.IsRate
                    )
                    .SetProperty(
                        n => n.IsActive,
                        n => !client.IsActive ? client.IsActive : n.IsActive
                    )

                    // Always update timestamp
                    .SetProperty(b => b.UpdateDate, b => DateTime.Now)
                );
            }

            await _constant.PushClientDetails();
            Task.Run(async () => await _commonService.GetDeviceAccessSummaryAsync(existing.Id, existing.Username)).Wait();
            var newData = await GetClientListDtoAsync();
            if (client.Puid == "0")
            {
                return ApiResponse.Ok(newData.Where(x => x.Id == client.Id), "Client updated successfully.");
            }
            else
            {
                return ApiResponse.Ok(newData.Where(x => x.Id.ToString() == client.Puid), "Client updated successfully.");
            }
        }

        public async Task<ApiResponse> DeleteClientAsync(int id)
        {
            var client = await _context.Client.FindAsync(id);
            if (client == null)
                return ApiResponse.Fail("Client not found.");

            // Delete related entities
            await _context.ClientDevices.Where(u => u.ClientId == client.Id).ExecuteDeleteAsync();
            await _context.Instruments.Where(u => u.ClientId == client.Id).ExecuteDeleteAsync();

            if (client.Puid == "0")
            {
                var subClientIds = await _context.Client
                    .Where(u => u.Puid == client.Id.ToString())
                    .Select(x => x.Id)
                    .ToListAsync();

                await _context.Client.Where(u => subClientIds.Contains(u.Id)).ExecuteDeleteAsync();
                await _context.ClientDevices.Where(u => subClientIds.Contains(u.ClientId)).ExecuteDeleteAsync();
                await _context.Instruments.Where(u => subClientIds.Contains(u.ClientId)).ExecuteDeleteAsync();
            }

            _context.Client.Remove(client);
            await _context.SaveChangesAsync();

            // Post-deletion updates
            await _commonService.GetDeviceAccessSummaryAsync(client.Id, client.Username);

            if (client.Puid != "0")
            {
                var parentClient = await _context.Client
                    .FirstOrDefaultAsync(x => x.Id.ToString() == client.Puid);
                if (parentClient != null)
                    await _commonService.GetDeviceAccessSummaryAsync(parentClient.Id, parentClient.Username);
            }

            var newData = await GetClientListDtoAsync();
            return client.Puid == "0"
                ? ApiResponse.Ok(newData.Where(x => x.Id == client.Id), "Client deleted successfully.")
                : ApiResponse.Ok(newData.Where(x => x.Id.ToString() == client.Puid), "Client deleted successfully.");
        }

        public async Task<ApiResponse> CreateAndSendAlert(NotificationAlert input)
        {
            if (string.IsNullOrWhiteSpace(input.Identifier))
                return ApiResponse.Fail("Identifier is required.");

            if (string.IsNullOrWhiteSpace(input.Condition))
                return ApiResponse.Fail("Condition is required.");

            if (string.IsNullOrWhiteSpace(input.Flag))
                return ApiResponse.Fail("Flag is required.");

            if (input.Rate <= 0)
                return ApiResponse.Fail("Rate must be greater than 0.");

            if (string.IsNullOrWhiteSpace(input.Type))
                return ApiResponse.Fail("Type is required.");

            var instrumentExists = await _context.Instruments.AnyAsync(i =>i.Identifier == input.Identifier && i.ClientId == input.ClientId && i.IsMapped == true);

            if (!instrumentExists)
                return ApiResponse.Fail("Invalid Symbol identifier.");

            if (input.Id > 0)
            {
                var existing = await _context.NotificationAlerts
                    .FirstOrDefaultAsync(a => a.Id == input.Id && a.ClientId == input.ClientId);

                if (existing == null)
                    return ApiResponse.Fail("Alert not found for update.");

                existing.Rate = input.Rate;
                existing.Flag = input.Flag;
                existing.Type = input.Type;
                existing.Condition = input.Condition;
                existing.MDate = DateTime.UtcNow;
                existing.IsPassed = false;
                existing.AlertDate = null;

                await _context.SaveChangesAsync();
            }
            else
            {
                bool exists = await _context.NotificationAlerts.AnyAsync(a =>
                    a.Identifier == input.Identifier &&
                    a.Rate == input.Rate &&
                    a.ClientId == input.ClientId && a.ClientDeviceId == input.ClientDeviceId &&
                    a.Flag == input.Flag &&
                    a.Type == input.Type &&
                    a.Condition == input.Condition);

                if (exists)
                    return ApiResponse.Fail("A similar alert already exists.");

                input.IsPassed = false;
                input.AlertDate = null;

                await _context.NotificationAlerts.AddAsync(input);
                await _context.SaveChangesAsync();
            }

            await _constant.SetRateAlertToRedis(input.ClientId);

            var responseData = new
            {
                input.Id, input.Identifier, input.ClientId,input.Rate, input.Flag, input.Condition,
                Type = input.Type?.Trim().ToLower() switch
                {
                    "bid" => "0",
                    "ask" => "1",
                    "ltp" => "2",
                    _ => input.Type
                }

            };
            return ApiResponse.Ok(responseData, "Alert  saved successfully.");
        }

        public async Task<ApiResponse> GetNotificationsAsync(int clientId, string deviceId, string deviceType)
        {
            var alerts = await _context.NotificationAlerts
                            .Join(_context.Instruments,
                                n => new { n.ClientId, n.Identifier },
                                i => new { i.ClientId, i.Identifier },
                                (n, i) => new { NotificationAlert = n, Instrument = i })
                            .Where(joined => joined.Instrument.IsMapped == true && joined.NotificationAlert.ClientId == clientId )
                            .Select(joined => joined.NotificationAlert)
                            .OrderByDescending(n => n.CreateDate)
                            .ToListAsync();

            var responseData = alerts.Select(a => new
            {
                a.Id, a.Identifier,a.Rate, a.Flag, a.Condition,
                Type = a.Type?.Trim().ToLower() switch
                {
                    "bid" => "0",
                    "ask" => "1",
                    "ltp" => "2",
                    _ => a.Type
                },
                a.IsPassed, a.AlertDate, a.CreateDate, a.MDate
            });

            return ApiResponse.Ok(responseData);
        }

        public async Task<ApiResponse> MarkRateAlertPassedAsync(int clientId, string symbol, int id)
        {
            var alert = await (from a in _context.NotificationAlerts join c in _context.Client on a.ClientId equals c.Id
                where a.Id == id && a.ClientId == clientId && a.Identifier.Equals(symbol) && !a.IsPassed && a.AlertDate == null select a ).FirstOrDefaultAsync();
            if (alert == null) 
                return ApiResponse.Fail("No matching pending alert found, or client does not exist.");
            alert.IsPassed = true;
            alert.AlertDate = DateTime.UtcNow;
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                return ApiResponse.Fail($"Failed to mark alert as passed. Error: {ex.Message}");
            }

            return ApiResponse.Ok("Alert marked as passed.");
        }

        public async Task<ApiResponse> AddWatchInstrumentAsync(WatchInstrument watchInstrument, int clientId)
        {
            if (watchInstrument == null || string.IsNullOrWhiteSpace(watchInstrument.Identifier))
                return ApiResponse.Fail("Invalid instrument data.");
            
            watchInstrument.ClientId = clientId;
            watchInstrument.Mdate = DateTime.UtcNow;
            var existing = await _context.WatchInstrument.FirstOrDefaultAsync(w => w.ClientId == clientId && w.Identifier == watchInstrument.Identifier);

            if (existing != null)
            {
                return ApiResponse.Fail("Instrument already in watchlist.");
            }
            await _context.WatchInstrument.AddAsync(watchInstrument);
            await _context.SaveChangesAsync();
            var responseData = new
            {
                watchInstrument.Id, watchInstrument.Identifier, watchInstrument.Mdate
            };
            return ApiResponse.Ok(responseData, "Instrument added to watchlist successfully.");
        }

        public async Task<ApiResponse> GetWatchInstrumentAsync(int clientId)
        {
            var watchInstruments = await _context.WatchInstrument.Where(w => w.ClientId == clientId).OrderByDescending(w => w.Mdate).Select(w => new
                { w.Id,w.Identifier,w.Mdate }).ToListAsync();

            return ApiResponse.Ok(watchInstruments, "Watch instruments retrieved successfully.");
        }
        public async Task<ApiResponse> DeleteNotificationAsync(int clientId, int alertId)
        {
            var client = await _context.Client.FindAsync(clientId);
            if (client == null)
                return ApiResponse.Fail("Client not found.");

            // Delete related devices first
            var alert = await _context.NotificationAlerts
                .Where(u => u.ClientId == client.Id && u.Id == alertId)
                .FirstOrDefaultAsync();

            if (alert == null)
            {
                return ApiResponse.Fail("Rate alert not found.");
            }
            else if (alert.IsPassed)
            {
                return ApiResponse.Fail("Unable to delete: the rate alert has already triggered and is no longer active.");
            }
            else
            {
                _context.NotificationAlerts.Remove(alert);

                await _context.SaveChangesAsync(); // ✅ Single commit

                var responseData = new { clientId = clientId };
                return ApiResponse.Ok(responseData, "Alert deleted successfully.");
            }
        }
        public async Task<IEnumerable<ClientUser>> GetSubClientAsync(int clientId)
        {
            var subClients = await GetSubClientListAsync(clientId);
            return subClients;
        }
        public async Task<ApiResponse> ChangePasswordSubClientAsync(int clientId, int subClientId, string password)
        {
            var subClients = await GetSubClientListAsync(clientId);

            var subClient = subClients.Where(x => x.Id == subClientId).FirstOrDefault();

            if (subClient == null)
                return ApiResponse.Fail("Sub Client not found.");

            var res = await _context.Client
                            .Where(b => b.Id == subClientId)
                            .ExecuteUpdateAsync(setters => setters
                            .SetProperty(b => b.Password, password)
                            .SetProperty(b => b.UpdateDate, DateTime.Now));

            return ApiResponse.Ok(null, "Password reset successfully.");
        }
        private async Task<IEnumerable<ClientUser>> GetSubClientListAsync(int clientId)
        {
            var clients = await _context.Client
                        .FromSqlInterpolated($@" SELECT * FROM Client CROSS APPLY STRING_SPLIT(Puid, '~') WHERE value = {clientId}")
                        .ToListAsync();
            return clients;
        }
    }
}
public  class HierarchyBuilder
{
    public  List<ClientListDto> BuildHierarchy(List<ClientListDto> rows)
    {
        // Dictionary to quickly find nodes by Id
        var nodeMap = new Dictionary<int, ClientListDto>();

        // Ensure every record exists in the map
        foreach (var row in rows)
        {
            if (!nodeMap.ContainsKey(row.Id))
            {
                nodeMap[row.Id] = row;
                row.SubClient = new List<ClientListDto>(); // always initialize
            }
        }

        // Attach children based on ParentPath
        foreach (var row in rows)
        {
            if (string.IsNullOrEmpty(row.Puid) || row.Puid == "0")
                continue; // root node

            var ids = row.Puid.Split('~').Select(int.Parse).ToList();

            // parent is always the last id in the path
            var parentId = ids.Last();

            if (nodeMap.ContainsKey(parentId))
            {
                var parent = nodeMap[parentId];
                if (!parent.SubClient.Any(c => c.Id == row.Id))
                {
                    parent.SubClient.Add(row);
                }
            }
        }

        // return only root nodes (ParentPath empty or "0")
        return rows.Where(r => string.IsNullOrEmpty(r.Puid) || r.Puid == "0").ToList();
    }
}