using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;

namespace CommonDatabase.Models
{
    public class Instruments
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ClientId { get; set; }

        [Required]
        public string Identifier { get; set; }

        [Required]
        public string Contract { get; set; }

        public bool IsMapped { get; set; } = false;

        public DateTime? Mdate { get; set; }
    }

    public class SubscribeInstrumentView
    {
        public string Identifier { get; set; }
        public string Contract { get; set; }      
        public bool IsMapped { get; set; }
        public DateTime? MappedDate { get; set; } // optional
    }
    [Keyless]
    public class InstrumentUserDto
    {
        public string User { get; set; }
        public string Identifier { get; set; }
    }

    public class EnrichedSymbolRate : SymbolRate
    {
        public string Contract { get; set; }
        public bool IsMapped { get; set; }
        public string MappedDate { get; set; }
    }

    public class SymbolRate
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
        public string d { get; set; }
        public string v { get; set; }

    }

   

}
