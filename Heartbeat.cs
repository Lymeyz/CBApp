using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CBApp1
{
    public class Heartbeat
    {
        public Heartbeat(string type, string sequence, string last_trade_id, string product_id, string time)
        {
            Type = type;
            Sequence = sequence;
            Last_Trade_Id = int.Parse(last_trade_id);
            Product_Id = product_id;
            Time = time;
        }

        public string Type { get; }
        public string Sequence { get; }
        public int Last_Trade_Id { get; }
        public string Product_Id { get; }
        public string Time { get; }
    }
}
