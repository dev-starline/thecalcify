using CommonDatabase.DTO;
using CommonDatabase.Interfaces;
using CommonDatabase.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClientExcelApi.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class CostConfigController : ControllerBase
    {
        private readonly ICostConfigService _costConfigService;

        public CostConfigController(ICostConfigService costConfigService)
        {
            _costConfigService = costConfigService;
        }
        [HttpGet("GetCostConfig")]
        public async Task<IActionResult> GetCostConfigList()
        {
            var claimClientId = User.FindFirst("Id")?.Value;
            var clients = await _costConfigService.GetCostConfigListAsync(int.Parse(claimClientId));
            return Ok(clients);
        }

        [HttpPost("SaveCostConfig")]
        public async Task<IActionResult> SaveCostConfig([FromBody] CostingConfigDto costingConfig)
        {
            var claimClientId = User.FindFirst("Id")?.Value;

            var result = await _costConfigService.SaveCostConfigAsync(int.Parse(claimClientId), costingConfig);
            return result.IsSuccess ? Ok(result) : BadRequest(result);
        }
    }
}
