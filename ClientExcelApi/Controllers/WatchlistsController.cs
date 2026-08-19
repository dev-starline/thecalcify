using CommonDatabase.Interfaces;
using CommonDatabase.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ClientExcelApi.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class WatchlistsController : ControllerBase
    {
        private readonly IWatchListsService _watchListsService;
        public WatchlistsController(IWatchListsService watchListsService)
        {
            _watchListsService = watchListsService;
        }

        [HttpGet("GetWatchlists")]
        public async Task<IActionResult> GetWatchlists()
        {
            try
            {
                var clientIdClaim = User.FindFirst("Id")?.Value;
                var response = await _watchListsService.GetWatchListsByClientIdAsync(int.Parse(clientIdClaim)); // Replace 1 with the actual clientId
                return Ok(new { message = "Watchlists retrieved successfully.", data = response });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Error retrieving watchlists: {ex.Message}" });
            }
        }
        [HttpPost("UpsertWatchlist")]
        public async Task<IActionResult> UpsertWatchlist([FromBody] List<ReqWatchLists> watchlist)
        {
            try
            {
                var clientIdClaim = User.FindFirst("Id")?.Value;
                foreach (var req in watchlist)
                {
                    req.ClientId = int.Parse(clientIdClaim);
                }
                var response = await _watchListsService.UpsertWatchListsAsync(watchlist);
                return Ok(new { message = "Watchlist upserted successfully.", data = response });
            }
            catch (Exception)
            {

                throw;
            }
        }
        [HttpDelete("DeleteWatchlist")]
        public async Task<IActionResult> DeleteWatchlist(string wId)
        {
            try
            {
                var clientIdClaim = User.FindFirst("Id")?.Value;
                var response = await _watchListsService.DeleteWatchListAsync(int.Parse(clientIdClaim), wId);
                return Ok(new { message = "Watchlist deleted successfully.", data = response });
            }
            catch (Exception)
            {

                throw;
            }
        }

        [HttpGet("GetWatchlistById")]
        public async Task<IActionResult> GetWatchlistById(string watchlistId)
        {
            try
            {
                var clientIdClaim = User.FindFirst("Id")?.Value;
                var response = await _watchListsService.GetWatchListByIdAsync(int.Parse(clientIdClaim), watchlistId);
                return Ok(new { message = "Watchlist retrieved successfully.", data = response });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Error retrieving watchlist: {ex.Message}" });
            }
        }
    }
}
