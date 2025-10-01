using CommonDatabase.DTO;
using CommonDatabase.Interfaces;
using CommonDatabase.Models;
using Microsoft.AspNetCore.Mvc;

namespace DashboardExcelApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SelfSubscribeController : ControllerBase
    {
        private readonly ISelfSubscribeService _service;

        public SelfSubscribeController(ISelfSubscribeService service)
        {
            _service = service;
        }

        [HttpGet("GetselfList")]
        public async Task<IActionResult> GetAll()
        {
            var result = await _service.GetAllAsync();
            return Ok(ApiResponse.Ok(result, "Self subscriber list fetched."));           
        }

        [HttpPost("Upsert")]
        public async Task<IActionResult> AddOrUpdate([FromBody] SelfSubscribe subscribe)
        {
            var response = await _service.AddUpdateAsync(subscribe);
            return Ok(response);
        }

        [HttpDelete("DeleteSelf")]
        public async Task<IActionResult> Delete(int id)
        {
            var response = await _service.DeleteAsync(id);
            return Ok(response);
        }
    }
}
