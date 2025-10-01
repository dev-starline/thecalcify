using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CommonDatabase.Models
{   
    public class SelfSubscribe
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string Name { get; set; }
        [Required]
        public string Identifier { get; set; }
       [RegularExpression(@"^--|-?[0-9]+(\.[0-9]+)?$", ErrorMessage = "Invalid format. Only --, integers, or decimal numbers (e.g., 12.34, -12.34) are allowed.")]
        public string Bid { get; set; }
        [RegularExpression(@"^--|-?[0-9]+(\.[0-9]+)?$", ErrorMessage = "Invalid format. Only --, integers, or decimal numbers (e.g., 12.34, -12.34) are allowed.")]
        public string Ask { get; set; }
        [RegularExpression(@"^--|-?[0-9]+(\.[0-9]+)?$", ErrorMessage = "Invalid format. Only --, integers, or decimal numbers (e.g., 12.34, -12.34) are allowed.")]
        public string Ltp { get; set; }
        [RegularExpression(@"^--|-?[0-9]+(\.[0-9]+)?$", ErrorMessage = "Invalid format. Only --, integers, or decimal numbers (e.g., 12.34, -12.34) are allowed.")]
        public string High { get; set; }
        [RegularExpression(@"^--|-?[0-9]+(\.[0-9]+)?$", ErrorMessage = "Invalid format. Only --, integers, or decimal numbers (e.g., 12.34, -12.34) are allowed.")]
        public string Low { get; set; }
        [RegularExpression(@"^--|-?[0-9]+(\.[0-9]+)?$", ErrorMessage = "Invalid format. Only --, integers, or decimal numbers (e.g., 12.34, -12.34) are allowed.")]
        public string Open { get; set; }
        [RegularExpression(@"^--|-?[0-9]+(\.[0-9]+)?$", ErrorMessage = "Invalid format. Only --, integers, or decimal numbers (e.g., 12.34, -12.34) are allowed.")]
        public string Close { get; set; }
        //public TimeSpan? Time { get; set; }
        public DateTime? Mdate { get; set; }
    }

    public class MarketFeedDto
    {
        public string n { get; set; }  
        public string i { get; set; }   
        public string b { get; set; }  
        public string a { get; set; }  
        public string ltp { get; set; } 
        public string h { get; set; }  
        public string l { get; set; }   
        public string t { get; set; }   
        public string o { get; set; }  
        public string c { get; set; }  
        public string v { get; set; } = "self";
    }

}
