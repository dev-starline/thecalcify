using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonDatabase.Models
{
    public class CostingConfig
    {
        public int Id { get; set; }
        public int ClientId { get; set; }
        public string ConfigJson { get; set; }
        public DateTime CreatedDate { get; set; }

        public DateTime ModifiedDate { get; set; }
    }
}
