using CommonDatabase.Interfaces;
using CommonDatabase.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reuters.Repositories;

namespace ClientExcelApi.Controllers
{
    [Route("Client/[controller]")]
    public class ReutersController : Controller
    {
        private readonly IAuthService _authService;
        private readonly ReutersService _reutersService;

        public ReutersController(IAuthService authService, ReutersService reutersService)
        {
            _authService = authService;
            _reutersService = reutersService;
        }

        [Authorize(Roles = "Client")]
        [HttpGet("Categories")]
        public async Task<ActionResult> Categories()
        {
            try
            {
                var results = await _reutersService.GetCategories();
                return Ok(results);
            }
            catch (Exception ex)
            {
                throw ex;
            }

        }
        [Authorize(Roles = "Client")]
        [HttpGet("Items")]
        public async Task<ActionResult> Items(string category, string subCategory, int pageSize, string cursorToken)
        {
            try
            {
                var results = await _reutersService.GetItems(category, subCategory, pageSize, cursorToken);
                return Ok(results);
            }
            catch (Exception ex)
            {
                throw ex;
            }

        }


        [Authorize(Roles = "Client")]
        [HttpGet("ItemDescription")]
        public async Task<ActionResult> ItemDescription(string id)
        {
            try
            {
                var results = await _reutersService.GetDescription(id);
                return Ok(results);
            }
            catch (Exception ex)
            {
                throw ex;
            }

        }
    }
}
