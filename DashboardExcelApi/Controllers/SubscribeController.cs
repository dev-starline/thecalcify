using CommonDatabase.DTO;
using CommonDatabase.Interfaces;
using CommonDatabase.Models;
using CommonDatabase.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DashboardExcelApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SubscribeController : ControllerBase
    {
        private readonly ISubscribeService _subscribeService;
        private readonly ApplicationConstant _constant;

        public SubscribeController(ISubscribeService subscribeService, ApplicationConstant constant)
        {
            _subscribeService = subscribeService;
            _constant = constant;
        }

        [HttpGet("getSubscribe")]
        public async Task<IActionResult> GetSubscribe()
        {
            await _constant.SetSubscriberToRedis();
            return Ok(ApiResponse.Ok("Subscribe data pushed successfully."));
        }

        [HttpGet("GetSubscribeList")]
        public async Task<IActionResult> GetAll()
        {
            var data = await _subscribeService.GetAllAsync();
            return Ok(ApiResponse.Ok(data));
        }

        [HttpPost("InsertSubscribe")]
        public async Task<IActionResult> Add([FromBody] Subscribe subscribe)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse.Fail("Invalid data"));

            // Auto-trim instead of rejecting
            subscribe.Identifier = subscribe.Identifier?.Trim();
            subscribe.Contract = subscribe.Contract?.Trim();

            var result = await _subscribeService.AddAsync(subscribe);
            await _constant.SetSubscriberToRedis();
            return Ok(result);
        }


        [HttpPut("UpdateSubscribe")]
        public async Task<IActionResult> UpdateList([FromBody] List<Subscribe> subscribeList)
        {
            if (subscribeList == null || !subscribeList.Any())
                return BadRequest(ApiResponse.Fail("No data provided."));

            foreach (var item in subscribeList)
            {
                item.Identifier = item.Identifier?.Trim();
                item.Contract = item.Contract?.Trim();
            }

            var result = await _subscribeService.UpdateListAsync(subscribeList);
            await _constant.SetSubscriberToRedis();
            return Ok(result);
        }


        [HttpDelete("DeleteSubscribe")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _subscribeService.DeleteAsync(id);
            await _constant.SetSubscriberToRedis();
            return Ok(result);
        }
    }
}
