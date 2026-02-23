using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonDatabase.Models
{
    public class MarketDataRequest
    {
        [Required(ErrorMessage = "Identifier is required")]
        public string Identifier { get; set; }

        [Required(ErrorMessage = "Date is required")]
        public string Date { get; set; }
       
        public float DurationInMinute { get; set; }
    }
}
