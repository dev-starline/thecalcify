using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonDatabase.Models
{
    public class NotificationAlert
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Identifier { get; set; }

        public int ClientId { get; set; }

        [Precision(18, 6)]
        [Required]
        public decimal Rate { get; set; }

        [Required]
        public string Flag { get; set; } // e.g., "Statusbar", "Popup"

        [Required]
        public string Condition { get; set; } // e.g., ">=" or "<="

        [Required]
        public string Type { get; set; } 

        public bool IsPassed { get; set; } = false;

        public DateTime? AlertDate { get; set; }

        public DateTime CreateDate { get; set; } = DateTime.UtcNow;

        public DateTime MDate { get; set; } = DateTime.UtcNow; // Updated on modify
        public int ClientDeviceId { get; set; }
    }


    public class MarkPassedInput
    {
        public int ClientId { get; set; }
        public string? Symbol { get; set; }
        public int Id { get; set; }
        public string? Username { get; set; }
        public string? Type { get; set; }     // Bid, Ask, Ltp or numeric equivalent
        public string? Condition { get; set; }
        public string? Flag { get; set; }
        public decimal Rate { get; set; }
        public string DeviceId { get; set; }
    }


}
