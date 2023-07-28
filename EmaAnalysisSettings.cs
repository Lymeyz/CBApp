using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CBApp1
{
    public class DoubleEmaAnalysisSettings
    {
        // Constructor for double ema analysis using difference between long and short
        public DoubleEmaAnalysisSettings( string product,
                                          double tStartP,
                                          double bTurnP,
                                          double sTurnP,
                                          double bTooLateP,
                                          double sTooLateP,
                                          int[] periods,
                                          ref ConcurrentDictionary<string, Candle> currentCandles,
                                          ref Dictionary<int, Ema> currEmas,
                                          ref ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentStack<Ema>>> emas )
        {
            Product = product;
            TStartP = tStartP;
            BTurnP = bTurnP;
            STurnP = sTurnP;
            BTooLateP = bTooLateP;
            STooLateP = sTooLateP;
            Periods = periods;
            CurrentCandles = currentCandles;
            CurrEmas = currEmas;
            Emas = emas;
        }

        // Constructor for double ema analysis using slopes of long and short emas
        public DoubleEmaAnalysisSettings( string product,
                                          double sDiffP,
                                          double sOffP,
                                          int[] periods,
                                          ref ConcurrentDictionary<string, Candle> currentCandles,
                                          ref Dictionary<int, Ema> currEmas,
                                          ref Dictionary<int, Ema> currEmaSlopes,
                                          ref ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentStack<Ema>>> emas )
        {
            Product = product;
            SDiffP = sDiffP;
            SOffP = sOffP;
            Periods = periods;
            CurrentCandles = currentCandles;
            CurrEmas = currEmas;
            CurrEmaSlopes = currEmaSlopes;
            Emas1 = emas;
            
        }

        public string Product { get; }
        public double TStartP { get; }
        public double BTurnP { get; }
        public double STurnP { get; }
        public double BTooLateP { get; }
        public double STooLateP { get; }
        public int[] Periods { get; }
        public double SDiffP { get; }
        public double SOffP { get; }
        public ConcurrentDictionary<string, Candle> CurrentCandles { get; }
        public Dictionary<int, Ema> CurrEmaSlopes { get; }
        public ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentStack<Ema>>> Emas1 { get; }
        public Dictionary<int, Ema> CurrEmas { get; }
        public ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentStack<Ema>>> Emas { get; }
    }

    public class SingleEmaAnalysisSettings
    {
        public SingleEmaAnalysisSettings( string product,
                                          double sOffP )
        {
            Product = product;
            SOffP = sOffP;
        }

        public string Product { get; }
        public double SOffP { get; }
    }
}
