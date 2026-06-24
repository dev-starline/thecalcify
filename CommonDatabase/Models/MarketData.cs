using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CommonDatabase.Models
{
    public class MarketIntervalData
    {
        [JsonPropertyName("n")]
        public string N { get; set; }

        [JsonPropertyName("h")]
        public string H { get; set; }

        [JsonPropertyName("l")]
        public string L { get; set; }

        [JsonPropertyName("t")]
        public string T { get; set; }

        [JsonPropertyName("o")]
        public string O { get; set; }

        [JsonPropertyName("c")]
        public string C { get; set; }

        [JsonPropertyName("vt")]
        public string VT { get; set; }
    }

    public class ChartIntervalData
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("high")]
        public string High { get; set; }

        [JsonPropertyName("low")]
        public string Low { get; set; }

        [JsonPropertyName("time")]
        public long Time { get; set; }

        [JsonPropertyName("open")]
        public string Open { get; set; }

        [JsonPropertyName("close")]
        public string Close { get; set; }

        [JsonPropertyName("volume")]
        public string Volume { get; set; }
    }
}
