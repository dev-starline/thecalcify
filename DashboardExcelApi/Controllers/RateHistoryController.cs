using Azure.Core;
using CommonDatabase;
using CommonDatabase.DTO;
using CommonDatabase.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;
using System;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using static System.Runtime.InteropServices.JavaScript.JSType;


// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace DashboardExcelApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RateHistoryController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly IConnectionMultiplexer _redis;
        private readonly IDatabase _redisDb;
        private readonly AppDbContext _context;
        private readonly string _rateHistoryDir = "";
        private readonly int _maxHistoryRowLimit = 0;
        public RateHistoryController(AppDbContext context, IConnectionMultiplexer redis, IConfiguration configuration)
        {
            _context = context;
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
            _configuration = configuration;
            _redisDb = redis.GetDatabase();
            _rateHistoryDir = _configuration.GetValue<string>("RateHistoryDir") ?? Directory.GetCurrentDirectory();
            _maxHistoryRowLimit = _configuration.GetValue<int>("MaxHistoryRowLimit");
        }
        
        [Authorize]
        [HttpPost("market-data")]
        public async Task<IActionResult> ReadAllMarketData(MarketDataRequest request)
        {
            try
            {
                if (!DateTime.TryParseExact(request.Date, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
                    return Ok(new ApiResponse { IsSuccess = false, Message = "Date must be in format dd-MM-yyyy." });

                if (parsedDate.Date > DateTime.Now.Date)
                    return Ok(new ApiResponse { IsSuccess = false, Message = "Date must be today or earlier." });

                var clientId = User.FindFirst("Id")?.Value;
                var userInstrument = _context.Instruments
                                    .Where(ui => ui.ClientId == int.Parse(clientId) && ui.Identifier == request.Identifier)
                                    .Select(i=> new {i.IsMapped, i.Contract})
                                    .FirstOrDefault();

                if (userInstrument == null)
                    return Ok(new ApiResponse { IsSuccess = false, Message = "Invalid identifier." });

                if (!userInstrument.IsMapped) 
                    return Ok(new ApiResponse { IsSuccess = false, Message = $"You are not authorized to access {request.Identifier} identifier data." });
                
                var subscribe = _context.Subscribe
                                    .Where(s => s.Identifier == request.Identifier)
                                    .Select(i => new { i.Contract })
                                    .FirstOrDefault();

                StringBuilder sbdir = new StringBuilder();
                sbdir.Append(_rateHistoryDir);

                string datFile = $"{request.Date}.dat";
                var path = Path.Combine(_rateHistoryDir, sbdir.ToString(), datFile);
                var listMarketData = new List<MarketData>();
                string[] values = [];

                if (System.IO.File.Exists(path))
                {
                    var values1 = System.IO.File.ReadAllLines(path);
                    values = values1.Reverse().ToArray();
                }
                else
                {
                    sbdir = new StringBuilder();
                    sbdir.Append($"{_rateHistoryDir}/{request.Date}");
                    datFile = $"{subscribe.Contract}.dat";
                    path = Path.Combine(_rateHistoryDir, sbdir.ToString(), datFile);
                    var values1 = System.IO.File.ReadAllLines(path);
                    values = values1.Reverse().ToArray();
                     
                }

                listMarketData = values.Where(x => x.Split('|')[0] == subscribe.Contract)
                                .Select(val =>
                                {
                                    var parts = ((string)val).Split('|');
                                    return new MarketData
                                    {
                                        N = userInstrument.Contract,
                                        B = parts[1],
                                        A = parts[2],
                                        H = parts[3],
                                        L = parts[4],
                                        LTP = parts.Count() > 6 ? parts[6] : null,
                                        T = DateTime.Parse(parts[5]).AddHours(5).AddMinutes(30).ToString("yyyy-MM-dd HH:mm:ss.fff")
                                    };
                                }).ToList();

                if (listMarketData.Count > 0)
                {
                    if (request.DurationInMinute > 0)
                    {
                        DateTime baseDate = DateTime.Now; // or from your entity
                        if (parsedDate.Date != baseDate.Date)
                        {
                            baseDate = listMarketData.FirstOrDefault() is var first && first != null ? DateTime.Parse(first.T) : baseDate.Date;
                        }
                        DateTime cutoff = baseDate.AddMinutes(-request.DurationInMinute);

                        listMarketData = listMarketData.Where(x => DateTime.Parse(x.T) >= cutoff && DateTime.Parse(x.T) <= baseDate).ToList();
                    }
                    return Ok(new ApiResponse { IsSuccess = true, Message = "Success", Data = listMarketData });
                }
                return Ok(new ApiResponse { IsSuccess = false, Message = "Not Found" });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse { IsSuccess = false, Message = "Something went wrong" });
            }
        }

        [Authorize]
        [HttpPost("market-data-history")]
        public async Task<IActionResult> ReadMarketData(MarketParamRequest request)
        {
            //int maxRowLimit = 10000;
            try
            {
                if (!DateTime.TryParseExact(request.Date, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
                    return Ok(new ApiResponse { IsSuccess = false, Message = "Date must be in format dd-MM-yyyy." });

                if (parsedDate.Date > DateTime.Now.Date)
                    return Ok(new ApiResponse { IsSuccess = false, Message = "Date must be today or earlier." });

                TimeSpan fromTs = TimeSpan.Parse(request.FromTime);
                TimeSpan toTs = TimeSpan.Parse(request.ToTime);

                // Combine with today's date
                DateTime fromDateTime = parsedDate.Add(fromTs);
                DateTime toDateTime = parsedDate.Add(toTs);

                if (fromDateTime >= toDateTime)
                    return Ok(new ApiResponse { IsSuccess = false, Message = "fromTime must be earlier than toTime" });

                var clientId = User.FindFirst("Id")?.Value;
                var userInstrument = _context.Instruments
                                    .Where(ui => ui.ClientId == int.Parse(clientId) && ui.Identifier == request.Identifier)
                                    .Select(i => new { i.IsMapped, i.Contract })
                                    .FirstOrDefault();

                if (userInstrument == null)
                    return Ok(new ApiResponse { IsSuccess = false, Message = "Invalid identifier." });

                if (!userInstrument.IsMapped)
                    return Ok(new ApiResponse { IsSuccess = false, Message = $"You are not authorized to access {request.Identifier} identifier data." });

                var subscribe = _context.Subscribe
                                .Where(s => s.Identifier == request.Identifier)
                                .Select(i => new { i.Contract })
                                .FirstOrDefault();

                string zipFile = $"{request.Date}.zip";
                var path = Path.Combine(_rateHistoryDir, subscribe.Contract, zipFile);
                var listMarketData = new List<MarketData>();
                string[] values = [];

                if (System.IO.File.Exists(path))
                {
                    bool exists = FileExistsInZip(path, $"{request.Date}.dat");
                    if (exists)
                    {
                        var values1 = ReadFileFromZip(path, $"{request.Date}.dat");
                        values = values1.ToArray();
                    }
                }
                else 
                {
                    string datFile = $"{request.Date}.dat";
                    path = Path.Combine(_rateHistoryDir, subscribe.Contract, datFile);
                    if (System.IO.File.Exists(path))
                    {
                        //var values1 = System.IO.File.ReadAllLines(path);
                        //values = values1.ToArray();
                        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) // <-- critical
                        using (var reader = new StreamReader(stream))
                        {
                            string content = reader.ReadToEnd();
                            values = content.Split("\r\n").ToArray();
                        }
                    }
                }

                listMarketData = values
                            .Select(val => val.Split('|'))
                            .Where(parts => parts.Length > 6 && !string.IsNullOrEmpty(parts[0]) &&
                                DateTime.TryParse(parts[0], out var dt) &&
                                TimeZoneInfo.ConvertTimeFromUtc(dt, TimeZoneInfo.FindSystemTimeZoneById("India Standard Time")) >= fromDateTime &&
                                TimeZoneInfo.ConvertTimeFromUtc(dt, TimeZoneInfo.FindSystemTimeZoneById("India Standard Time")) <= toDateTime)
                            .Take(_maxHistoryRowLimit)
                            .Select(parts =>
                            {// Check if parts[0] is a DateTime
                                bool isDate0 = DateTime.TryParseExact(
                                    parts[0],
                                    "yyyy-MM-dd HH:mm:ss.fff",
                                    CultureInfo.InvariantCulture,
                                    DateTimeStyles.None,
                                    out DateTime parsedDate0
                                );

                                // Check if parts[5] is NOT a DateTime (so treat as string)
                                bool isDate5 = DateTime.TryParseExact(
                                    parts[5],
                                    "yyyy-MM-dd HH:mm:ss.fff",
                                    CultureInfo.InvariantCulture,
                                    DateTimeStyles.None,
                                    out DateTime parsedDate5
                                );
                                //DateTime.TryParse(parts[0], out var dt);
                                DateTime.TryParse(isDate0 ? parsedDate0.ToString("o") : parts[5], out var dt);
                                var istTime = TimeZoneInfo.ConvertTimeFromUtc(dt, TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"));

                                return new MarketData
                                {
                                    N = userInstrument.Contract,
                                    B = isDate0 ? parts[2] : parts[1],
                                    A = isDate0 ? parts[3] : parts[2],
                                    H = isDate0 ? parts[4] : parts[3],
                                    L = isDate0 ? parts[5] : parts[4],
                                    LTP = parts[6],
                                    T = istTime.ToString("yyyy-MM-dd HH:mm:ss.fff")
                                };
                            })
                            .ToList();

                if (listMarketData.Count > 0)
                {
                    DateTime lastFetchedRow = DateTime.Parse(listMarketData.Last().T);
                    int RowLimit = 0;
                    RowLimit = listMarketData.Count < _maxHistoryRowLimit ? listMarketData.Count : _maxHistoryRowLimit;
                    return Ok(
                        new ApiResponse { 
                            IsSuccess = true, 
                            Message = listMarketData.Count < _maxHistoryRowLimit
                                    ? "Success" 
                                    : $"Maximum {RowLimit:N0} rows can be fetched up to {lastFetchedRow.ToString("HH:mm")}. To request additional data, please adjust the time range starting from {lastFetchedRow.AddMinutes(1).ToString("HH:mm")}.", 
                            Data = listMarketData 
                        }
                    );
                }
                return Ok(new ApiResponse { IsSuccess = false, Message = "Not Found" });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse { IsSuccess = false, Message = "Something went wrong", ExceptionMessage= ex.StackTrace.ToString() });
            }
        }

        [Authorize]
        [HttpPost("interval-market-data-history")]
        public async Task<IActionResult> ReadInvtervalMarketData(MarketParamRequest request)
        {
            //int maxRowLimit = 10000;
            try
            {
                if (!DateTime.TryParseExact(request.Date, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
                    return Ok(new ApiResponse { IsSuccess = false, Message = "Date must be in format dd-MM-yyyy." });

                if (parsedDate.Date > DateTime.Now.Date)
                    return Ok(new ApiResponse { IsSuccess = false, Message = "Date must be today or earlier." });

                TimeSpan fromTs = TimeSpan.Parse(request.FromTime);
                TimeSpan toTs = TimeSpan.Parse(request.ToTime);

                // Combine with today's date
                DateTime fromDateTime = parsedDate.Add(fromTs);
                DateTime toDateTime = parsedDate.Add(toTs);

                if (fromDateTime >= toDateTime)
                    return Ok(new ApiResponse { IsSuccess = false, Message = "fromTime must be earlier than toTime" });

                var clientId = User.FindFirst("Id")?.Value;
                var userInstrument = _context.Instruments
                                    .Where(ui => ui.ClientId == int.Parse(clientId) && ui.Identifier == request.Identifier)
                                    .Select(i => new { i.IsMapped, i.Contract })
                                    .FirstOrDefault();

                if (userInstrument == null)
                    return Ok(new ApiResponse { IsSuccess = false, Message = "Invalid identifier." });

                if (!userInstrument.IsMapped)
                    return Ok(new ApiResponse { IsSuccess = false, Message = $"You are not authorized to access {request.Identifier} identifier data." });

                var subscribe = _context.Subscribe
                                .Where(s => s.Identifier == request.Identifier)
                                .Select(i => new { i.Contract })
                                .FirstOrDefault();

                string datFile = $"{request.Date}_{request.Interval}.dat";
                var path = Path.Combine(_rateHistoryDir, subscribe.Contract, datFile);
                var listMarketData = new List<MarketData>();
                var values = new List<MarketData>();


                //datFile = $"{request.Date}.dat";
                path = Path.Combine(_rateHistoryDir, subscribe.Contract, datFile);
                if (System.IO.File.Exists(path))
                {
                    //var values1 = System.IO.File.ReadAllLines(path);
                    //values = values1.ToArray();
                    using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) // <-- critical
                    using (var reader = new StreamReader(stream))
                    {
                        string content = reader.ReadToEnd();
                        values = JsonSerializer.Deserialize<List<MarketData>>(content);
                    }
                }
                

                listMarketData = values
                            .Where(parts =>
                                DateTime.TryParse(parts.T, out var dt) &&
                                TimeZoneInfo.ConvertTimeFromUtc(dt, TimeZoneInfo.FindSystemTimeZoneById("India Standard Time")) >= fromDateTime &&
                                TimeZoneInfo.ConvertTimeFromUtc(dt, TimeZoneInfo.FindSystemTimeZoneById("India Standard Time")) <= toDateTime)
                            .Select(parts =>
                            {
                                DateTime.TryParse(parts.T, out var dt);
                                var istTime = TimeZoneInfo.ConvertTimeFromUtc(dt, TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"));
                                return new MarketData
                                {
                                    N = userInstrument.Contract,
                                    B = parts.B,
                                    A = parts.A,
                                    H = parts.H,
                                    L = parts.L,
                                    LTP = parts.LTP,
                                    T = istTime.ToString("yyyy-MM-dd HH:mm")
                                };
                            })
                            .Take(_maxHistoryRowLimit)
                            .ToList();

                if (listMarketData.Count > 0)
                {
                    DateTime lastFetchedRow = DateTime.Parse(listMarketData.Last().T);
                    int RowLimit = 0;
                    RowLimit = listMarketData.Count < _maxHistoryRowLimit ? listMarketData.Count : _maxHistoryRowLimit;
                    return Ok(
                        new ApiResponse
                        {
                            IsSuccess = true,
                            Message = listMarketData.Count < _maxHistoryRowLimit
                                    ? "Success"
                                    : $"Maximum {RowLimit:N0} rows can be fetched up to {lastFetchedRow.ToString("HH:mm")}. To request additional data, please adjust the time range starting from {lastFetchedRow.AddMinutes(1).ToString("HH:mm")}.",
                            Data = listMarketData
                        }
                    );
                }
                return Ok(new ApiResponse { IsSuccess = false, Message = "Not Found" });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse { IsSuccess = false, Message = "Something went wrong", ExceptionMessage = ex.StackTrace.ToString() });
            }
        }

        [Authorize]
        [HttpGet("stream-market-data")]
        public async IAsyncEnumerable<string> GetData(string identifier, string date, string fromTime, string toTime, int maxRows = 1000)
        {
            DateTime.TryParseExact(date, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate);
            TimeSpan fromTs = TimeSpan.Parse(fromTime);
            TimeSpan toTs = TimeSpan.Parse(toTime);

            // Combine with today's date
            DateTime fromDateTime = parsedDate.Add(fromTs);
            DateTime toDateTime = parsedDate.Add(toTs);
            var subscribe = _context.Subscribe
                               .Where(s => s.Identifier == identifier)
                               .Select(i => new { i.Contract })
                               .FirstOrDefault();
            string zipFile = $"{date}.zip";
            var path = Path.Combine(_rateHistoryDir, subscribe.Contract, zipFile);
            var listMarketData = new List<MarketData>();
            //string[] values = [];
            int count = 0;
            if (System.IO.File.Exists(path))
            {
                bool exists = FileExistsInZip(path, $"{date}.dat");
                if (exists)
                {
                    using var zipStream = System.IO.File.OpenRead(path);

                    using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

                    // Assume the .dat file is named "data.dat" inside the zip
                    var entry = archive.GetEntry($"{date}.dat");
                    if (entry == null) yield break;

                    using var entryStream = entry.Open();
                    using var reader = new StreamReader(entryStream);

                    while (!reader.EndOfStream && count < maxRows)
                    {
                        var line = await reader.ReadLineAsync();
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        var parts = line.Split('|');
                        if (parts.Length < 2) continue;

                        // Parse timestamp (first column)
                        if (DateTime.TryParse(parts[0], out var timestamp))
                        {
                            if (timestamp >= fromDateTime && timestamp <= toDateTime)
                            {
                                yield return line;
                                count++;

                            }
                        }
                    }

                }
            }
            else
            {
                string datFile = $"{date}.dat";
                path = Path.Combine(_rateHistoryDir, subscribe.Contract, datFile);
                using var reader = new StreamReader(path);
                while (!reader.EndOfStream && count < maxRows)
                {
                    var line = await reader.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    // Split by '|'
                    var parts = line.Split('|');
                    if (parts.Length < 2) continue;

                    // Parse timestamp
                    if (DateTime.TryParse(parts[0], out var timestamp))
                    {
                        if (timestamp >= fromDateTime && timestamp <= toDateTime)
                        {
                            yield return line;
                        }
                    }
                }
            }
        }
        public static string[] ReadFileFromZip(string zipPath, string fileName)
        {
            using var zipStream = new FileStream(zipPath, FileMode.Open, FileAccess.Read);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

            var entry = archive.Entries
                .FirstOrDefault(e => string.Equals(Path.GetFileName(e.FullName), fileName, StringComparison.OrdinalIgnoreCase));

            if (entry == null)
                throw new FileNotFoundException($"File '{fileName}' not found in ZIP.");

            using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
            var content = reader.ReadToEnd();
            return content.Split(Environment.NewLine);
        }
        
        public static bool FileExistsInZip(string zipPath, string fileName)
        {
            using var archive = ZipFile.OpenRead(zipPath);
            return archive.Entries.Any(entry =>
                string.Equals(Path.GetFileName(entry.FullName), fileName, StringComparison.OrdinalIgnoreCase));
        }
    }
}