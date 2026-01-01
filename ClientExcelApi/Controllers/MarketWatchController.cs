using CommonDatabase;
using CommonDatabase.DTO;
using CommonDatabase.Models;
using DashboardExcelApi;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace ClientExcelApi.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class MarketWatchController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IHubContext<ExcelHub> _hubContext;
        public MarketWatchController(AppDbContext context, IConfiguration configuration, IHubContext<ExcelHub> hubContext)
        {
            _context = context;
            _configuration = configuration;
            _hubContext = hubContext;
        }
        // GET: api/<MarketWatchController>
        [HttpGet("get-marketwatch")]
        public async Task<IActionResult> GetMarketWatch()
        {
            try
            {
                var clientId = User.FindFirst("Id")?.Value;

                var marketWatches = await _context.MarketWatch
                                    .Where(x => x.ClientId == int.Parse(clientId))
                                    .Select(o => new
                                    {
                                        o.Id,
                                        o.MarketWatchName,
                                        o.ListOfSymbols,
                                        LastModified = o.ModifiedDate.ToString("dd-MM-yyyy HH:mm:ss")
                                    }).ToListAsync();

                if (marketWatches == null)
                {
                    return NotFound(new ApiResponse
                    {
                        IsSuccess = false,
                        Message = "Not Found",
                    });
                }

                return Ok(new ApiResponse
                {
                    IsSuccess = true,
                    Message = "Success",
                    Data = marketWatches
                });
            }
            catch(Exception ex)
            {
                return BadRequest(new ApiResponse { IsSuccess = false, Message = "Something went wrong" });
            }    
        }

        // GET api/<MarketWatchController>/5
        [HttpGet("get-marketwatch/{id:int:min(1)}")]
        public async Task<IActionResult> GetMarketWatch(int id)
        {
            try
            {
                var clientId = User.FindFirst("Id")?.Value;

                var marketWatches = await _context.MarketWatch
                                    .Where(x => x.ClientId == int.Parse(clientId) && x.Id == id)
                                    .Select(o => new 
                                    {
                                        o.Id,
                                        o.MarketWatchName,
                                        o.ListOfSymbols,
                                        LastModified = o.ModifiedDate.ToString("dd-MM-yyyy HH:mm:ss")
                                    }).FirstOrDefaultAsync();

                if (marketWatches != null)
                {
                    return Ok(new ApiResponse
                    {
                        IsSuccess = true,
                        Message = "Success",
                        Data = marketWatches
                    });
                }
                else
                {
                    return NotFound(new ApiResponse
                    {
                        IsSuccess = false,
                        Message = "Not Found",
                    });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse { IsSuccess = false, Message = "Something went wrong" });
            }
        }

        // POST api/<MarketWatchController>
        [HttpPost("save-marketwatch")]
        public async Task<IActionResult> SaveMarketWatch([FromBody] MarketWatch marketWatch)
        {
            try
            {
                var clientId = User.FindFirst("Id")?.Value;
                var username = User.FindFirst("userName")?.Value;
                var deviceId = User.FindFirst("DeviceId")?.Value;

                if (marketWatch.Id != null && marketWatch.Id > 0)
                {
                    return BadRequest(new ApiResponse
                    {
                        IsSuccess = false,
                        Message = "Marketwatch update not allowed",
                    });
                }

                int clientDevicesID = await _context.ClientDevices
                                    .Where(x => x.ClientId == int.Parse(clientId) && x.DeviceId == deviceId)
                                    .Select(o => o.Id)
                                    .FirstOrDefaultAsync();

                var market = new MarketWatch
                {
                    ClientId = int.Parse(clientId),
                    ClientDeviceId = clientDevicesID,
                    ListOfSymbols = marketWatch.ListOfSymbols,
                    MarketWatchName = marketWatch.MarketWatchName,
                    CreatedDate = DateTime.UtcNow,
                    ModifiedDate = DateTime.UtcNow
                };

                await _context.MarketWatch.AddAsync(market);
                await _context.SaveChangesAsync();
                var groupName = GroupNameResolver.Resolve(username);
                await _hubContext.Clients.Group(groupName).SendAsync("MarketWatchUpdated", true);

                return Ok(new ApiResponse
                {
                    IsSuccess = true,
                    Message = "Marketwatch saved successfully",
                    Data = new { 
                        market.Id,
                        market.MarketWatchName,
                        market.ListOfSymbols,
                        LastModified = market.ModifiedDate.ToString("dd-MM-yyyy HH:mm:ss")
                    }
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse { IsSuccess = false, Message = "Something went wrong" });
            }
        }

        // PUT api/<MarketWatchController>/5
        [HttpPut("update-marketwatch/{id:int:min(1)}")]
        public async Task<IActionResult> UpdateMarketWatch(int id, [FromBody] MarketWatch marketWatch)
        {
            try
            {
                var clientId = User.FindFirst("Id")?.Value;
                var username = User.FindFirst("userName")?.Value;
                var deviceId = User.FindFirst("DeviceId")?.Value;

                var marketWatchExisting = _context.MarketWatch.FirstOrDefault(x => x.ClientId == int.Parse(clientId) && x.Id == id);

                if (marketWatchExisting == null)
                {
                    return NotFound(new ApiResponse
                    {
                        IsSuccess = false,
                        Message = "Not Found",
                    });
                }

                int clientDevicesID = await _context.ClientDevices
                                    .Where(x => x.ClientId == int.Parse(clientId) && x.DeviceId == deviceId)
                                    .Select(o => o.Id)
                                    .FirstOrDefaultAsync();

                marketWatchExisting.MarketWatchName = marketWatch.MarketWatchName;
                marketWatchExisting.ListOfSymbols = marketWatch.ListOfSymbols;
                marketWatchExisting.ClientDeviceId = clientDevicesID;
                marketWatchExisting.ModifiedDate = DateTime.UtcNow;

                _context.MarketWatch.Update(marketWatchExisting);

                await _context.SaveChangesAsync();
                var groupName = GroupNameResolver.Resolve(username);
                await _hubContext.Clients.Group(groupName).SendAsync("MarketWatchUpdated", true);

                return Ok(new ApiResponse
                {
                    IsSuccess = true,
                    Message = "Marketwatch updated successfully",
                });
            }
            catch (Exception)
            {
                return BadRequest(new ApiResponse { IsSuccess = false, Message = "Something went wrong" });
            }
        }

        // DELETE api/<MarketWatchController>/5
        [HttpDelete("delete-marketwatch/{id:int:min(1)}")]
        public async Task<IActionResult> DeleteMarketWatch(int id)
        {
            try
            {
                var clientId = User.FindFirst("Id")?.Value;
                var username = User.FindFirst("userName")?.Value;

                int marketWatchExisting = await _context.MarketWatch
                                        .Where(x => x.ClientId == int.Parse(clientId) && x.Id == id)
                                        .Select(o => o.Id)
                                        .FirstOrDefaultAsync();

                if (marketWatchExisting == 0)
                {
                    return NotFound(new ApiResponse
                    {
                        IsSuccess = false,
                        Message = "Not Found",
                    });
                }

                await _context.MarketWatch.Where(m => m.Id == id).ExecuteDeleteAsync();
                var groupName = GroupNameResolver.Resolve(username);
                await _hubContext.Clients.Group(groupName).SendAsync("MarketWatchUpdated", true);

                return Ok(new ApiResponse
                {
                    IsSuccess = true,
                    Message = "Marketwatch deleted successfully",
                });
            }
            catch (Exception)
            {
                return BadRequest(new ApiResponse { IsSuccess = false, Message = "Something went wrong" });
            }   
        }
    }
}