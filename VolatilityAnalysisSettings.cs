using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CBApp1
{
    public class VolatilityAnalysisSettings
    {
        public VolatilityAnalysisSettings( string product,
                                           bool slopeBased,
                                           int length,
                                           double ignoreFactor,
                                           ConcurrentDictionary<string, Candle> currentCandles,
                                           ref Dictionary<string, Queue<Candle>> candles,
                                           ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentStack<Ema>>> emas,
                                           ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentStack<Ema>>> emaSlopes,
                                           Ema latestEma)
        {
            Product = product;
            SlopeBased = slopeBased;
            Length = length;
            double ignoreLength = 1 + (ignoreFactor * length);
            IgnoreAfterPeak = Convert.ToInt32(Math.Round(ignoreLength, 0));
            CurrentCandles = currentCandles;
            Candles = new LimitedDateTimeList<Candle>( candles[ product ], candles[ product ].Count );
            if( !slopeBased )
            {
                
                Emas = new Dictionary<int, LimitedDateTimeList<Ema>>();

                for( int i = 0; i < 2; i++ )
                {
                    Emas[ Length ] = new LimitedDateTimeList<Ema>( emas[ product ][ Length ], 
                        emas[ product ][ Length ].Count );
                }
            }
            else
            {
                EmaSlopes = new LimitedDateTimeList<Ema>( emaSlopes[ product ][ Length ],
                    emaSlopes[ product ][ Length].Count );
                Ema lastSlopeHolder;
                emaSlopes[ product ][ length ].TryPeek( out lastSlopeHolder );
                LastEma = latestEma;
                LastEmaSlope = lastSlopeHolder;
            }
        }

        public string Product { get; set; }
        public bool SlopeBased { get; }
        public int Length { get; }
        public int IgnoreAfterPeak { get; }
        public ConcurrentDictionary<string, Candle> CurrentCandles { get; }
        public LimitedDateTimeList<Candle> Candles { get; }
        public Dictionary<int, LimitedDateTimeList<Ema>> Emas { get; set; }
        public Ema LastEma { get; }
        public Ema LastEmaSlope { get;  }
        public LimitedDateTimeList<Ema> EmaSlopes { get; set; }
    }
}
