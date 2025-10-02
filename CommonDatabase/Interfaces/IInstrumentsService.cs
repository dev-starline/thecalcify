using CommonDatabase.Models;
using CommonDatabase.DTO;

namespace CommonDatabase.Interfaces
{
    public interface IInstrumentsService
    {
        Task<ApiResponse> GetInstrumentListByClientAsync(int clientId);
        Task<ApiResponse> UpsertInstrumentListAsync(List<Instruments> input); 
        Task<ApiResponse> GetSymbolsByUserIdAsync(int clientId);
    }
}
