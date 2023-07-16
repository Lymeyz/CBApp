using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CBApp1
{
    public class TradeResources
    {
        public TradeResources(string product_id, string profile_id, string endPoint,
                                OrderBounds bounds, ref Authenticator authenticator)
        {
            
        }
        public string ProductId { get; }
        public string ProfileId { get; }

        public OrderBounds Bounds { get; set; }
        public ProductInfo Info { get; }
        public RequestMaker ReqMaker { get; }

        private RequestMaker requestMaker;
    }
}
