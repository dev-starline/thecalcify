using CommonDatabase.DTO;
using CommonDatabase.Interfaces;
using CommonDatabase.Models;
using Microsoft.EntityFrameworkCore;

namespace CommonDatabase.Services
{
    public class InstrumentsService : IInstrumentsService
    {
        private readonly AppDbContext _context;
        private readonly ApplicationConstant _constant;
        private readonly ICommonService _commonService;
        public InstrumentsService(AppDbContext context, ApplicationConstant constant, ICommonService commonService)
        {
            _context = context;
            _constant = constant;
            _commonService = commonService;
        }

        public async Task<ApiResponse> GetInstrumentListByClientAsync(int clientId)
        {
            try
            {
              
                var subscribeList = await _context.Subscribe.ToListAsync();               
                var instruments = await _context.Instruments.Where(i => i.ClientId == clientId).ToListAsync();
                var instrumentMap = instruments.ToDictionary(i => i.Identifier, i => i);
                var result = subscribeList.Select(s =>
                {
                    if (instrumentMap.TryGetValue(s.Identifier, out var instrument))
                    {                       
                        return new SubscribeInstrumentView
                        {
                            Identifier = s.Identifier,Contract = instrument.Contract,IsMapped = instrument.IsMapped,MappedDate = instrument.Mdate
                        };
                    }
                    else
                    {                        
                        return new SubscribeInstrumentView
                        {
                            Identifier = s.Identifier,Contract = s.Contract,   IsMapped = false,MappedDate = null
                        };
                    }
                }).ToList();

                return ApiResponse.Ok(result);
            }
            catch (Exception ex)
            {
                return ApiResponse.Fail("Failed to fetch instrument list.", ex.Message);
            }
        }

        public async Task<ApiResponse> UpsertInstrumentListAsync(List<Instruments> input)
        {
            if (input == null || !input.Any())
                return ApiResponse.Fail("Input list is empty.");

            foreach (var instrument in input)
            {
               
                var clientExists = await _context.Client.AnyAsync(c => c.Id == instrument.ClientId);
                if (!clientExists)
                    return ApiResponse.Fail($"Client with ID {instrument.ClientId} does not exist.");
                
                var subscribeExists = await _context.Subscribe.AnyAsync(s => s.Identifier == instrument.Identifier);
                if (!subscribeExists)
                    continue;              
                instrument.Mdate ??= DateTime.UtcNow;
                var existingInstrument = await _context.Instruments.FirstOrDefaultAsync(i => i.Identifier == instrument.Identifier && i.ClientId == instrument.ClientId);
                if (existingInstrument != null)
                {                  
                    existingInstrument.Contract = instrument.Contract;
                    existingInstrument.IsMapped = instrument.IsMapped;
                    existingInstrument.Mdate = instrument.Mdate;

                    _context.Instruments.Update(existingInstrument);
                }
                else
                {                   
                    await _context.Instruments.AddAsync(instrument);
                }
            }

            await _context.SaveChangesAsync();
            int clientId = input.First().ClientId;
            string? clientUsername = (await _context.Client.FirstOrDefaultAsync(c => c.Id == clientId))?.Username;
            Task.Run(async () => await _commonService.GetUserListOfSymbolAsync(clientId, clientUsername)).Wait();
            await _constant.SetIdentifireRedisAsync();
            return await GetInstrumentListByClientAsync(clientId);
        }

        public async Task<ApiResponse> GetSymbolsByUserIdAsync(int userId)
        {
            try
            {
                var client = await _context.Client.FirstOrDefaultAsync(c => c.Id == userId && c.IsActive);
                if (client == null)
                {
                    return ApiResponse.Fail("Client not found or is inactive.");
                }

                var instruments = await _context.Instruments.Where(i => i.ClientId == userId && i.IsMapped).Select(i => new { i.Id, i.Identifier, i.Contract, i.Mdate }).ToListAsync();
                var message = instruments.Any() ? "Symbols fetched successfully." : "No Symbols.";
                return ApiResponse.Ok(instruments, message);
            }
            catch (Exception ex)
            {
                return ApiResponse.Fail("Failed to fetch symbols.", ex.Message);
            }
        }

    }
}
