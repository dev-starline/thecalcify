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

        public async Task<ApiResponse> AddClientAsync(ClientUser client, string ipAddress, DateTime rateExpiredDate, DateTime newsExpiredDate)
        { 
            var existingClient = await _context.Client.Where(c => c.Username == client.Username || c.MobileNo == client.MobileNo).Select(c => new { c.Username, c.MobileNo }).FirstOrDefaultAsync();
            if (existingClient != null) {
                if (existingClient.Username == client.Username && existingClient.MobileNo == client.MobileNo) 
                    return ApiResponse.Fail("Username and Mobile number already exist."); }
            client.IPAddress = ipAddress; 
            client.RateExpiredDate = rateExpiredDate; 
            client.NewsExpiredDate = newsExpiredDate; 
            await _context.Client.AddAsync(client); 
            await _context.SaveChangesAsync();
            await _constant.PushClientDetails(); 
            var newClient = _context.Client.Where(x => x.Username == client.Username).FirstOrDefault();
            Task.Run(async () => await _commonService.GetDeviceAccessSummaryAsync(newClient.Id,newClient.Username)).Wait();
            return ApiResponse.Ok(client, "Client added successfully."); 
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
            existing.UpdateDate = DateTime.UtcNow;
            _context.Client.Update(existing);
            await _context.SaveChangesAsync();
            await _constant.PushClientDetails();
            Task.Run(async () => await _commonService.GetDeviceAccessSummaryAsync(existing.Id, existing.Username)).Wait();
            return ApiResponse.Ok(existing, "Client updated successfully.");
        }


        public async Task<ApiResponse> DeleteClientAsync(int id)
        {
            var client = await _context.Client.FindAsync(id);
            if (client == null)
                return ApiResponse.Fail("Client not found.");

            // Delete related devices first
            var itemsToDelete = await _context.ClientDevices
                .Where(u => u.ClientId == client.Id)
                .ToListAsync();

            _context.ClientDevices.RemoveRange(itemsToDelete);
            _context.Client.Remove(client);

            await _context.SaveChangesAsync(); // ✅ Single commit

            await _commonService.GetDeviceAccessSummaryAsync(client.Id, client.Username); // ✅ No blocking
            
            var responseData = new { clientId = id };
            return ApiResponse.Ok(responseData, "Client deleted successfully.");
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
                    a.ClientId == input.ClientId &&
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

        public async Task<ApiResponse> GetNotificationsAsync(int clientId)
        {
            var alerts = await _context.NotificationAlerts.Where(n => n.ClientId == clientId).OrderByDescending(n => n.CreateDate) .ToListAsync();

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
                where a.Id == id && a.ClientId == clientId && a.Identifier.Equals(symbol, StringComparison.OrdinalIgnoreCase) && !a.IsPassed && a.AlertDate == null select a ).FirstOrDefaultAsync();
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

    }
}              