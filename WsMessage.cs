using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CBApp1
{
    public class WsMessage
    {
        [JsonConstructor]
        public WsMessage( string channel,
                         WsMessageEvent[] events )
        {
            Channel = channel;
            Events = events;
        }

        public string Channel { get; }
        public WsMessageEvent[] Events { get; }
    }

    public class WsMessageEvent
    {
        [JsonConstructor]
        public WsMessageEvent(string type, Tick[] tickers, Match[] trades, WsOrder[] orders)
        {
            Type = type;

            if( trades != null )
            {
                Trades = trades;
            }
            if( tickers != null )
            {
                Tickers = tickers;
            }
            if( orders != null )
            {
                Orders = orders;
            }
            
        }

        public string Type { get; }
        public WsOrder[] Orders { get; }
        public Tick[] Tickers1 { get; }
        public Match[] Trades { get; }
        public Tick[] Tickers { get; }
    }

    public class WsOrder
    {
        public WsOrder( string order_id,
                       string client_order_id,
                       string status,
                       string product_id,
                       string cumulative_quantity,
                       string leaves_quantity,
                       string order_side,
                       string creation_time,
                       string total_fees)
        {
            Order_Id = order_id;
            Client_Order_Id = client_order_id;
            Status = status;
            Product_Id = product_id;
            Cumulative_Quantity = cumulative_quantity;
            Leaves_Quantity = leaves_quantity;
            Order_Side = order_side;
            Creation_Time = creation_time;
            Total_Fees = total_fees;
        }

        public string Order_Id { get; }
        public string Client_Order_Id { get; }
        public string Status { get; }
        public string Product_Id { get; }
        public string Cumulative_Quantity { get; }
        public string Leaves_Quantity { get; }
        public string Order_Side { get; }
        public string Creation_Time { get; }
        public string Total_Fees { get; }
    }
}
