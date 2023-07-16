using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CBApp1
{
    public class LameCandle
    {
        [JsonConstructor]
        public LameCandle(string start, string open, string low, string high, string close)
        {
            Start = start;
            Open = open;
            Low = low;
            High = high;
            Close = close;
        }

        public string Start { get; }
        public string Open { get; }
        public string Low { get; }
        public string High { get; }
        public string Close { get; }
    }
}
