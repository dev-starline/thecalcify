using CommonDatabase.Models;
using CommonDatabase.DTO; // ✅ Make sure namespace matches DTO folder
using System.Threading.Tasks;

namespace CommonDatabase.Interfaces
{
    public interface IAuthService
    {
        Task<ApiResponse> LoginAsync(AdminLogin loginDto);
        Task<ApiResponse> ValidateClientLogin(ClientAuth clientLogin);
        Task<ApiResponse> ClientLogout(LogoutRequest logoutRequest);
        Task<ApiResponse> UpdateStatusDnd(StatusDnd status);
        Task<ApiResponse> UpdateTopicKeyword(TopicKeyword status);
    }
}
