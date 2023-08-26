using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CBApp1
{
    public class SingleEmaAnalysisResult
    {
        public SingleEmaAnalysisResult( int slopeRateAverageLength )
        {
            Complete = false;
            SellOff = false;
            SellOk = false;
            BuyOk = false;
            Time = DateTime.MinValue;
            LastUpdate = DateTime.MinValue;
            SlopeRates = new LinkedList<double>();
            average = 0;
            SlopeRateAverageLength = slopeRateAverageLength;
        }

        public void UpdateSlopeRateAverage(double slopeRate)
        {
            try
            {
                SlopeRates.RemoveFirst();
                SlopeRates.AddFirst( slopeRate );
            }
            catch( Exception e )
            {
                throw new Exception( e.Message + '\n'+ e.StackTrace );
            }
            
        }

        public bool Trend { get; set; } // true = short ema over long ema, positive
        public double SlopeRateAverage
        {
            get
            {
                if( SlopeRates.Count > SlopeRateAverageLength )
                {
                    for( int i = 0; i < SlopeRates.Count - SlopeRateAverageLength; i++ )
                    {
                        SlopeRates.RemoveLast();
                    }

                    average = 0;

                    foreach( var rate in SlopeRates )
                    {
                        average += rate;
                    }

                    average = average / SlopeRateAverageLength;

                    return average;
                }
                else
                {
                    average = 0;

                    foreach( var rate in SlopeRates )
                    {
                        average += rate;
                    }

                    average = average / SlopeRateAverageLength;

                    return average;
                }
                
            }
        }
        public LinkedList<double> SlopeRates { get; set; }
        public bool SellOk { get; set; }
        public bool BuyOk { get; set; }
        public bool SellOff { get; set; }
        public bool Complete { get; set; }
        public double Price { get; set; }
        public double StartPrice { get; set; } 
        public DateTime Time { get; set; } // start of trend
        public DateTime LastUpdate { get; set; }
        public int SlopeRateAverageLength { get; set; }

        private double average;
    }
}
