using CommonDatabase.DTO;
using CommonDatabase.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonDatabase.Interfaces
{
    public interface ICostConfigService
    {
        Task<ApiResponse> SaveCostConfigAsync(int clientId, CostingConfigDto costingConfig);
        Task<ApiResponse> GetCostConfigListAsync(int clientId);
    }
}
