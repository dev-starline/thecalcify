using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonDatabase.Models
{
    public class MarketParamRequest
    {
        [Required(ErrorMessage = "Identifier is required")]
        public string Identifier { get; set; }

        [Required(ErrorMessage = "Date is required")]
        public string Date { get; set; }

        public int Interval { get; set; }
        [Required(ErrorMessage = "FromTime is required")]
        [RegularExpression(@"^([01]\d|2[0-3]):[0-5]\d$", ErrorMessage = "FromTime must be in HH:mm format")]
        public string FromTime { get; set; }
        [Required(ErrorMessage = "ToTime is required")]
        [RegularExpression(@"^([01]\d|2[0-3]):[0-5]\d$", ErrorMessage = "ToTime must be in HH:mm format")]
        public string ToTime { get; set; }
    }
}
