using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CBApp1
{
    public class AnalysisParameters
    {
        public AnalysisParameters( string product,
                                  double bTurnP,
                                  double sTurnP,
                                  double bTooLateP,
                                  double sTooLateP,
                                  double tStartP,
                                  double bTooHighP,
                                  double bStartCandleP,
                                  double bProfitLimitP)
        {
            Product = product;
            BTurnP = bTurnP;
            STurnP = sTurnP;
            BTooLateP = bTooLateP;
            STooLateP = sTooLateP;
            TStartP = tStartP;
            BTooHighP = bTooHighP;
            BStartCandleP = bStartCandleP;
            BProfitLimitP = bProfitLimitP;
        }

        public string Product { get; }
        public double BTurnP { get; }
        public double STurnP { get; }
        public double BTooLateP { get; }
        public double STooLateP { get; }
        public double TStartP { get; }
        public double BTooHighP { get; }
        public double BStartCandleP { get; }
        public double BProfitLimitP { get; }
    }
}
