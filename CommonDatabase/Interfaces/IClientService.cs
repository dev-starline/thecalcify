using CommonDatabase.DTO;
using CommonDatabase.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CommonDatabase.Interfaces
{
    public interface IClientService
    {

        Task<IEnumerable<ClientUser>> GetClientListAsync();
        Task<ApiResponse> AddClientAsync(ClientUser client, string ipAddress, DateTime rateExpiredDate, DateTime newsExpiredDate);
        Task<ApiResponse> UpdateClientAsync(ClientUser client, string ipAddress, DateTime rateExpiredDate, DateTime newsExpiredDate);
        Task<ApiResponse> DeleteClientAsync(int id);
        Task<ApiResponse> CreateAndSendAlert(NotificationAlert input);
        Task<ApiResponse> GetNotificationsAsync(int clientId);
        Task<ApiResponse> MarkRateAlertPassedAsync(int clientId, string symbol,int Id);
        Task<ApiResponse> AddWatchInstrumentAsync(WatchInstrument watchInstrument, int clientId);
        Task<ApiResponse> GetWatchInstrumentAsync(int clientId);       

    }
}
