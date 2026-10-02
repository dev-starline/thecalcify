using CommonDatabase.DTO;
using CommonDatabase.Interfaces;
using CommonDatabase.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CommonDatabase.Services
{
    public class CostConfigService : ICostConfigService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        public CostConfigService(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<ApiResponse> GetCostConfigListAsync(int clientId)
        {
            var client = await _context.Client.Where(x => x.Id == clientId).FirstOrDefaultAsync();
            int cId = (client.Puid == "0" ? client.Id : int.Parse(client.Puid));
            var costConfig = await _context.CostingConfig.Where(c => c.ClientId == cId).FirstOrDefaultAsync();

            return ApiResponse.Ok(
                costConfig== null?new CostingConfigDto():JsonSerializer.Deserialize<CostingConfigDto>(costConfig.ConfigJson)
                , "Cost config list fetched.");
        }

        public async Task<ApiResponse> SaveCostConfigAsync(int clientId, CostingConfigDto costingConfig)
        {
            var costConfig = await _context.CostingConfig.Where(c => c.ClientId == clientId).FirstOrDefaultAsync();
            var costCongigJson = JsonSerializer.Serialize(costingConfig);
            if (costConfig == null)
            {
                await _context.CostingConfig.AddAsync(new CostingConfig
                {
                    ClientId = clientId,
                    ConfigJson = costCongigJson,
                    CreatedDate = DateTime.Now,
                    ModifiedDate = DateTime.Now
                });
            }
            else
            {
                costConfig.ConfigJson = costCongigJson; 
                costConfig.ModifiedDate = DateTime.Now;
                _context.CostingConfig.Update(costConfig);
            }
            await _context.SaveChangesAsync();

            return ApiResponse.Ok(costingConfig, "Cost config saved successfully.");
        }
    }
}
