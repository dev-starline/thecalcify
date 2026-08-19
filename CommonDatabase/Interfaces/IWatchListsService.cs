using CommonDatabase.DTO;
using CommonDatabase.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonDatabase.Interfaces
{
    public interface IWatchListsService
    {
        Task<ApiResponse?> GetWatchListByIdAsync(int clientId, string watchListId);
        Task<ApiResponse?> GetWatchListsByClientIdAsync(int clientId);
        Task<ApiResponse?> UpsertWatchListsAsync(List<ReqWatchLists> watchList);
        Task<ApiResponse?> DeleteWatchListAsync(int clientId, string watchListId);
    }
}
