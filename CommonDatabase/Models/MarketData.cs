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

        [JsonPropertyName("ah")]
        public string AH { get; set; }

        [JsonPropertyName("al")]
        public string AL { get; set; }

        [JsonPropertyName("t")]
        public string T { get; set; }

        [JsonPropertyName("ao")]
        public string AO { get; set; }

        [JsonPropertyName("ac")]
        public string AC { get; set; }

        [JsonPropertyName("vt")]
        public string VT { get; set; }

        [JsonPropertyName("bh")]
        public string? BH { get; set; }

        [JsonPropertyName("bl")]
        public string? BL { get; set; }
        [JsonPropertyName("bo")]
        public string? BO { get; set; }
            
        [JsonPropertyName("bc")]
        public string? BC { get; set; }

         [JsonPropertyName("ltph")]
        public string? LTPH { get; set; }

        [JsonPropertyName("ltpl")]
        public string? LTPL { get; set; }
        [JsonPropertyName("ltpo")]
        public string? LTPO { get; set; }

        [JsonPropertyName("ltpc")]
        public string? LTPC { get; set; }
    }

    public class ChartIntervalData
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("askHigh")]
        public string AskHigh { get; set; }

        [JsonPropertyName("askLow")]
        public string AskLow { get; set; }

        [JsonPropertyName("time")]
        public long Time { get; set; }

        [JsonPropertyName("askOpen")]
        public string AskOpen { get; set; }

        [JsonPropertyName("askClose")]
        public string AskClose { get; set; }

        [JsonPropertyName("volume")]
        public string Volume { get; set; }

        [JsonPropertyName("bidHigh")]
        public string BidHigh { get; set; }

        [JsonPropertyName("bidLow")]
        public string BidLow { get; set; }

        [JsonPropertyName("bidOpen")]
        public string BidOpen { get; set; }

        [JsonPropertyName("bidClose")]
        public string BidClose { get; set; }

        [JsonPropertyName("ltpHigh")]
        public string LtpHigh { get; set; }

        [JsonPropertyName("ltpLow")]
        public string LtpLow { get; set; }

        [JsonPropertyName("ltpOpen")]
        public string LtpOpen { get; set; }

        [JsonPropertyName("ltpClose")]
        public string LtpClose { get; set; }
    }

    public class OldMarketIntervalData
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

    public class OldChartIntervalData
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