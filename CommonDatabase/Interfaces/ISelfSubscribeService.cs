using CommonDatabase.DTO;
using CommonDatabase.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonDatabase.Interfaces
{
    public interface ISelfSubscribeService
    {
        Task<IEnumerable<SelfSubscribe>> GetAllAsync();
        Task<ApiResponse> AddUpdateAsync(SelfSubscribe subscribe);
        Task<ApiResponse> DeleteAsync(int id);
    }

}
