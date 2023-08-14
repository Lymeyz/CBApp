using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CBApp1
{
    public class SingleEmaAnalysisResult
    {
        public SingleEmaAnalysisResult()
        {
            Complete = false;
            SellOff = false;
            SellOk = false;
            BuyOk = false;
            Time = DateTime.MinValue;
            LastUpdate = DateTime.MinValue;
            SlopeRates = new LinkedList<double>();
            count = 0;
            average = 0;
        }

        public void UpdateSlopeRateAverage(double slopeRate)
        {
            try
            {
                SlopeRates.RemoveFirst();
                count--;
                SlopeRates.AddFirst( slopeRate );
            }
            catch( Exception e )
            {
                throw new Exception( e.Message + '\n'+ e.StackTrace );
            }
            
        }

        public bool Trend { get; set; } // true = short ema over long ema, positive
        public double RateAverage
        {
            get
            {
                if( SlopeRates.Count > count )
                {
                    average = 0;
                    count = SlopeRates.Count;

                    foreach( var rate in SlopeRates )
                    {
                        average += rate;
                    }

                    return average / count;
                }
                else
                {
                    return average;
                }
                
            }
        }
        public int RateAverageCount
        {
            get
            {
                return SlopeRates.Count;
            }
        }
        public LinkedList<double> SlopeRates { get; set; }
        public bool SellOk { get; set; }
        public bool BuyOk { get; set; }
        public bool SellOff { get; set; }
        public bool Complete { get; set; }
        public double Price { get; set; }
        public DateTime Time { get; set; } // start of trend
        public DateTime LastUpdate { get; set; }

        private double average;
        private int count;
    }
}
