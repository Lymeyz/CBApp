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
                                           int volatilityLength,
                                           bool slopeBased,
                                           int[] lengths,
                                           ConcurrentDictionary<string, Candle> currentCandles,
                                           ref Dictionary<string, Queue<Candle>> candles,
                                           ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentStack<Ema>>> emas,
                                           ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentStack<Ema>>> emaSlopes )
        {
            VolatilityLength = volatilityLength;
            SlopeBased = slopeBased;
            Lengths = lengths;
            CurrentCandles = currentCandles;
            Candles = new LimitedDateTimeList<Candle>( candles[ product ], candles[ product ].Count );
            if( !slopeBased )
            {
                
                Emas = new Dictionary<int, LimitedDateTimeList<Ema>>();

                for( int i = 0; i < 2; i++ )
                {
                    Emas[ Lengths[ i ] ] = new LimitedDateTimeList<Ema>( emas[ product ][ Lengths[ i ] ], 
                        emas[ product ][ Lengths[ i ] ].Count );
                }
            }
            else
            {
                EmaSlopes = new LimitedDateTimeList<Ema>( emaSlopes[ product ][ Lengths[ 0 ] ], 
                    emas[ product ][ Lengths[ 0 ] ].Count );
                Ema lastSlopeHolder;
                emas[ product ][ lengths[ 0 ] ].TryPeek( out lastSlopeHolder );
                LastSlopeEma = lastSlopeHolder;
            }
        }

        public string Product { get; set; }
        public int VolatilityLength { get; }
        public bool SlopeBased { get; }
        public int[] Lengths { get; }
        public ConcurrentDictionary<string, Candle> CurrentCandles { get; }
        public LimitedDateTimeList<Candle> Candles { get; }
        public Dictionary<int, LimitedDateTimeList<Ema>> Emas { get; set; }
        public Ema LastSlopeEma { get;  }
        public LimitedDateTimeList<Ema> EmaSlopes { get; set; }
    }
}
