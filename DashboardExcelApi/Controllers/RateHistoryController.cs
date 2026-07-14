using Azure.Core;
using CommonDatabase;
using CommonDatabase.DTO;
using CommonDatabase.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
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
        private readonly string _chartHistoryDir = "";
        public RateHistoryController(AppDbContext context, IConnectionMultiplexer redis, IConfiguration configuration)
        {
            _context = context;
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
            _configuration = configuration;
            _redisDb = redis.GetDatabase();
            _rateHistoryDir = _configuration.GetValue<string>("RateHistoryDir") ?? Directory.GetCurrentDirectory();
            _maxHistoryRowLimit = _configuration.GetValue<int>("MaxHistoryRowLimit");
            _chartHistoryDir = _configuration.GetValue<string>("ChartHistoryDir") ?? Directory.GetCurrentDirectory();
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
                                        VT = parts.Length > 7 ? parts[7] : "",
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
                                    VT = parts.Length > 7 ? parts[7] : "",
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
                        DateTime jsonDate = new DateTime(2026, 6, 10);
                        if (parsedDate.Date > jsonDate)
                        {
                            values = content.Split(Environment.NewLine).Where(x => x != "")
                            .Select(line =>
                            {
                                var parts = line.Split("|");

                                return new MarketData
                                {
                                    T = parts[0],
                                    N = parts[1],
                                    B = parts[2],
                                    A = parts[3],
                                    H = parts[4],
                                    L = parts[5],
                                    LTP = parts[6],
                                    VT = parts.Length > 7 ? parts[7] : ""
                                };
                            })
                            .ToList();

                        }
                        else
                        {
                            values = JsonSerializer.Deserialize<List<MarketData>>(content);
                        }
                       
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
                                    VT = parts.VT,
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
        [HttpPost("chart-market-history")]
        public async Task<IActionResult> ReadChartInvtervalMarketData(ChartParamRequest request)
        {
            try
            {
                if (!DateTime.TryParseExact(request.FromDateTime, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fromDateTime))
                    return Ok(new ApiResponse { IsSuccess = false, Message = "FromDateTime must be in format yyyy-MM-dd HH:mm." });

                if (!DateTime.TryParseExact(request.ToDateTime, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var toDateTime))
                    return Ok(new ApiResponse { IsSuccess = false, Message = "ToDateTime must be in format yyyy-MM-dd HH:mm." });

                if (fromDateTime >= toDateTime)
                    return Ok(new ApiResponse { IsSuccess = false, Message = "fromDateTime must be earlier than toDateTime" });

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
                // Difference
                TimeSpan diff = toDateTime - fromDateTime;
                Console.WriteLine($"Total days: {diff.TotalDays}"); // 3.58 days
                int totalDays = (int)Math.Ceiling(diff.TotalDays);
                var listMarketData = new List<MarketData>();
                var values = new List<MarketData>();
                for (int i = 0; i < totalDays; i++)
                {
                    var day = fromDateTime.AddDays(i);
                                   
                    string datFile = $"{day.Date.ToString("dd-MM-yyyy")}_{request.Interval}.dat";
                    var path = Path.Combine(_rateHistoryDir, subscribe.Contract, datFile);
                
                    path = Path.Combine(_rateHistoryDir, subscribe.Contract, datFile);
                    if (System.IO.File.Exists(path))
                    {
                        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) // <-- critical
                        using (var reader = new StreamReader(stream))
                        {
                            string content = reader.ReadToEnd();
                            DateTime jsonDate = new DateTime(2026, 6, 10);
                            if (day.Date > jsonDate)
                            {
                                values.AddRange(content.Split(Environment.NewLine).Where(x => x != "")
                                .Select(line =>
                                {
                                    var parts = line.Split("|");

                                    return new MarketData
                                    {
                                        T = parts[0],
                                        N = parts[1],
                                        B = parts[2],
                                        A = parts[3],
                                        H = parts[4],
                                        L = parts[5],
                                        LTP = parts[6],
                                        VT = parts.Length > 7 ? parts[7] : ""
                                    };
                                })
                                .ToList());

                            }
                            else
                            {
                                values.AddRange(JsonSerializer.Deserialize<List<MarketData>>(content));
                            }
                        }
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
                                    VT = parts.VT,
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




        [HttpPost("GetIntervalData")]
        public IActionResult GetIntervalData([FromBody] MarketRequest request)
        {
            try
            {
                // 1. Basic validation
                if (string.IsNullOrWhiteSpace(request.Symbol))
                    return BadRequest("Symbol is required.");

                if (request.FromDate <= 0 || request.ToDate <= 0)
                    return BadRequest("Invalid timestamps. Must be positive Unix time in milliseconds.");

                if (request.FromDate > request.ToDate)
                    return BadRequest("fromDate cannot be greater than toDate.");

                var clientId = User.FindFirst("Id")?.Value;
                var userInstrument = _context.Instruments
                                    .Where(ui => ui.ClientId == int.Parse(clientId) && ui.Identifier == request.Symbol)
                                    .Select(i => new { i.IsMapped, i.Contract })
                                    .FirstOrDefault();

                if (userInstrument == null)
                    return Ok(new ApiResponse { IsSuccess = false, Message = "Invalid identifier." });

                if (!userInstrument.IsMapped)
                    return Ok(new ApiResponse { IsSuccess = false, Message = $"You are not authorized to access {request.Symbol} identifier data." });

                var subscribe = _context.Subscribe
                                .Where(s => s.Identifier == request.Symbol)
                                .Select(i => new { i.Contract })
                                .FirstOrDefault();
                // 2. Convert to DateTime
                DateTime fromDate = DateTimeOffset.FromUnixTimeSeconds(request.FromDate).UtcDateTime.AddHours(5).AddMinutes(30);
                DateTime toDate = DateTimeOffset.FromUnixTimeSeconds(request.ToDate).UtcDateTime.AddHours(5).AddMinutes(30);

                // Optional sanity check (e.g., max 1 year range)
                if ((toDate - fromDate).TotalDays > 365)
                    return BadRequest("Date range too large. Maximum allowed is 1 year.");

                var response = new List<MarketIntervalData>();
                var response2 = new List<ChartIntervalData>();
                // 3. Loop through month-year files
                DateTime current = new DateTime(fromDate.Year, fromDate.Month, 1);
                DateTime end = new DateTime(toDate.Year, toDate.Month, 1);

                while (current <= end)
                {
                    //string datFile = $"{day.Date.ToString("dd-MM-yyyy")}_{request.Interval}.dat";
                    //var path = Path.Combine(_rateHistoryDir, subscribe.Contract, datFile);
                    string monthYear = current.ToString("MM-yyyy");
                    string filePath = Path.Combine(_chartHistoryDir, subscribe.Contract, $"{monthYear}.dat");

                    if (System.IO.File.Exists(filePath))
                    {
                        //var lines = System.IO.File.ReadAllLines(filePath);
                        var lines = new List<string>();
                        using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (var reader = new StreamReader(fs))
                        {
                            while (!reader.EndOfStream)
                            {
                                var line = reader.ReadLine();
                                if (line != null)
                                {
                                    lines.Add(line);
                                }
                            }
                        }

                        foreach (var line in lines)
                        {
                            var parts = line.Replace("\"", "").Split(',');
                            if (parts.Length < 7) continue;

                            var tick = new MarketIntervalData
                            {
                                N = request.Symbol,
                                T = parts[1],
                                AO = parts[2],
                                AH = parts[3],
                                AL = parts[4],
                                AC = parts[5],
                                VT = parts[6],
                                BO = parts[7],
                                BH = parts[8],
                                BL = parts[9],
                                BC = parts[10],
                                LTPO = parts[11],
                                LTPH = parts[12],
                                LTPL = parts[13],
                                LTPC = parts[14],
                            };

                            string[] formats = { "dd-MM-yyyy HH:mm", "MM/dd/yyyy HH:mm" };

                            if (DateTime.TryParseExact(tick.T, formats,
                                CultureInfo.InvariantCulture,
                                DateTimeStyles.AssumeUniversal,
                                out DateTime tickTime))
                            {
                                if (tickTime.AddHours(-5).AddMinutes(-30) >= fromDate && tickTime.AddHours(-5).AddMinutes(-30) <= toDate)
                                {
                                    response2.Add(new ChartIntervalData
                                    {
                                        Name = request.Symbol,
                                        Time = new DateTimeOffset(tickTime.AddHours(-5).AddMinutes(-30)).ToUnixTimeSeconds(),
                                        AskOpen = parts[2],
                                        AskHigh = parts[3],
                                        AskLow = parts[4],
                                        AskClose = parts[5],
                                        Volume = parts[6],
                                        BidOpen = parts[7],
                                        BidHigh = parts[8],
                                        BidLow = parts[9],
                                        BidClose = parts[10],
                                        LtpOpen = parts[11],
                                        LtpHigh = parts[12],
                                        LtpLow = parts[13],
                                        LtpClose = parts[14],
                                    });
                                }
                               
                            }
                        }
                    }

                    current = current.AddMonths(1);
                }
                if (response2.Count>0)
                {
                    return Ok(new ApiResponse { IsSuccess = true, Message = "Success", Data = response2 });
                }
                return Ok(new ApiResponse { IsSuccess = false, Message = "Not Found" });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse { IsSuccess = false, Message = "Something went wrong", ExceptionMessage = ex.StackTrace });
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


        [HttpPost("FetchIntervalValues")]
        public IActionResult FetchIntervalValues([FromBody] MarketRequest request)
        {
            try
            {
                // 1. Basic validation
                if (string.IsNullOrWhiteSpace(request.Symbol))
                    return BadRequest("Symbol is required.");

                if (request.FromDate <= 0 || request.ToDate <= 0)
                    return BadRequest("Invalid timestamps. Must be positive Unix time in milliseconds.");

                if (request.FromDate > request.ToDate)
                    return BadRequest("fromDate cannot be greater than toDate.");

                var clientId = User.FindFirst("Id")?.Value;
                var userInstrument = _context.Instruments
                                    .Where(ui => ui.ClientId == int.Parse(clientId) && ui.Identifier == request.Symbol)
                                    .Select(i => new { i.IsMapped, i.Contract })
                                    .FirstOrDefault();

                if (userInstrument == null)
                    return Ok(new ApiResponse { IsSuccess = false, Message = "Invalid identifier." });

                if (!userInstrument.IsMapped)
                    return Ok(new ApiResponse { IsSuccess = false, Message = $"You are not authorized to access {request.Symbol} identifier data." });

                var subscribe = _context.Subscribe
                                .Where(s => s.Identifier == request.Symbol)
                                .Select(i => new { i.Contract })
                                .FirstOrDefault();
                // 2. Convert to DateTime
                DateTime fromDate = DateTimeOffset.FromUnixTimeSeconds(request.FromDate).UtcDateTime.AddHours(5).AddMinutes(30);
                DateTime toDate = DateTimeOffset.FromUnixTimeSeconds(request.ToDate).UtcDateTime.AddHours(5).AddMinutes(30);

                // Optional sanity check (e.g., max 1 year range)
                if ((toDate - fromDate).TotalDays > 365)
                    return BadRequest("Date range too large. Maximum allowed is 1 year.");

                var response = new List<MarketIntervalData>();
                var response2 = new List<ChartIntervalData>();
                // 3. Loop through month-year files
                DateTime current = new DateTime(fromDate.Year, fromDate.Month, 1);
                DateTime end = new DateTime(toDate.Year, toDate.Month, 1);
                var lines = new List<string>();
                while (current <= end)
                {
                    string monthYear = current.ToString("MM-yyyy");
                    string filePath = Path.Combine(_chartHistoryDir, subscribe.Contract, $"{monthYear}.dat");

                    if (System.IO.File.Exists(filePath))
                    {
                        using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (var reader = new StreamReader(fs))
                        {
                            while (!reader.EndOfStream)
                            {
                                var line = reader.ReadLine();
                                var parts = line.Replace("\"", "");
                                var splitData = parts.Split(',');
                                string[] formats = { "dd-MM-yyyy HH:mm", "MM/dd/yyyy HH:mm" };

                                if (DateTime.TryParseExact(splitData[1], formats,
                                    CultureInfo.InvariantCulture,
                                    DateTimeStyles.AssumeUniversal,
                                    out DateTime tickTime))
                                {
                                    if (tickTime.AddHours(-5).AddMinutes(-30) >= fromDate && tickTime.AddHours(-5).AddMinutes(-30) <= toDate)
                                    {
                                        // Convert the second element (index 1) to Unix timestamp
                                        string unixTimestamp = DateTimeOffset.ParseExact(
                                            splitData[1],
                                            "dd-MM-yyyy HH:mm",
                                            System.Globalization.CultureInfo.InvariantCulture
                                        ).ToUnixTimeSeconds().ToString();
                                        splitData[0] = request.Symbol;
                                        // Replace the original date with the timestamp
                                        splitData[1] = unixTimestamp;
                                        //GOLD_I,BIDOpen,BidClose,BidHigh,BidLow,AskOpen,AskClose,AskHigh,AskLow,LtpOpen,LtpClose,LtpHigh,LtpLow,Volume,Time
                                        // Concatenate everything back
                                        string result = string.Join(",", 
                                                            splitData[0],   // SymbolName
                                                            splitData[7],   // BIDOpen
                                                            splitData[10],   // BidClose
                                                            splitData[8],   // BidHigh
                                                            splitData[9],  // BidLow
                                                            splitData[2],   // AskOpen
                                                            splitData[5],   // AskClose 
                                                            splitData[3],   // AskHigh
                                                            splitData[4],   // AskLow
                                                            splitData[11],  // LtpOpen
                                                            splitData[14],  // LtpClose
                                                            splitData[12],  // LtpHigh
                                                            splitData[13],  // LtpLow
                                                            splitData[6],   // Volume
                                                            splitData[1]    // Time
                                                                //AskOpen = parts[2],
                                                                //AskHigh = parts[3],
                                                                //AskLow = parts[4],
                                                                //AskClose = parts[5],
                                                                //Volume = parts[6],
                                                                //BidOpen = parts[7],
                                                                //BidHigh = parts[8],
                                                                //BidLow = parts[9],
                                                                //BidClose = parts[10],
                                                                //LtpOpen = parts[11],
                                                                //LtpHigh = parts[12],
                                                                //LtpLow = parts[13],
                                                                //LtpClose = parts[14],
                                                        );
                                        lines.Add(result);
                                    }
                                }
                            }
                        }
                    }
                    current = current.AddMonths(1);
                }
                if (lines.Count > 0)
                {
                    return Ok(new ApiResponse { IsSuccess = true, Message = "Success", Data = lines });
                }
                return Ok(new ApiResponse { IsSuccess = false, Message = "Not Found" });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse { IsSuccess = false, Message = "Something went wrong", ExceptionMessage = ex.StackTrace });
            }
        }
    }
}