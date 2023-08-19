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
                                           ref ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentStack<Ema>>> emas,
                                           ref ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentStack<Ema>>> emaSlopes )
        {
            VolatilityLength = volatilityLength;
            SlopeBased = slopeBased;
            Lengths = lengths;

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
            }
        }

        public string Product { get; set; }
        public int VolatilityLength { get; }
        public bool SlopeBased { get; }
        public int[] Lengths { get; }

        public Dictionary<int, LimitedDateTimeList<Ema>> Emas { get; set; }
        public LimitedDateTimeList<Ema> EmaSlopes { get; set; }
    }
}
