using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CBApp1
{
    public class SingleEmaAnalysisResult
    {
        public SingleEmaAnalysisResult()
        {
            Complete = false;
            SellOff = false;
            SellOk = false;
            BuyOk = false;
        }


        public bool Trend { get; set; } // true = short ema over long ema, positive
        public double RateAverage { get; set; }
        public int RateAverageCount { get; set; }
        public bool SellOk { get; set; }
        public bool BuyOk { get; set; }
        public bool SellOff { get; set; }
        public bool Complete { get; set; }
        public double Price { get; set; }
        public DateTime Time { get; set; } // start of trend
    }
}
