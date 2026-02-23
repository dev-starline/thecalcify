using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CommonDatabase.Models
{
    public class MarketDataDto
    {
        [JsonPropertyName("n")]
        public string N { get; set; }

        [JsonPropertyName("i")]
        public string I { get; set; }

        [JsonPropertyName("b")]
        public string B { get; set; }

        [JsonPropertyName("a")]
        public string A { get; set; }

        [JsonPropertyName("ltp")]
        public string Ltp { get; set; }

        [JsonPropertyName("h")]
        public string H { get; set; }

        [JsonPropertyName("l")]
        public string L { get; set; }

        [JsonPropertyName("t")]
        public DateTime T { get; set; }

        [JsonPropertyName("o")]
        public string O { get; set; }

        [JsonPropertyName("c")]
        public string C { get; set; }

        [JsonPropertyName("d")]
        public string D { get; set; }

        [JsonPropertyName("v")]
        public string V { get; set; }

        [JsonPropertyName("atp")]
        public string Atp { get; set; }

        [JsonPropertyName("bq")]
        public string Bq { get; set; }

        [JsonPropertyName("tbq")]
        public string Tbq { get; set; }

        [JsonPropertyName("sq")]
        public string Sq { get; set; }

        [JsonPropertyName("tsq")]
        public string Tsq { get; set; }

        [JsonPropertyName("vt")]
        public string Vt { get; set; }

        [JsonPropertyName("oi")]
        public string Oi { get; set; }

        [JsonPropertyName("ltq")]
        public string Ltq { get; set; }
    }
}
