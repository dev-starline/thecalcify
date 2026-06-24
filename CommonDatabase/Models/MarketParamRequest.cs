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

    public class ChartParamRequest
    {
        [Required(ErrorMessage = "Identifier is required")]
        public string Identifier { get; set; }

        [Required(ErrorMessage = "FromDateTime is required")]
        [RegularExpression(@"^\d{4}-(0[1-9]|1[0-2])-(0[1-9]|[12]\d|3[01]) ([01]\d|2[0-3]):[0-5]\d$",
    ErrorMessage = "Must be in yyyy-MM-dd HH:mm format")]

        public string FromDateTime { get; set; }

        [Required(ErrorMessage = "ToDateTime is required")]
        [RegularExpression(@"^\d{4}-(0[1-9]|1[0-2])-(0[1-9]|[12]\d|3[01]) ([01]\d|2[0-3]):[0-5]\d$",
    ErrorMessage = "Must be in yyyy-MM-dd HH:mm format")]

        public string ToDateTime { get; set; }

        public int Interval { get; set; }
    }
}
