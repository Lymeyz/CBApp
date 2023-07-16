using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CBApp1
{
    public class Caid
    {
        public Caid( Dictionary<string, Candle> shortCandles,
                    Dictionary<string, Stack<int>> tradeIds,
                    Dictionary<string, int> firstIds,
                    Dictionary<string, int> lastIds )
        {
            ShortCandles = shortCandles;
            LongCandles = null;
            TradeIds = tradeIds;
            FirstIds = firstIds;
            LastIds = lastIds;
        }

        public Dictionary<string, Candle> ShortCandles { get; }
        public Dictionary<string, Candle> LongCandles { get; set; }
        public Dictionary<string, Stack<int>> TradeIds { get; }
        public Dictionary<string, int> FirstIds { get; }
        public Dictionary<string, int> LastIds { get; }
    }
}
