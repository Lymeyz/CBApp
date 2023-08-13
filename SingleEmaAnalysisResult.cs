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
            PeakDiff = -1;
            PeakTime = DateTime.MinValue;
            PrevPeakDiff = -1;
            PrevPeakTime = DateTime.MinValue;
            Complete = false;
            SellOff = false;
            SellOk = false;
            BuyOk = false;
        }


        public bool Trend { get; set; } // true = short ema over long ema, positive
        public bool SellOk { get; set; }
        public bool BuyOk { get; set; }
        public bool SellOff { get; set; }
        public bool Complete { get; set; }
        public double PeakDiff { get; set; }
        public double StartPrice { get; set; }
        public double Price { get; set; }
        public double PeakPrice { get; set; }

        public DateTime PeakTime { get; set; }
        public DateTime Time { get; set; } // start of trend
        public double PrevPeakPrice { get; set; }
        public double PrevPeakDiff { get; set; }
        public DateTime PrevPeakTime { get; set; }
        public DateTime PrevTime { get; set; }
    }
}
