using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CBApp1
{
    // short 6-26, 50
    // long 6-12, 20
    public class DoubleEmaAnalysisSettings
    {
        /// <summary>
        /// Constructor for double ema analysis using difference between long and short.
        /// Needs to be passed current candles, and current emas need calculating
        /// </summary>
        /// <param name="product">Product id</param>
        /// <param name="tStartP">Percentage difference between emas considered start of trend</param>
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
                                          bool slopes,
                                          double tStartP,
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
            Slopes = slopes;
            TStartP = tStartP;
            BTurnP = bTurnP;
            STurnP = sTurnP;
            BTooLateP = bTooLateP;
            STooLateP = sTooLateP;
            BTrigger = bTrigger;
            STrigger = sTrigger;
            Periods = periods;
            CurrentCandles = currentCandles;
            CurrEmas = currEmas;
            Emas = emas;
        }

        /// <summary>
        /// Constructor for double ema analysis using slopes of long and short emas
        /// Needs to be passed current candles, and current emas and ema-slopes need calculating
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
                                          bool slopes,
                                          double bDiffP,
                                          double sDiffP,
                                          bool bTrigger,
                                          bool sTrigger,
                                          int[] periods,
                                          ref ConcurrentDictionary<string, Candle> currentCandles,
                                          ref Dictionary<int, Ema> currEmas,
                                          ref Dictionary<int, Ema> currEmaSlopes,
                                          ref ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentStack<Ema>>> emaSlopes )
        {
            Product = product;
            Slopes = slopes;
            BDiffP = bDiffP;
            SDiffP = sDiffP;
            BTrigger = bTrigger;
            STrigger = sTrigger;
            Periods = periods;
            CurrentCandles = currentCandles;
            CurrEmas = currEmas;
            CurrEmaSlopes = currEmaSlopes;
            EmaSlopes = emaSlopes;
            
        }

        public string Product { get; }
        public bool Slopes { get; }
        public double BDiffP { get; }
        public double TStartP { get; }
        public double BTurnP { get; }
        public double STurnP { get; }
        public double BTooLateP { get; }
        public double STooLateP { get; }
        public int[] Periods { get; }
        public double SDiffP { get; }
        public bool BTrigger { get; }
        public bool STrigger { get; }
        public ConcurrentDictionary<string, Candle> CurrentCandles { get; }
        public Dictionary<int, Ema> CurrEmaSlopes { get; }
        public ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentStack<Ema>>> EmaSlopes { get; }
        public Dictionary<int, Ema> CurrEmas { get; }
        public ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentStack<Ema>>> Emas { get; }
    }

    public class SingleEmaAnalysisSettings
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="product"></param>
        /// <param name="sOffSP">Selloff slope %</param>
        /// <param name="sOffSSP">Selloff slope rate %</param>
        /// <param name="sOffPeakRP">% return from peak for selloff</param>
        /// <param name="bS1">Buy slope %</param>
        /// <param name="bS2">Buy slope rate %</param>
        /// <param name="bPeakRP">% return from bottom for sell</param>
        /// <param name="sS1">Sell slope %</param>
        /// <param name="sS2">Sell slope rate %</param>
        /// <param name="sPeakRP">% return from peak for sell</param>
        /// <param name="bTrigger">True --> analysis can suggest buys</param>
        /// <param name="strigger">True --> analysis can suggest sells</param>
        /// <param name="sOffTrigger">True --> analysis can suggest selloff</param>
        /// <param name="emaLength">Length of ema to be used</param>
        /// <param name="currentCandles"></param>
        /// <param name="emas"></param>
        /// <param name="emaSlopes"></param>
        public SingleEmaAnalysisSettings( string product,
                                          double sOffSP,
                                          double sOffSSP,
                                          double sOffPeakRP,
                                          double sOffPeakWindow,
                                          double bS1,
                                          double bS2,
                                          bool onlyBS1,
                                          bool onlyBS2,
                                          double bPeakRP,
                                          double bPeakWindow,
                                          double sS1,
                                          double sS2,
                                          bool onlySS1,
                                          bool onlySS2,
                                          double sPeakRP,
                                          double sPeakWindow,
                                          bool bTrigger,
                                          bool strigger,
                                          bool sOffTrigger,
                                          double slopeRateAvgLength,
                                          int emaLength,
                                          ref ConcurrentDictionary<string, Candle> currentCandles,
                                          ref ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentStack<Ema>>> emas,
                                          ref ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentStack<Ema>>> emaSlopes
                                          )
        {
            Product = product;
            SOffSP = sOffSP;
            SOffSSP = sOffSSP;
            SOffPeakRP = sOffPeakRP;
            SOffPeakWindow = sOffPeakWindow;
            BS1 = bS1;
            BS2 = bS2;
            OnlyBS1 = onlyBS1;
            BS2Override = onlyBS2;
            BPeakRP = bPeakRP;
            BPeakWindow = bPeakWindow;
            SS1 = sS1;
            SS2 = sS2;
            OnlySS1 = onlySS1;
            OnlySS2 = onlySS2;
            SPeakRP = sPeakRP;
            SPeakWindow = sPeakWindow;
            BTrigger = bTrigger;
            STrigger = strigger;
            SOffTrigger = sOffTrigger;
            SlopeRateAvgP = slopeRateAvgLength;
            EmaLength = emaLength;
            CurrentCandles = currentCandles;
            PrevEmas = new LimitedDateTimeList<Ema>( emas[ product ][ emaLength ], emaSlopes[ product ][ emaLength ].Count );
            PrevEmaSlopes = new LimitedDateTimeList<Ema>( emaSlopes[ product ][ emaLength ], emaSlopes[ product ][ emaLength ].Count );
        }

        public string Product { get; }
        public double SOffSP { get; }
        public double SOffSSP { get; }
        public double SOffPeakRP { get; }
        public double SOffPeakWindow { get; }
        public double BS1 { get; }
        public double BS2 { get; }
        public bool OnlyBS1 { get; }
        public bool BS2Override { get; }
        public double BPeakRP { get; }
        public double BPeakWindow { get; }
        public double SS1 { get; }
        public double SS2 { get; }
        public bool OnlySS1 { get; }
        public bool OnlySS2 { get; }
        public double SPeakRP { get; }
        public double SPeakWindow { get; }
        public bool BTrigger { get; }
        public bool STrigger { get; }
        public bool SOffTrigger { get; }
        public double SlopeRateAvgP { get; }
        public int EmaLength { get; }

        public ConcurrentDictionary<string, Candle> CurrentCandles { get; }
        public LimitedDateTimeList<Ema> PrevEmas { get; set; }
        public LimitedDateTimeList<Ema> PrevEmaSlopes { get; set; }
    }
}
