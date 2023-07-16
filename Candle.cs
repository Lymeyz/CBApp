using System;
using System.Globalization;
using System.Text.Json.Serialization;

namespace CBApp1
{
    public class Candle : ITimeBound
    {
        [JsonConstructor]
        public Candle( string start, double low, double high, double open, double close, double volume )
        {
            CultureInfo culture = new CultureInfo( "En-Us" );
            //Time = DateTime.Parse( start );
            //Low = double.Parse( low, culture );
            //High = double.Parse( high, culture );
            //Open = double.Parse( open, culture );
            //Close = double.Parse( close, culture );
            //Volume = double.Parse( volume, culture );
            //Time = DateTime.Parse( start );
            Time = new DateTime( 1970, 1, 1 ).AddSeconds( int.Parse(start) );
            Low = low;
            High = high;
            Open = open;
            Close = close;
            Volume = volume;
        }
        internal Candle(DateTime dateTime, string low, string high, string open, string close, string volume)
        {
            //Time = DateTime.Parse(time);
            //Time = DateTime.ParseExact(time, "yyyy-MM-ddTHH:mm:ss.ffffffZ",
            //    CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
            Time = dateTime;
            Low = double.Parse(low, new CultureInfo("En-Us"));
            High = double.Parse(high, new CultureInfo("En-Us"));
            avg = (High + Low) / 2.0;
            Open = double.Parse(open, new CultureInfo("En-Us"));
            Close = double.Parse(close, new CultureInfo("En-Us"));
            Volume = double.Parse(volume, new CultureInfo("En-Us"));
        }
        internal Candle(DateTime dateTime, double lowDouble, double highDouble, double openDouble, double closeDouble,
                    double volumeDouble)
        {
            Time = dateTime;
            Low = lowDouble;
            High = highDouble;
            avg = (highDouble + lowDouble) / 2.0;
            Open = openDouble;
            Close = closeDouble;
            Volume = volumeDouble;
        }

        internal Candle( Candle candle )
        {
            Time = candle.Time;
            Low = candle.Low;
            High = candle.High;
            avg = candle.Avg;
            Open = candle.Open;
            Close = candle.Close;
            Volume = candle.Volume;
        }



        public DateTime Time { get; set; }

        public double High
        {
            get
            {
                return high;
            }
            set
            {
                high = value;
                avg = (high + low) / 2.0;
            }
        }

        public double Low
        {
            get
            {
                return low;
            }
            set
            {
                low = value;
                avg = (high + low) / 2.0;
            }
        }

        public double Avg
        {
            get
            {
                return avg;
            }
        }
        public double Open { get; set; }
        public double Close { get; set; }
        public double Volume { get; set; }

        private double avg;
        private double high;
        private double low;
    }
}