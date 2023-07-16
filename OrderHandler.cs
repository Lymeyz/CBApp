using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Timers;
using WebSocketSharp;
using Newtonsoft.Json;

namespace CBApp1
{
    public class OrderHandler
    {
        // Needs to read product-info from file...
        public OrderHandler(ref System.Timers.Timer timer1, TradeResources resources)
        {
            _tracker = new OrderTracker(ref reqMaker, profile_id, product_id);

            this.bounds = resources.Bounds;

            timer1.Elapsed += OnTimedEvent;

            this.product_id = resources.ProductId;
            this.profile_id = resources.ProfileId;
            this.reqMaker = resources.ReqMaker;

            baseAndQuoteAccounts = new Account[2];

            GetBalances();
        }

        private PriceHandler pHandler;
        private MessageReciever _mReciever;
        private readonly string product_id;
        private readonly string profile_id;
        private readonly RequestMaker reqMaker;
        private readonly OrderTracker _tracker;
        private InfoFetcher infoFetcher;
        private ProductInfo product;
        private Account[] baseAndQuoteAccounts;

        public OrderBounds bounds { get; set; }

        //private readonly OrderLogger _logger;
        private int minuteCounter;

        public MessageReciever MReciever
        {
            get
            {
                return _mReciever;
            }
        }

        private bool TryCalculateOrders(ref LimitOrder[] orders, int validity)
        {
            try
            {
                double lowestPrice = Double.MaxValue;
                double highestPrice = 0;
                double lowOrderPrice;
                double highOrderPrice;
                double size;

                string sizeString;
                string lowOrderString;
                string highOrderString;
                DateTime dTime = DateTime.Now;
                DateTime dTime2 = DateTime.Now.AddHours(-1).AddHours(-1).AddMinutes(-validity);

                GetBalances();

                List<ProductPrice> relevantPrices = pHandler.Prices.Where(p =>
                                                p.dTime > DateTime.Now.AddHours(-1).AddMinutes(-validity))
                                                .Select(p => p)
                                                .ToList();

                //Find highest and lowest prices in relevant interval
                foreach (var Price in relevantPrices)
                {
                    if (Price.PriceDouble < lowestPrice)
                    {
                        lowestPrice = Price.PriceDouble;
                    }
                    if (Price.PriceDouble > highestPrice)
                    {
                        highestPrice = Price.PriceDouble;
                    }
                }
                double interval = Math.Abs(highestPrice - lowestPrice);
                Console.WriteLine($"Order interval is: {Math.Round(interval, 4)} EUR");
                // compare with current prize and determine which orders are to be sent
                // order set to null if not in question
                // OBS - FIRST PRICE TO DEQUEUE IS LAST IN QUEUE AND OLDEST
                double currentPrice = -1;
                size = 0;

                while(currentPrice == -1)
                {
                    currentPrice = pHandler.TryPeekLastPrice();
                }
                //lowOrderPrice = Math.Round((lowestPrice + (bounds.BuyPercent * interval)), product.QuotePrecision)
                lowOrderPrice = Math.Round((lowestPrice + (0.12 * interval)), product.QuotePrecision);

                //(Math.Abs(lowestPrice - currentPrice) > (bounds.BuyLimit * interval)
                if (Math.Abs(lowestPrice - currentPrice) > (bounds.BuyLimit * interval))
                {
                    //size = Math.Round(bounds.QuoteSize / lowOrderPrice, product.BasePrecision)
                    size = Math.Round(bounds.QuoteSize / lowOrderPrice, product.BasePrecision);

                    if (size < product.BaseMinSize)
                    {
                        size = product.BaseMinSize;
                    }

                    lowOrderString = lowOrderPrice.ToString("");
                    sizeString = size.ToString($"F{product.BasePrecision}");

                    Console.WriteLine($"Calculated buy-price: {lowOrderPrice}");
                    if (!SimilarOrderExists(lowOrderPrice, interval, bounds.BuyDiff))
                    {
                        orders[0] = new LimitOrder(profile_id, product_id, "limit", "buy", "co",
                        lowOrderString, sizeString, "GTC", "hour", true, lowOrderPrice);
                    }
                }
                else
                {
                    orders[0] = null;
                }

                //highOrderPrice = Math.Round((highestPrice - (bounds.SellPercent * interval)), product.QuotePrecision)
                highOrderPrice = Math.Round((highestPrice - (bounds.SellPercent * interval)), product.QuotePrecision);

                //Math.Abs(highestPrice - currentPrice) > (bounds.SellLimit * interval)
                if (Math.Abs(highestPrice - currentPrice) > (bounds.SellLimit * interval))
                {
                    if (orders[0] == null)
                    {
                        //size = Math.Round(bounds.QuoteSize / highOrderPrice, product.BasePrecision)
                        size = Math.Round(bounds.QuoteSize / highOrderPrice, product.BasePrecision);
                    }
                    if (size < product.BaseMinSize)
                    {
                        size = product.BaseMinSize;
                    }
                    if ((baseAndQuoteAccounts[0].AvailableDouble < 2 * size) && baseAndQuoteAccounts[0].AvailableDouble > 0)
                    {
                        size = baseAndQuoteAccounts[0].AvailableDouble;
                    }

                    highOrderString = highOrderPrice.ToString("");
                    sizeString = size.ToString($"F{product.BasePrecision}");

                    Console.WriteLine($"Calculated sell-price: {highOrderPrice}");

                    if (!SimilarOrderExists(highOrderPrice, interval, bounds.SellDiff))
                    {
                        orders[1] = new LimitOrder(profile_id, product_id, "limit", "sell", "co",
                        highOrderString, sizeString, "GTC", "hour", true, highOrderPrice);
                    }
                }
                else
                {
                    orders[1] = null;
                }

                if (orders[0] != null || orders[1] != null)
                {
                    //0.005 is fee rate
                    if (Math.Round
                        ((highOrderPrice * size) - (lowOrderPrice * size), product.QuotePrecision) 
                        >
                        Math.Round
                        ((highOrderPrice * size * 0.005) + (lowOrderPrice * size * 0.005), product.QuotePrecision))
                    {
                        return true;
                    }
                    else
                    {
                        Console.WriteLine($"Interval between {Math.Round(lowOrderPrice, product.QuotePrecision)} " +
                            $"and {Math.Round(highOrderPrice, product.QuotePrecision)} doesn't profit");
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }
        }

        private bool SimilarOrderExists(double price, double interval, double range)
        {
            List<OrderInfo> similar = _tracker.NotCanceledOrders.Where(o => Math.Abs(o.Price - price) <= interval * range).ToList();
            if (similar.Count>0)
            {
                foreach (var order in similar)
                {
                    Console.WriteLine($"Similar order found: {order.Id} ---{order.Side}: {order.Price} --- Cancels at {order.CancelAt.ToString()}");
                }
                return true;
            }
            else
            {
                return false;
            }

        }
        private void CopyAndSendOrders(params int[] validities)
        {
            if (pHandler.TryCopyPrices(5))
            {
                foreach (int validity in validities)
                {
                    TrySendOrders(validity);
                }
            }
        }
        private void TrySendOrders(int validity)
        {
            try
            {
                if (pHandler.Prices != null)
                {
                    LimitOrder[] orders = new LimitOrder[2];

                    if (TryCalculateOrders(ref orders, validity))
                    {
                        if (SendOrders(ref orders, validity))
                        {
                            Console.WriteLine("Sent an order");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
        private bool SendOrders(ref LimitOrder[] orders, int validity)
        {
            string orderString;
            bool sentBuyOrder;
            bool sentSellOrder;

            if (orders[0] != null)
            {
                orderString = JsonConvert.SerializeObject(orders[0]);

                IRestResponse lowResp = reqMaker.SendOrderRequest(orderString);

                if (lowResp.IsSuccessful)
                {
                    OrderInfo orderInfo = JsonConvert.DeserializeObject<OrderInfo>(lowResp.Content);
                    orderInfo.Validity = validity;

                    _tracker.TrackOrder(orderInfo);

                    Console.WriteLine($"Sent {orderInfo.Id} --- {orderInfo.Side}: {orderInfo.Price} --- Cancels: {orderInfo.CancelAt.ToString()}");
                    sentBuyOrder = true;
                }
                else
                {
                    Console.WriteLine($"Buy-order failed: {lowResp.Content}");
                    sentBuyOrder = false;
                }
            }
            else
            {
                sentBuyOrder = false;
            }

            if (orders[1] != null)
            {
                orderString = JsonConvert.SerializeObject(orders[1]);

                IRestResponse highResp = reqMaker.SendOrderRequest(orderString);

                if (highResp.IsSuccessful)
                {
                    OrderInfo orderInfo = JsonConvert.DeserializeObject<OrderInfo>(highResp.Content);
                    orderInfo.Validity = validity;

                    _tracker.TrackOrder(orderInfo);

                    Console.WriteLine($"Sent {orderInfo.Id} --- {orderInfo.Side}: {orderInfo.Price} --- Cancels: {orderInfo.CancelAt.ToString()}");
                    sentSellOrder = true;
                }
                else
                {
                    Console.WriteLine($"Sell-order failed: {highResp.Content}");
                    sentSellOrder = false;
                }
            }
            else
            {
                sentSellOrder = false;
            }

            if (sentBuyOrder || sentSellOrder)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        
        private bool RefreshOrders(DateTime now)
        {
            try
            {
                if (_tracker.hasOrders(now))
                {
                    List<OrderInfo> orders = _tracker.UpdateTracker(now);

                    foreach (var order in orders)
                    {
                        if (order.CancelAt.Hour == now.Hour &&
                            order.CancelAt.Minute == now.Minute &&
                            Math.Abs(order.CancelAt.Second - now.Second)<10)
                        {
                            Console.WriteLine($"Canceling {order.Id} --- {order.Side} --- {order.Price}");
                            reqMaker.SendCancelRequest(order.Id, profile_id);
                        }
                    }
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
                return false;
            }
        }

        private void GetBalances()
        {
            try
            {
                List<Account> accounts =
                JsonConvert.DeserializeObject<List<Account>>(reqMaker.GetAccountsRequest().Content);

                foreach (Account account in accounts)
                {
                    if (account.Currency == product.BaseCurrency)
                    {
                        baseAndQuoteAccounts[0] = account;
                    }
                    if (account.Currency == product.QuoteCurrency)
                    {
                        baseAndQuoteAccounts[1] = account;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
            

            
        }

        //make sure interval nvm
        private void SendOrdersOnTime(int counter, int minutes, int interval)
        {
            for (int i = 0; i < minutes; i+=interval)
            {
                if (counter >= minutes && ((counter - i) % minutes == 0))
                {
                    CopyAndSendOrders(minutes);
                }
            }
        }
        private void OnTimedEvent(Object source, ElapsedEventArgs e)
        {
            minuteCounter++;
            Console.WriteLine($"Runtime: {minuteCounter} minutes");
            DateTime now = new DateTime();
            now = DateTime.Now;

            //Update order-tracking
            RefreshOrders(now);

            SendOrdersOnTime(minuteCounter, 180, 20);
        }
    }
}
