using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CommonDatabase.Models
{
    public class ClientUser
    {
        [Key]
        public int Id { get; set; }
        [Required]
        [MaxLength(30)]
        public string Username { get; set; } = string.Empty;
        [Required]
        public string Password { get; set; } = string.Empty;
        [Required]
        public string FirmName { get; set; } = string.Empty;
        [Required]
        public string ClientName { get; set; } = string.Empty;
        [Required]
        [MaxLength(15)]
        public string MobileNo { get; set; } = string.Empty;
        [Required]
        public string City { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;      
        public bool IsNews { get; set; } = false;
        public DateTime? NewsExpiredDate { get; set; } = DateTime.UtcNow;
        public int AccessNoOfNews { get; set; } = 0;      
        public bool IsRate { get; set; } = false;
        public DateTime? RateExpiredDate { get; set; } = DateTime.UtcNow;
        public int AccessNoOfRate { get; set; } = 0; 
        public string? IPAddress { get; set; }
        public string? DeviceToken { get; set; }
        public DateTime? UpdateDate { get; set; } = DateTime.UtcNow;

    }

    public class ClientDetailsDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public int AccessNoOfNews { get; set; }
        public int AccessNoOfRate { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string DeviceToken { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool IsNews { get; set; }
        public bool IsRate { get; set; }
        public DateTime NewsExpiredDate { get; set; }
        public DateTime RateExpiredDate { get; set; }
    }


}
