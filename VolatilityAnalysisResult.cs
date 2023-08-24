using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CBApp1
{
    public class VolatilityAnalysisResult
    {
        public VolatilityAnalysisResult(string product, int emaLength, LinkedList<double> peaks, LinkedList<double> emaVolatilities, double LatestVolEma, LinkedList<DateTime> peakTimes, LinkedList<DateTime> switchTimes )
        {
            Product = product;
            EmaLength = emaLength;
            Peaks = peaks;
            EmaVolatilities = emaVolatilities;
            CurrentEmaVolatility = LatestVolEma;
            PeakTimes = peakTimes;
            SwitchTimes = switchTimes;
        }

        public string Product { get; }
        public int EmaLength { get; }
        public LinkedList<double> Peaks { get; }
        public LinkedList<double> EmaVolatilities { get; }
        public double CurrentEmaVolatility { get; }
        public LinkedList<DateTime> PeakTimes { get; }
        public LinkedList<DateTime> SwitchTimes { get; }
    }
}
