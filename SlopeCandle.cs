using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CBApp1
{
    class SlopeCandle : ITimeBound
    {
        public SlopeCandle(double upperSlope, double lowerSlope, double volumeSlope, DateTime start)
        {
            UpperSlope = upperSlope;
            LowerSlope = lowerSlope;
            AvgSlope = (upperSlope + lowerSlope) / 2;
            VolumeSlope = volumeSlope;
            Time = start;
        }
        public double UpperSlope { get; set; }
        public double LowerSlope { get; set; }
        public double AvgSlope { get; set; }
        public double VolumeSlope { get; set; }
        public DateTime Time { get; }

    }
}
