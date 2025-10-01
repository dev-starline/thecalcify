using CommonDatabase.DTO;
using CommonDatabase.Interfaces;
using CommonDatabase.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Data;
using CommonDatabase.Utility;

namespace CommonDatabase.Services
{
    public class SubscribeService : ISubscribeService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public SubscribeService(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<IEnumerable<Subscribe>> GetAllAsync()
        {
            return await _context.Set<Subscribe>().ToListAsync();
        }

        public async Task<ApiResponse> AddAsync(Subscribe subscribe)
        {
            var existing = await _context.Subscribe.FirstOrDefaultAsync(s => s.Identifier == subscribe.Identifier);
            if (existing != null)
            {
                return ApiResponse.Fail("Identifier already exists. Cannot insert duplicate.");
            }            
            await _context.Subscribe.AddAsync(subscribe);
            await _context.SaveChangesAsync();
            return ApiResponse.Ok(subscribe, "Subscribe added successfully.");
        }


        public async Task<ApiResponse> UpdateListAsync(List<Subscribe> subscribeList)
        {
            var updatedList = new List<Subscribe>();

            foreach (var item in subscribeList)
            {
                var existing = await _context.Set<Subscribe>().FindAsync(item.Id);
                if (existing == null) continue;

                existing.Identifier = item.Identifier ?? existing.Identifier;
                existing.Contract = item.Contract ?? existing.Contract;
                existing.IsActive = item.IsActive;
                existing.Digit = item.Digit ?? existing.Digit;
                existing.Type = item.Type ?? existing.Type; 
                existing.UpdateDate = DateTime.UtcNow;

                updatedList.Add(existing);
            }

            if (!updatedList.Any())
                return ApiResponse.Fail("No valid Subscribe records found to update.");

            _context.Subscribe.UpdateRange(updatedList);
            await _context.SaveChangesAsync();

            return ApiResponse.Ok(updatedList, $"{updatedList.Count} records updated successfully.");
        }


        public async Task<ApiResponse> DeleteAsync(int id)
        {
            var existing = await _context.Set<Subscribe>().FindAsync(id);
            if (existing == null)
                return ApiResponse.Fail("Subscribe record not found.");

            _context.Set<Subscribe>().Remove(existing);
            await _context.SaveChangesAsync();

            return ApiResponse.Ok(id, "Subscribe deleted successfully.");
        }

    }
}
