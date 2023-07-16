using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace CBApp1
{
    public class FillInfo : ITimeBound
    {
        [JsonConstructor]
        public FillInfo(string product_id, string trade_id, string order_id, string price, string size, string side, string fee, string created_at, string Time)
        {
            Product_Id = product_id;
            Trade_Id = trade_id;
            Order_Id = order_id;
            Price = double.Parse(price, new CultureInfo("En-Us"));
            Size = double.Parse(size, new CultureInfo("En-Us"));
            Side = side;
            Fee = double.Parse(fee, new CultureInfo("En-Us")); ;
            if( Time != null )
            {
                this.Time = DateTime.Parse( Time ).ToUniversalTime();
            }
            if( created_at != null )
            {
                this.Time = DateTime.Parse( created_at ).ToUniversalTime();
            }
            
            // if created_at !=null, if string time !=null
        }

        public FillInfo( FillInfo fill )
        {
            this.Product_Id = fill.Product_Id;
            this.Trade_Id = fill.Trade_Id;
            this.Order_Id = fill.Order_Id;
            this.Price = fill.Price;
            this.Size = fill.Size;
            this.Side = fill.Side;
            this.Fee = fill.Fee;
            this.Time = fill.Time;
        }

        public string Product_Id { get; }
        public string Trade_Id { get; }
        public string Order_Id { get; }
        public double Price { get; set; }
        public double Size { get; set; }
        public string Side { get; }
        public double Fee { get; }
        public DateTime Time { get; }
    }
}
