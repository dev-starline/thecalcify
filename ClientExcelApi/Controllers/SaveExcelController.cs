using CommonDatabase;
using CommonDatabase.DTO;
using CommonDatabase.Models;
using DashboardExcelApi;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.IO;
using System.Text;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace ClientExcelApi.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class SaveExcelController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IHubContext<ExcelHub> _hubContext;
        public SaveExcelController(AppDbContext context, IConfiguration configuration, IHubContext<ExcelHub> hubContext)
        {
            _context = context;
            _configuration = configuration;
            _hubContext = hubContext;
        }
        [HttpPost("save-html-base64")]
        public async Task<IActionResult> SaveHtmlBase64([FromBody] string base64Html,string fileName)
        {
            if (string.IsNullOrEmpty(base64Html))
                return BadRequest(new { returnCode = 400, returnMsg = "Base64 HTML content is required" });
            if (string.IsNullOrEmpty(fileName))
                return BadRequest(new { returnCode = 400, returnMsg = "Filename is required" });

            try
            {
                string fileNameWithExtension = fileName.EndsWith(".html") ? fileName : $"{fileName}.html";
                // 1️⃣ Decode Base64 into raw HTML string
                byte[] bytes = Convert.FromBase64String(base64Html);
                string htmlContent = System.Text.Encoding.UTF8.GetString(bytes);

                // 2️⃣ Remove existing <script> tags
                string cleanedHtml = System.Text.RegularExpressions.Regex.Replace(
                    htmlContent,
                    "<script.*?</script>",
                    string.Empty,
                    System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase
                );

                // 3️⃣ Insert new script tag
                string newScript = GetJsScript();
                string updatedHtml = cleanedHtml.Contains("</body>")
                    ? cleanedHtml.Replace("</body>", $"{newScript}</body>")
                    : cleanedHtml + newScript;

                var clientId = User.FindFirst("Id")?.Value;
                // 4️⃣ Save to file

                //var excelData = await _context.ExcelFilePath.Where(e => e.ClientId.ToString() == clientId && EF.Functions.Like(e.SheetName, $"%{clientId}_%_{fileNameWithExtension}%")).FirstOrDefaultAsync();
                var excelData = await _context.ExcelFilePath.Where(e => e.ClientId.ToString() == clientId && e.SheetName == fileName.Trim() && e.Type == "html").FirstOrDefaultAsync();
                int sheetId = 0;

                if (excelData == null)
                {
                    var excel = new ExcelFilePath()
                    {
                        ClientId = int.Parse(clientId),
                        CreatedDate = DateTime.Now,
                        ModifiedDate = DateTime.Now,
                        SheetName = fileName,
                        Type = "html"
                    };

                    await _context.ExcelFilePath.AddAsync(excel);
                    await _context.SaveChangesAsync();
                    // EF automatically sets the Id property
                    sheetId = excel.Id;
                }
                else
                {
                    sheetId = excelData.Id;
                }

                StringBuilder sbdir = new StringBuilder();
                sbdir.Append("wwwroot/sheets");

                // Optional: confirm
                if (!Directory.Exists(sbdir.ToString()))
                {
                    // Create folder if not exists
                    Directory.CreateDirectory(sbdir.ToString());
                }
                string htmlFileName = $"{clientId}_{sheetId}_{fileNameWithExtension}";
                var path = Path.Combine(Directory.GetCurrentDirectory(), sbdir.ToString(), htmlFileName);

                await System.IO.File.WriteAllTextAsync(path, updatedHtml);

                await _context.ExcelFilePath
                            .Where(b => b.Id == sheetId)
                            .ExecuteUpdateAsync(setters => setters
                            .SetProperty(b => b.SheetName, fileName)
                            .SetProperty(b => b.FilePath, $"{sbdir.ToString()}/{htmlFileName}")
                            .SetProperty(b => b.ModifiedDate, b => DateTime.Now));

                await _context.SaveChangesAsync();

                var username = User.FindFirst("userName")?.Value;
                var groupName = GroupNameResolver.Resolve(username);
                await _hubContext.Clients.Group(groupName).SendAsync("SheetUpdated", true);

                return Ok(new ApiResponse{
                    IsSuccess = true, 
                    Message = "HTML file created successfully", 
                });
            }
            catch (FormatException)
            {
                return BadRequest(new ApiResponse{ IsSuccess = false, Message = "Invalid Base64 string" });
            }
        }

        // GET: api/<SaveExcelController>
        [HttpGet("get-sheet-list/{id:int}")]
        public async Task<IActionResult> GetSheetList([FromRoute] int id)
        {
            try
            {
                var clientId = User.FindFirst("Id")?.Value;
                var fileList = _context.ExcelFilePath.Where(x => x.ClientId == int.Parse(clientId)).AsEnumerable();

                return Ok(new ApiResponse
                {
                    IsSuccess = true,
                    Message = "Success",
                    Data= fileList.Where(x => id == 0 || x.Id == id).Select(o => new { o.Id, o.FilePath, o.SheetName })
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse { IsSuccess = false, Message = ex.Message.ToString() });
            }
        }

        // GET: api/<SaveExcelController>
        [HttpGet("get-sheet-list")]
        public async Task<IActionResult> GetSheetList()
        {
            try
            {
                var clientId = User.FindFirst("Id")?.Value;
                var fileList = _context.ExcelFilePath.Where(x => x.ClientId == int.Parse(clientId)).AsEnumerable();
                var sheetEntries = new List<SheetEntry>();
                if (fileList.ToList().Count > 0)
                {
                    sheetEntries = fileList.Select(file => new SheetEntry
                    {
                        Type = file.Type,
                        SheetName = file.SheetName,
                        SheetId = file.Id,
                        Data = new SheetData
                        {
                            Url = file.Type == "html" ? file.FilePath.Replace("wwwroot/", "") : "",
                            EditedCells = file.Type == "json" ? JsonConvert.DeserializeObject<EditedCells>(file.FilePath) : new EditedCells(), // Initialize as empty
                            SheetJSON = file.Type == "json" ? JsonConvert.DeserializeObject<SheetModel>(GetJson()) : new SheetModel() // Initialize as empty
                        },
                        LastUpdated = file.ModifiedDate.ToString("dd-MM-yyyy HH:mm:ss")
                    }).ToList();
                }
                else
                {
                    string editableCellsJson = GetDefaultEditedCells();
                    var excelData = new ExcelFilePath()
                    {
                        // Assign properties as needed
                        ClientId = int.Parse(clientId),
                        SheetName = "Cost.Cal",
                        Type = "json",
                        FilePath = editableCellsJson,
                        CreatedDate = DateTime.Now,
                        ModifiedDate = DateTime.Now
                    };
                    await _context.ExcelFilePath.AddAsync(excelData);
                    await _context.SaveChangesAsync();
                    sheetEntries.Add(new SheetEntry
                    {
                        Type = "json",
                        SheetName = "Cost.Cal",
                        SheetId = excelData.Id,
                        Data = new SheetData
                        {
                            Url = "",
                            EditedCells = JsonConvert.DeserializeObject<EditedCells>(editableCellsJson),
                            SheetJSON = JsonConvert.DeserializeObject<SheetModel>(GetJson())
                        },
                        LastUpdated = excelData.ModifiedDate.ToString("dd-MM-yyyy HH:mm:ss")
                    });
                }


                return Ok(new ApiResponse
                {
                    IsSuccess = true,
                    Message = "Success",
                    Data = sheetEntries.OrderByDescending(x => x.Type)
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse { IsSuccess = false, Message = ex.Message.ToString() });
            }
        }

        // POST api/<SaveExcelController>
        [HttpPost("edited-cells")]
        public async Task<IActionResult> EditedCells([FromBody] EditableCells editableCells)
        {
            try
            {
                var clientId = User.FindFirst("Id")?.Value;
                var data = await _context.ExcelFilePath.Where(x => x.ClientId == int.Parse(clientId) && x.Type == "json").FirstOrDefaultAsync();
                
                if (data == null)
                {
                    var excelData = new ExcelFilePath()
                    {
                        // Assign properties as needed
                        ClientId = int.Parse(clientId),
                        SheetName = "Cost.Cal",
                        Type = "json",
                        FilePath = editableCells.editableCellsJson,
                        CreatedDate = DateTime.Now,
                        ModifiedDate = DateTime.Now
                    };
                    await _context.ExcelFilePath.AddAsync(excelData);
                }
                else
                {
                    data.SheetName = "Cost.Cal";
                    data.FilePath = editableCells.editableCellsJson;
                    data.ModifiedDate = DateTime.Now;
                    _context.ExcelFilePath.Update(data);
                }

                await _context.SaveChangesAsync();

                var username = User.FindFirst("userName")?.Value;

                var groupName = GroupNameResolver.Resolve(username);
                await _hubContext.Clients.Group(groupName).SendAsync("SheetUpdated", true);

                return Ok(new ApiResponse
                {
                    IsSuccess = true,
                    Message = "Editable cells saved successfully"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse { IsSuccess = false, Message = ex.Message.ToString() });
            }
        }

        // DELETE api/<SaveExcelController>/5
        [HttpDelete("delete-html-file/{id:int}")]
        public async Task<IActionResult> DeleteHTMLFile(int id)
        {
            try
            {
                var clientId = User.FindFirst("Id")?.Value;
                var res = await _context.ExcelFilePath.Where(x => x.Id == id && x.ClientId == int.Parse(clientId)).FirstOrDefaultAsync();
                if (res == null)
                {
                    return NotFound(new ApiResponse
                    {
                        IsSuccess = false,
                        Message = "Not Found",
                    });
                }
                else
                {
                    if (res.Type == "json")
                    {
                        return BadRequest(new ApiResponse
                        {
                            IsSuccess = false,
                            Message = $"{res.SheetName} ({res.Type}) is mandatory and cannot be deleted."
                        });
                    }
                    else 
                    {
                        var path = Path.Combine(Directory.GetCurrentDirectory(), res.FilePath);
                        var directory = path.Substring(0, path.LastIndexOf("/"));

                        if (Directory.Exists(directory))
                        {
                            System.IO.File.Delete(path);
                            _context.ExcelFilePath.Remove(res);
                        }
                        await _context.SaveChangesAsync();

                        var username = User.FindFirst("userName")?.Value;
                        var groupName = GroupNameResolver.Resolve(username);
                        await _hubContext.Clients.Group(groupName).SendAsync("SheetUpdated", true);

                        return Ok(new ApiResponse
                        {
                            IsSuccess = true,
                            Message = "HTML file removed successfully"
                        });
                    }
                        
                }
                    
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse { IsSuccess = false, Message = ex.Message.ToString() });
            }
        }

        #region Private Method
        private string GetJsScript()
        {
            return $"<script src=\"{_configuration.GetSection("apiUrl").Value}ExtensionJs/excel-html-script.js\"></script>";
        }
        private string GetJson()
        {
            return System.IO.File.ReadAllText("wwwroot/ExcelExtractedJson/excel_extracted_json.json");
        }
        private string GetDefaultEditedCells()
        {
            return _configuration.GetSection("defaultEditedCells").Value;
        }
        #endregion
    }
}
public class EditableCells
{
    public string editableCellsJson { get; set; }
}