using CommonDatabase.DTO;
using CommonDatabase.Interfaces;
using FirebaseAdmin.Messaging;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonDatabase.Models
{
    public class WatchlistsService : IWatchListsService
    {
        private readonly AppDbContext _context;
        public WatchlistsService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ApiResponse> UpsertWatchListsAsync(List<ReqWatchLists> watchLists)
        {
            if (watchLists == null || !watchLists.Any())
                return null;

            foreach (var watchList in watchLists)
            {
                // Check if record exists (by Id or unique key like ClientId + WId)
                var existing = await _context.WatchLists
                    .FirstOrDefaultAsync(w => w.WId == watchList.WId);

                if (existing != null)
                {
                    // Update existing record
                    existing.ClientId = watchList.ClientId;
                    existing.WId = watchList.WId;
                    existing.Title = watchList.Title;
                    existing.Layout = watchList.Layout;
                    existing.Charts = watchList.Charts;
                    existing.Sizes = watchList.Sizes;
                    existing.UpdatedDate = DateTime.Now;

                    _context.WatchLists.Update(existing);
                }
                else
                {
                    // Insert new record
                    var newWatchList = new WatchLists
                    {
                        ClientId = watchList.ClientId,
                        WId = watchList.WId,
                        Title = watchList.Title,
                        Layout = watchList.Layout,
                        Charts = watchList.Charts,
                        Sizes = watchList.Sizes,
                        CreatedDate = DateTime.Now,
                        UpdatedDate = DateTime.Now
                    };

                    await _context.WatchLists.AddAsync(newWatchList);
                }
            }

            await _context.SaveChangesAsync();

            return new ApiResponse{
                IsSuccess = true,
                Message = "Watchlists upserted successfully."
            };
        }


        public async Task<ApiResponse> DeleteWatchListAsync(int clientId, string watchListId)
        {
            _context.WatchLists.RemoveRange(_context.WatchLists.Where(w => w.ClientId == clientId && w.WId == watchListId));
            await _context.SaveChangesAsync();
            return new ApiResponse
            {
                IsSuccess = true,
                Message = "Watchlists deleted successfully."
            };
        }

        public async Task<ApiResponse> GetWatchListByIdAsync(int clientId, string watchListId)
        {
            var watchList = _context.WatchLists
                .Where(w => w.ClientId == clientId && w.WId == watchListId)
                .Select(x => new
                {
                    x.WId,
                    x.Title,
                    x.Layout,
                    x.Charts,
                    x.Sizes
                }).FirstOrDefault();
            if (watchList ==null)
            {
                return new ApiResponse
                {
                    IsSuccess = false,
                    Message = "Not Found",
                };
            }
            return new ApiResponse
            {
                IsSuccess = true,
                Message = "Watchlists retrieved successfully.",
                Data = watchList
            };
        }

        public async Task<ApiResponse> GetWatchListsByClientIdAsync(int clientId)
        {
            var watchLists = _context.WatchLists
                .Where(w => w.ClientId == clientId)
                .Select(x => new {
                    x.WId,
                    x.Title,
                    x.Layout,
                    x.Charts,
                    x.Sizes
                })
                .ToList();
            if (watchLists == null || !watchLists.Any())
            {
                return new ApiResponse
                {
                    IsSuccess = false,
                    Message = "Not Found",
                };
            }
            return new ApiResponse
            {
                IsSuccess = true,
                Message = "Watchlists retrieved successfully.",
                Data = watchLists
            };
        }
    }
}
