using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonDatabase.Models
{
    public class CostingConfigDto
    {
        [JsonProperty("manualFields")]
        public ManualFields ManualFields { get; set; }
        [JsonProperty("blocks")]
        public List<Block> Blocks { get; set; }
    }

    public class ManualFields
    {
        public decimal G995 { get; set; }
        public decimal G999 { get; set; }
        public decimal Sil { get; set; }
        public decimal CdC { get; set; }
        public decimal CdR { get; set; }
        public decimal CdG { get; set; }
        public decimal CdS { get; set; }
    }

    public class Block
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string SpotKey { get; set; }
        public string Premium { get; set; }
        public string InrSpotKey { get; set; }
        public string Interbank { get; set; }
        public string PurityKey { get; set; }
        public string DutyKey { get; set; }
        public string Gst { get; set; }
        public string Division { get; set; }
        public string FutureKey { get; set; }
    }

}
