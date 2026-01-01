using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CommonDatabase.Models
{
    public class MarketWatch
    {
        [Key]
        [JsonIgnore]
        public int Id { get; set; }

        [JsonIgnore]
        public int ClientId { get; set; }

        [JsonIgnore]
        public int ClientDeviceId { get; set; }

        [Required(ErrorMessage = "MarketWatchName is required")]
        public string MarketWatchName { get; set; }

        [Required(ErrorMessage = "ListOfSymbols is required")]
        public string ListOfSymbols { get; set; }

        [JsonIgnore]
        public DateTime CreatedDate { get; set; }

        [JsonIgnore]
        public DateTime ModifiedDate { get; set; }
    }
}