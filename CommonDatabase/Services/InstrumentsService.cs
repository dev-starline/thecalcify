using CommonDatabase.DTO;
using CommonDatabase.Interfaces;
using CommonDatabase.Models;
using Microsoft.EntityFrameworkCore;
using System.Net;

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
            List<string> NoMappedIdentifierInParent =new List<string>();
            int clientId = input.First().ClientId;
            string parentUsername = "";
            foreach (var instrument in input)
            {
               
                var client = await _context.Client.Where(c => c.Id == instrument.ClientId).FirstOrDefaultAsync();
                if (client == null)
                    return ApiResponse.Fail($"Client with ID {instrument.ClientId} does not exist.");
                
                var subscribeExists = await _context.Subscribe.AnyAsync(s => s.Identifier == instrument.Identifier);
                if (!subscribeExists)
                    continue;              
                

                if (client.Puid == "0")
                {
                    instrument.Mdate ??= DateTime.UtcNow;
                    var existingInstrument = await _context.Instruments.FirstOrDefaultAsync(i => i.Identifier == instrument.Identifier && i.ClientId == instrument.ClientId);
                    if (existingInstrument != null)
                    {
                        existingInstrument.Contract = instrument.Contract;
                        existingInstrument.IsMapped = instrument.IsMapped;
                        existingInstrument.Mdate = instrument.Mdate;

                        _context.Instruments.Update(existingInstrument);
                       

                        

                        var subClient = await _context.Client.Where(b => b.Puid == client.Id.ToString()).Select(x => x.Id).ToListAsync();
                        if (await _context.Instruments
                            .AnyAsync(b => subClient.Contains(b.ClientId) && b.Identifier == existingInstrument.Identifier))
                        {

                            await _context.Instruments
                            .Where(b => subClient.Contains(b.ClientId) && b.Identifier == existingInstrument.Identifier)
                            .ExecuteUpdateAsync(setters => setters
                                // Conditional updates for flags
                                .SetProperty(
                                    n => n.IsMapped,
                                    n => existingInstrument.IsMapped
                                )

                                // Always update timestamp
                                .SetProperty(b => b.Mdate, b => DateTime.Now)
                            );
                        }
                        foreach (var subClientId in subClient)
                        {
                            string? subClientUsername = (await _context.Client.FirstOrDefaultAsync(c => c.Id == subClientId))?.Username;
                            Task.Run(async () => await _commonService.GetUserListOfSymbolAsync(subClientId, subClientUsername)).Wait();
                        }
                    }
                    else
                    {
                        await _context.Instruments.AddAsync(instrument);
                    }

                    await _context.SaveChangesAsync();
                    string? clientUsername = (await _context.Client.FirstOrDefaultAsync(c => c.Id == clientId))?.Username;
                    Task.Run(async () => await _commonService.GetUserListOfSymbolAsync(clientId, clientUsername)).Wait();
                }
                else
                {
                    var childClient = await _context.Client.Where(b => b.Id == instrument.ClientId).FirstOrDefaultAsync();
                    var parentClient = await _context.Client.Where(b => b.Id == int.Parse(childClient.Puid)).FirstOrDefaultAsync();
                    parentUsername = parentClient.FirmName;
                    var parentInstruments = await _context.Instruments.Where(x => x.ClientId == int.Parse(childClient.Puid) && x.Identifier == instrument.Identifier).FirstOrDefaultAsync();
                    var isInstumentExists = await _context.Instruments.AnyAsync(b => b.ClientId == instrument.ClientId && b.Identifier == instrument.Identifier);
                    if (parentInstruments != null)
                    {
                        if (isInstumentExists)
                        {
                            await _context.Instruments
                                .Where(b => b.ClientId == instrument.ClientId && b.Identifier == instrument.Identifier)
                                .ExecuteUpdateAsync(setters => setters
                                    // Conditional updates for flags
                                    .SetProperty(
                                        n => n.IsMapped,
                                        n => !parentInstruments.IsMapped ? parentInstruments.IsMapped : instrument.IsMapped
                                    )

                                    // Always update timestamp
                                    .SetProperty(b => b.Mdate, b => DateTime.Now)
                                );
                        }
                        else
                        {
                            instrument.IsMapped = parentInstruments.IsMapped;
                            instrument.Mdate = DateTime.Now;
                            await _context.Instruments.AddAsync(instrument);
                           
                        }
                       
                    }
                    if (parentInstruments == null || !parentInstruments.IsMapped)
                    {
                        NoMappedIdentifierInParent.Add(instrument.Identifier);
                    }
                    await _context.SaveChangesAsync();
                    string? clientUsername = (await _context.Client.FirstOrDefaultAsync(c => c.Id == clientId))?.Username;
                    Task.Run(async () => await _commonService.GetUserListOfSymbolAsync(clientId, clientUsername)).Wait();
                }
            }

            var getInstrument = await GetInstrumentListByClientAsync(clientId);


            await _constant.SetIdentifireRedisAsync();
            return ApiResponse.Ok(
               getInstrument.Data, 
               NoMappedIdentifierInParent.Count > 0 ? $"{string.Join(", ", NoMappedIdentifierInParent.Select(x => x))} not mapped in {parentUsername}."
                                                    : "Success" 
                );
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
