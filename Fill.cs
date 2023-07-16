using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CBApp1
{
    public class Fill
    {
        public Fill(string order_id, string size, string product_id, string trade_id )
        {
            Order_Id = order_id;
            Size = double.Parse( size, new CultureInfo( "En-Us" ) );
            Product_Id = product_id;
            Trade_Id = trade_id;
        }

        public string Order_Id { get; }
        public double Size { get; set; }
        public string Product_Id { get; }
        public string Trade_Id { get; set; }

    }

    public class FillsHolder
    {
        public FillsHolder( Fill[] fills)
        {
            Fills = fills;
        }

        public Fill[] Fills { get; }
    }
}
