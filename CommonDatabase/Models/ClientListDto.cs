using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonDatabase.Models
{
    public class ClientListDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FirmName { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public string MobileNo { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public bool IsNews { get; set; } = false;
        public DateTime? NewsExpiredDate { get; set; } = DateTime.Now;
        public int AccessNoOfNews { get; set; } = 0;
        public bool IsRate { get; set; } = false;
        public DateTime? RateExpiredDate { get; set; } = DateTime.Now;
        public int AccessNoOfRate { get; set; } = 0;
        public string? IPAddress { get; set; }
        public string? DeviceToken { get; set; }
        public DateTime? UpdateDate { get; set; } = DateTime.Now;
        public string Topics { get; set; } = "";
        public string Keywords { get; set; } = "";
        public string Puid { get; set; } = "0";
        public List<ClientListDto> SubClient { get; set; } = new();
        public int SubClientLimit { get; set; }
        public int PendingSubClient { get; set; }
    }
}
