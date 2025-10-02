using CommonDatabase.DTO;
using CommonDatabase.Interfaces;
using CommonDatabase.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DashboardExcelApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InstrumentsController : ControllerBase
    {
        private readonly IInstrumentsService _instrumentsService;

        public InstrumentsController(IInstrumentsService instrumentsService)
        {
            _instrumentsService = instrumentsService;
        }
       

        [HttpGet("GetInstrumentList")]
        public async Task<IActionResult> GetAll([FromQuery] int clientId)
        {
            if (clientId <= 0)
            {
                return BadRequest(ApiResponse.Fail("Invalid or missing ClientId."));
            }

            var result = await _instrumentsService.GetInstrumentListByClientAsync(clientId);
            return Ok(result);
        }

        [HttpPost("UpsertInstrumentList")]
        public async Task<IActionResult> UpsertInstrumentList([FromBody] List<Instruments> input)
        {
            if (input == null || !input.Any())
                return BadRequest(ApiResponse.Fail("Input list is empty."));

            foreach (var item in input)
            {
                if (!TryValidateModel(item))
                    return BadRequest(ApiResponse.Fail("Invalid input for one or more instruments."));
            }

            var result = await _instrumentsService.UpsertInstrumentListAsync(input);
            return Ok(result);
        }
    }
}
