using CommonDatabase.Models;
using CommonDatabase.DTO;

namespace CommonDatabase.Interfaces
{
    public interface ISubscribeService
    {
        Task<IEnumerable<Subscribe>> GetAllAsync();
        Task<ApiResponse> AddAsync(Subscribe subscribe);
        Task<ApiResponse> UpdateListAsync(List<Subscribe> subscribeList);
        Task<ApiResponse> DeleteAsync(int id);
       
    }
}
