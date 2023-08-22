using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CBApp1
{
    public class VolatilityAnalysisResult
    {
        public VolatilityAnalysisResult(string product, LinkedList<double> peaks, LinkedList<double> emaVolatilities, double LatestVolEma, LinkedList<DateTime> peakTimes, LinkedList<DateTime> switchTimes )
        {
            Product = product;
            Peaks = peaks;
            EmaVolatilities = emaVolatilities;
            CurrentEmaVolatility = LatestVolEma;
        }

        public string Product { get; }
        public LinkedList<double> Peaks { get; }
        public LinkedList<double> EmaVolatilities { get; }
        public double CurrentEmaVolatility { get; }
    }
}
