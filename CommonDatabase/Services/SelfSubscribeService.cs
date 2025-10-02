using CommonDatabase.DTO;
using CommonDatabase.Interfaces;
using CommonDatabase.Models;
using CommonDatabase.Utility;
using Microsoft.EntityFrameworkCore;

namespace CommonDatabase.Services
{
    public class SelfSubscribeService : ISelfSubscribeService
    {
        private readonly AppDbContext _context;
        private readonly ApplicationConstant _constant;

        public SelfSubscribeService(AppDbContext context, ApplicationConstant constant)
        {
            _context = context;
            _constant = constant;
        }

        public async Task<IEnumerable<SelfSubscribe>> GetAllAsync()
        {
            return await _context.SelfSubscriber.ToListAsync();
        }      

        public async Task<ApiResponse> AddUpdateAsync(SelfSubscribe subscribe)
        {
            if (subscribe == null)
                return ApiResponse.Fail("Invalid data.");

            var existing = await _context.SelfSubscriber.FirstOrDefaultAsync(x => x.Id == subscribe.Id);
            var existingByIdentifier = await _context.SelfSubscriber.FirstOrDefaultAsync(x => x.Identifier == subscribe.Identifier && x.Id != subscribe.Id);
            if (existingByIdentifier != null)
            {
                return ApiResponse.Fail("A record with this Identifier already exists.");
            }
            if (existing == null)
            {
                subscribe.Mdate = DateTime.UtcNow; 
                await _context.SelfSubscriber.AddAsync(subscribe);
                await _context.SaveChangesAsync();              
                await _constant.SetSelfSubscriberToRedis(subscribe);  
            }
            else
            {
                existing.Name = subscribe.Name; existing.Bid = subscribe.Bid;
                existing.Ask = subscribe.Ask;existing.Ltp = subscribe.Ltp;existing.High = subscribe.High;existing.Low = subscribe.Low;existing.Open = subscribe.Open;
                existing.Close = subscribe.Close;existing.Mdate = DateTime.UtcNow;
                await _context.SaveChangesAsync();              
                await _constant.SetSelfSubscriberToRedis(existing);
            }

            var existingSubscribe = await _context.Subscribe.FirstOrDefaultAsync(s => s.Identifier == subscribe.Identifier);

            if (existingSubscribe == null)
            {
                var newSubscribe = new Subscribe
                {
                    Identifier = subscribe.Identifier, Contract = subscribe.Name, IsActive = true, Digit = 0, Type = "mcx", UpdateDate = DateTime.UtcNow
                };
                await _context.Subscribe.AddAsync(newSubscribe);
            }
            else
            {
                existingSubscribe.Contract = subscribe.Name;existingSubscribe.UpdateDate = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            await _constant.SetSubscriberToRedis();
            return ApiResponse.Ok(subscribe, "Self record saved successfully.");
        }

        public async Task<ApiResponse> DeleteAsync(int id)
        {
            var existing = await _context.SelfSubscriber.FindAsync(id);
            if (existing == null)
                return ApiResponse.Fail("Self record not found.");   
            
            _context.SelfSubscriber.Remove(existing);
            var existingSubscribe = await _context.Subscribe.FirstOrDefaultAsync(s => s.Identifier == existing.Identifier);
            var instrument = await _context.Instruments.FirstOrDefaultAsync(i => i.Identifier == existingSubscribe.Identifier);

            if (existingSubscribe != null)
            {
                _context.Subscribe.Remove(existingSubscribe);
                _context.Instruments.Remove(instrument);
            }
            await _context.SaveChangesAsync();
            await _constant.RemoveSelfSubscriberFromRedis(existing);
            await _constant.SetIdentifireRedisAsync();
            return ApiResponse.Ok(id, "Self deleted successfully.");
        }

    }
}
