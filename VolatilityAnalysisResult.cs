using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CBApp1
{
    public class VolatilityAnalysisResult
    {
        public VolatilityAnalysisResult(string product, LinkedList<double> peaks)
        {
            Product = product;
            Peaks = peaks;
        }

        public string Product { get; }
        public LinkedList<double> Peaks { get; }
    }
}
