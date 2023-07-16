using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CBApp1
{
    public class Match
    {
        public Match(string product_id, string price, string time, string size, string trade_id)
        {
            Product_Id = product_id;
            Price = price;
            Time = time;
            Size = size;
            Trade_Id = int.Parse(trade_id);
        }

        public string Product_Id { get; }
        public string Price { get; }
        public string Time { get; }
        public string Size { get; }
        public int Trade_Id { get; }
    }
}
