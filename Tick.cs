using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CBApp1
{
    public class Tick
    {
        public Tick( string product_id,
                    string price )
        {
            Product_Id = product_id;
            Price = double.Parse( price, new CultureInfo( "En-Us" ) );
        }

        public string Product_Id { get; }
        public double Price { get; }
    }
}
