using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CBApp1
{
    class HighLow
    {
        public HighLow(double highPrice, double lowPrice, double highVolume, double lowVolume)
        {
            HighPrice = highPrice;
            LowPrice = lowPrice;
            HighVolume = highVolume;
            LowVolume = lowVolume;
        }

        public HighLow()
        {
            HighPrice = -1;
            LowPrice = -1;
            HighVolume = -1;
            LowVolume = -1;
        }
        public double HighPrice { get; set; }
        public double LowPrice { get; set; }
        public double HighVolume { get; set; }
        public double LowVolume { get; set; }
    }
}
