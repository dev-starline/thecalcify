using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CommonDatabase.Models
{
    public class IntervalMarketData
    {
        [JsonPropertyName("n")]
        public string N { get; set; }

        [JsonPropertyName("b")]
        public string B { get; set; }

        [JsonPropertyName("a")]
        public string A { get; set; }

        [JsonPropertyName("h")]
        public string H { get; set; }

        [JsonPropertyName("l")]
        public string L { get; set; }

        [JsonPropertyName("t")]
        public string T { get; set; }
    }
    public class MarketData
    {
        [JsonPropertyName("n")]
        public string N { get; set; }

        [JsonPropertyName("b")]
        public string B { get; set; }

        [JsonPropertyName("a")]
        public string A { get; set; }

        [JsonPropertyName("h")]
        public string H { get; set; }

        [JsonPropertyName("l")]
        public string L { get; set; }

        [JsonPropertyName("ltp")]
        public string LTP { get; set; }

        [JsonPropertyName("t")]
        public string T { get; set; }

    }
}
