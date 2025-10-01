using CommonDatabase.DTO;
using CommonDatabase.Interfaces;
using CommonDatabase.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DashboardExcelApi.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("api/[controller]")]
    [ApiController]
    public class ClientController : ControllerBase
    {
        private readonly IClientService _clientService;

        public ClientController(IClientService clientService)
        {
            _clientService = clientService;
        }

        [HttpGet("GetClientList")]
        public async Task<IActionResult> GetClientList()
        {
            var clients = await _clientService.GetClientListAsync();
            return Ok(ApiResponse.Ok(clients, "Client list fetched."));
        }

        [HttpPost("InsertClient")]
        public async Task<IActionResult> AddClient([FromBody] ClientUser client)
        {
            if (client == null || !ModelState.IsValid)
            {
                return BadRequest(new ApiResponse { IsSuccess = false, Message = "Invalid client data." });
            }

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";           
            var rateExpiredDate = client.RateExpiredDate ?? DateTime.UtcNow; 
            var newsExpiredDate = client.NewsExpiredDate ?? DateTime.UtcNow; 

            var result = await _clientService.AddClientAsync(client, ip, rateExpiredDate, newsExpiredDate);
            return result.IsSuccess ? Ok(result) : BadRequest(result);
        }

        [HttpPut("UpdateClient")]
        public async Task<IActionResult> UpdateClient([FromBody] ClientUser client)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var rateExpiredDate = client.RateExpiredDate ?? DateTime.UtcNow;
            var newsExpiredDate = client.NewsExpiredDate ?? DateTime.UtcNow;
            var result = await _clientService.UpdateClientAsync(client, ip, rateExpiredDate, newsExpiredDate);
            return result.IsSuccess ? Ok(result) : NotFound(result);
        }

        [HttpDelete("DeleteClient")]
        public async Task<IActionResult> DeleteClient(int id)
        {
            var result = await _clientService.DeleteClientAsync(id);
            return result.IsSuccess ? Ok(result) : NotFound(result);
        }
    }
}
