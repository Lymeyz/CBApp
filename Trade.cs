using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CBApp1
{
    public class Trade
    {
        public Trade(string trade_id, string side, string size, string price, string time)
        {
            Trade_Id = int.Parse(trade_id);
            Side = side;
            Size = double.Parse(size, new CultureInfo("En-Us"));
            Price = double.Parse(price, new CultureInfo("En-Us"));
            Time = time;
        }

        public int Trade_Id { get; }
        public string Side { get; }
        public double Size { get; }
        public double Price { get; }
        public string Time { get; }
    }
}
