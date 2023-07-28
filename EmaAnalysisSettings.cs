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
        /// <summary>
        /// Constructor for double ema analysis using difference between long and short
        /// </summary>
        /// <param name="product">Product id</param>
        /// <param name="startP">Percentage difference between emas considered start of trend</param>
        /// <param name="bTurnP">Percentage decrease in difference between emas after peak difference to trigger buy</param>
        /// <param name="sTurnP">Percentage decrease in difference between emas after peak difference to trigger sell</param>
        /// <param name="bTooLateP">Percentage decrease in difference between emas --> too late to buy</param>
        /// <param name="sTooLateP">Percentage decrease in difference between emas --> too late to sell</param>
        /// <param name="bTrigger">Determines if analysis will trigger buys</param>
        /// <param name="sTrigger">Determines if analysis will trigger sells</param>
        /// <param name="periods">Length of emas required (short ema length, long ema length)</param>
        /// <param name="currentCandles">Reference to collection holding current candles</param>
        /// <param name="currEmas">Reference to collection holding current emas of appropriate lengths</param>
        /// <param name="emas">Reference to collection holding previously calculated emas of appropriate lengths</param>
        public DoubleEmaAnalysisSettings( string product,
                                          double startP,
                                          double bTurnP,
                                          double sTurnP,
                                          double bTooLateP,
                                          double sTooLateP,
                                          bool bTrigger,
                                          bool sTrigger,
                                          int[] periods,
                                          ref ConcurrentDictionary<string, Candle> currentCandles,
                                          ref Dictionary<int, Ema> currEmas,
                                          ref ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentStack<Ema>>> emas )
        {
            Product = product;
            TStartP = startP;
            BTurnP = bTurnP;
            STurnP = sTurnP;
            BTooLateP = bTooLateP;
            STooLateP = sTooLateP;
            BTrigger = bTrigger;
            Periods = periods;
            CurrentCandles = currentCandles;
            CurrEmas = currEmas;
            Emas = emas;
        }

        /// <summary>
        /// Constructor for double ema analysis using slopes of long and short emas
        /// </summary>
        /// <param name="product">Product id</param>
        /// <param name="sDiffP">Percentage difference in slopes of emas to trigger sell</param>
        /// <param name="sOffP">Percentage downslope on longer ema to trigger sell off</param>
        /// <param name="bTrigger">True if analysis should trigger buy</param>
        /// <param name="sTrigger">True if analysis should trigger normal sell</param>
        /// <param name="sOffTrigger">True if analysis should trigger sell off</param>
        /// <param name="periods">Length of emas required (short ema length, long ema length)</param>
        /// <param name="currentCandles">Reference to collection holding current candles</param>
        /// <param name="currEmas">Reference to collection holding current emas of appropriate lengths</param>
        /// <param name="currEmaSlopes">Reference to collection holding current slopes of emas of appropriate lengths</param>
        /// <param name="emaSlopes">Reference to collection holding previously calculated ema slopes of appropriate lengths</param>
        public DoubleEmaAnalysisSettings( string product,
                                          double sDiffP,
                                          double sOffP,
                                          bool bTrigger,
                                          bool sTrigger,
                                          bool sOffTrigger,
                                          int[] periods,
                                          ref ConcurrentDictionary<string, Candle> currentCandles,
                                          ref Dictionary<int, Ema> currEmas,
                                          ref Dictionary<int, Ema> currEmaSlopes,
                                          ref ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentStack<Ema>>> emaSlopes )
        {
            Product = product;
            SDiffP = sDiffP;
            SOffP = sOffP;
            BTrigger = bTrigger;
            STrigger = sTrigger;
            SOffTrigger = sOffTrigger;
            Periods = periods;
            CurrentCandles = currentCandles;
            CurrEmas = currEmas;
            CurrEmaSlopes = currEmaSlopes;
            EmaSlopes = emaSlopes;
            
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
        public bool BTrigger { get; }
        public bool STrigger { get; }
        public bool SOffTrigger { get; }
        public ConcurrentDictionary<string, Candle> CurrentCandles { get; }
        public Dictionary<int, Ema> CurrEmaSlopes { get; }
        public ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentStack<Ema>>> EmaSlopes { get; }
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
