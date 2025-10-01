using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonDatabase.Models
{
  
    public class WatchInstrument
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ClientId { get; set; }

        [Required]
        public string Identifier { get; set; }

        public DateTime? Mdate { get; set; }
    }
}
