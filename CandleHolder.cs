using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CBApp1
{
    public class CandleHolder<T>
    {

        [JsonConstructor]
        public CandleHolder( List<T> candles )
        {
            Candles = new List<T>( candles );
        }

        public List<T> Candles { get; set; }
    }
}
