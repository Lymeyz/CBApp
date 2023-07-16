using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CBApp1
{
    public class LimitOrder
    {
        public LimitOrder( string id,
                          string productId,
                          string orderSide,
                          OrderConfiguration config)
        {
            this.client_order_id = id;
            this.product_id = productId;
            this.side = orderSide;
            order_configuration = config;
        }
        public string product_id { get; }
        public string side { get; }
        public OrderConfiguration order_configuration { get; }
        public string client_order_id { get; }
    }

    public class OrderConfiguration
    {
        [JsonConstructor]
        public OrderConfiguration(LimitGtc limit_limit_gtc)
        {
            this.limit_limit_gtc = limit_limit_gtc;
        }

        public LimitGtc limit_limit_gtc { get; }
    }

    public class LimitGtc
    {
        [JsonConstructor]
        public LimitGtc(string base_size, string limit_price, bool post_only)
        {
            this.base_size = base_size;
            this.limit_price = limit_price;
            this.post_only = post_only;
        }

        public string base_size { get; }
        public string limit_price { get; }
        public bool post_only { get; }
    }
}
