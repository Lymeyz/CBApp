using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RestSharp;
using Newtonsoft.Json;
using System.Timers;
using System.Globalization;

namespace CBApp1
{
    public class AsyncOrderTracker
    {
        private readonly object activeOrdersRoot = new object();
        private readonly object unMatchedOrdersRoot = new object();
        private readonly object messageRoot = new object();
        private readonly object dequeueRoot = new object();
        public AsyncOrderTracker( string[] productIds,
                              ref DataAnalyser analyser,
                              ref AsyncOrderLogger logger,
                              ref SynchronizedConsoleWriter writer,
                              ref RequestMaker reqMaker,
                              ref ConcurrentDictionary<string, ProductInfo> productInfos,
                              ref System.Timers.Timer aTimer,
                              double orderSpreadPercent )
        {
            try
            {
                this.fetcher = analyser.DataHandler.Fetcher;
                this.logger = logger;
                this.writer = writer;
                this.reqMaker = reqMaker;
                this.productInfos = productInfos;

                sentOrders = new ConcurrentDictionary<string, OrderInfoResponse>();
                pendingOrders = new ConcurrentDictionary<string, OrderInfo>();
                activeOrders = new ConcurrentDictionary<string, ConcurrentDictionary<string, OrderInfo>>();
                associatedOrders = new ConcurrentDictionary<string, string>();
                unMatchedOrders = new ConcurrentDictionary<string, ConcurrentDictionary<string, OrderInfo>>();
                messageQueue = new ConcurrentQueue<WsMessageEvent>();
                pendingAssociatedIds = new ConcurrentDictionary<string, string>();
                recentCancels = new ConcurrentQueue<string>();
                recentFills = new ConcurrentQueue<WsOrder>();

                foreach( string product in productIds )
                {
                    // initalize collections.......
                    activeOrders[ product ] = new ConcurrentDictionary<string, OrderInfo>();
                    unMatchedOrders[ product ] = new ConcurrentDictionary<string, OrderInfo>();
                }

                this.orderSpreadPercent = orderSpreadPercent;
                processingMessages = false;
                culture = new CultureInfo( "En-Us" );

                // retrieve logs
                RetrieveLogs();

                Thread.Sleep( 1000 );

                aTimer.Elapsed += this.OnTimedEvent;
                fetcher.UserChannelUpdate += this.UserChannelUpdate;
                fetcher.SubscribeToUserChannel();
            }
            catch( Exception e )
            {
                Console.WriteLine( e.StackTrace );
                Console.WriteLine( e.Message );
            }
        }

        private void RetrieveLogs()
        {
            try
            {
                // copy any logged active orders
                if( logger.FileActiveOrders != null )
                {
                    foreach( var pair in logger.FileActiveOrders )
                    {
                        foreach( var innerPair in pair.Value )
                        {
                            activeOrders[ pair.Key ][ innerPair.Key ] = innerPair.Value;
                        }
                    }
                }

                // copy any logged pending orders
                if( logger.FilePendingOrders != null )
                {
                    foreach( var pair in logger.FilePendingOrders )
                    {
                        pendingOrders[ pair.Key ] = pair.Value;
                    }
                }

                // copy any logged unmatched orders
                if( logger.FileUnMatchedOrders != null )
                {
                    foreach( var pair in logger.FileUnMatchedOrders )
                    {
                        foreach( var innerPair in pair.Value )
                        {
                            unMatchedOrders[ pair.Key ][ innerPair.Key ] = innerPair.Value;
                        }
                    }
                }

                // copy any associated order ids
                if( logger.FileAssociatedOrders != null )
                {
                    foreach( var pair in logger.FileAssociatedOrders )
                    {
                        associatedOrders[ pair.Key ] = pair.Value;
                    }
                }

                if( logger.FilePendingAssociated != null )
                {
                    pendingAssociatedIds = logger.FilePendingAssociated;
                }
            }
            catch( Exception e)
            {
                Console.WriteLine( e.StackTrace );
                Console.WriteLine( e.Message );
            }
        }

        public async Task AddOrder(OrderInfoResponse orderInfo, string[] associatedOrderIds)
        {
            await Task.Run( () =>
            {
                if( associatedOrderIds != null )
                {
                    if( associatedOrderIds.Length > 0 )
                    {
                        foreach( var id in associatedOrderIds )
                        {
                            pendingAssociatedIds[ id ] = orderInfo.Success_Response.Client_Order_Id;
                        }

                        logger.LogPendingAssociated( pendingAssociatedIds );

                    }
                }
                
                sentOrders[ orderInfo.Success_Response.Client_Order_Id ] = orderInfo;
            });
        }

        public async Task<bool> CancelOrder(string orderProductId, string orderClientOrderId, string OrderId)
        {
            try
            {
                var orderIds = new { order_ids = new string[] { OrderId } };
                string jsonString = JsonConvert.SerializeObject( orderIds );

                RestResponse resp = reqMaker.SendAuthRequest( $@"api/v3/brokerage/orders/batch_cancel", Method.Post, jsonString );
                CancelResponse cancelResp = JsonConvert.DeserializeObject<CancelResponse>( resp.Content );

                OrderInfo removedPending;
                bool cancelled = false;

                if( cancelResp.Results[0].Success )
                {
                    // successful cancel request

                    // wait for 5 sec order id to be added to recentCancels

                    cancelled = false;

                    for( int i = 0; i < 30; i++ )
                    {

                        Thread.Sleep( 100 );

                        foreach( var id in recentCancels )
                        {
                            if( id == orderClientOrderId )
                            {
                                cancelled = true;
                                break;
                            }
                        }
                    }

                    if( cancelled )
                    {
                        if( pendingOrders.ContainsKey( orderClientOrderId ) )
                        {
                            pendingOrders.TryRemove( orderClientOrderId, out removedPending );
                        }
                    }
                }
                else
                {
                    cancelled = false;
                }

                return cancelled;
            }
            catch( Exception e)
            {
                Console.WriteLine( e.StackTrace );
                Console.WriteLine( e.Message );
                return false;
            }
        }

        /// <summary>
        /// Sends cancel request, waits for id to appear in recentCancels if successful.
        /// </summary>
        /// <param name="orderProductId"></param>
        /// <param name="orderClientOrderId"></param>
        /// <param name="OrderId"></param>
        /// <returns>Returns an OrderInfo if cancelled order was at least partly filled, otherwise returns null</returns>
        public async Task<OrderInfo> CancelReturnOrder( string orderProductId, string orderClientOrderId, string OrderId )
        {
            try
            {
                var orderIds = new { order_ids = new string[] { OrderId } };
                string jsonString = JsonConvert.SerializeObject( orderIds );

                RestResponse resp = reqMaker.SendAuthRequest( $@"api/v3/brokerage/orders/batch_cancel", Method.Post, jsonString );
                CancelResponse cancelResp = JsonConvert.DeserializeObject<CancelResponse>( resp.Content );

                OrderInfo removedPending;
                OrderInfo returnOrderInfo = null;

                if( cancelResp.Results[ 0 ].Success )
                {
                    // successful cancel request

                    // wait for 5 sec order id to be added to recentCancels

                    bool cancelled = false;

                    for( int i = 0; i < 20; i++ )
                    {

                        Thread.Sleep( 100 );

                        foreach( var id in recentCancels )
                        {
                            if( id == orderClientOrderId )
                            {
                                cancelled = true;
                                break;
                            }
                        }
                    }

                    if( cancelled )
                    {

                        if( pendingOrders.ContainsKey( orderClientOrderId ) )
                        {
                            pendingOrders.TryRemove( orderClientOrderId, out removedPending );
                        }

                        // if cancelled order was added to unMatched, it was partly filled and is returned
                        if( unMatchedOrders[ orderProductId ].ContainsKey( orderClientOrderId ) )
                        {
                            returnOrderInfo = unMatchedOrders[ orderProductId ][ orderClientOrderId ];
                        }

                    }
                }

                return returnOrderInfo;
            }
            catch( Exception e )
            {
                Console.WriteLine( e.StackTrace );
                Console.WriteLine( e.Message );
                return null;
            }
        }

        public async Task<bool> CancelAssociatedOrders( string productId, string unMatchedClientOrderId )
        {
            try
            {
                bool cancelled = false;
                bool isActive = false;
                string associatedClientId;
                string throwawayAssociated;
                OrderInfo associatedOrder;

                if( unMatchedOrders.ContainsKey( productId ) )
                {
                    if( unMatchedOrders[ productId ].ContainsKey( unMatchedClientOrderId ) )
                    {
                        if( associatedOrders.ContainsKey( unMatchedClientOrderId ) )
                        {
                            if( associatedOrders.TryGetValue( unMatchedClientOrderId, out associatedClientId ) )
                            {
                                foreach( var pair in activeOrders[ productId ] )
                                {
                                    if( pair.Key == associatedClientId )
                                    {
                                        isActive = true;
                                        if( activeOrders[ productId ].TryGetValue( associatedClientId, out associatedOrder ) )
                                        {
                                            if( await CancelOrder( productId, associatedClientId, associatedOrder.Order_Id ) )
                                            {
                                                if( associatedOrders.ContainsKey( unMatchedClientOrderId ) )
                                                {
                                                    associatedOrders.TryRemove( unMatchedClientOrderId, out throwawayAssociated );
                                                }
                                                cancelled = true;
                                            }
                                        }
                                    }
                                }
                                if( cancelled == false && isActive == false )
                                {
                                    if( associatedOrders.ContainsKey( unMatchedClientOrderId ) )
                                    {
                                        associatedOrders.TryRemove( unMatchedClientOrderId, out throwawayAssociated );
                                    }
                                }
                            }
                        }
                    }
                }

                logger.LogAssociatedOrders( associatedOrders );

                return cancelled;
            }
            catch( Exception e )
            {
                Console.WriteLine( e.StackTrace );
                Console.WriteLine( e.Message );
                return false;
            }
        }

        private async Task ProcessPendingOrderUpdate( WsOrder order )
        {
            try
            {

                // check for order in sentOrders, make new orderInfo and add to pendingOrders

                if( !activeOrders[ order.Product_Id ].ContainsKey( order.Client_Order_Id ) )
                {
                    int wait = 0;
                    while( wait < 3 )
                    {
                        if( sentOrders.ContainsKey( order.Client_Order_Id ) )
                        {

                            OrderInfoResponse sentOrder;

                            sentOrders.TryRemove( order.Client_Order_Id, out sentOrder );

                            // merge OrderInfoResponse and WsOrder
                            OrderInfo pendingOrder = new OrderInfo( order.Order_Id,
                                                                    order.Product_Id,
                                                                    sentOrder.Order_Configuration,
                                                                    order.Order_Side,
                                                                    order.Status,
                                                                    order.Creation_Time,
                                                                    order.Cumulative_Quantity,
                                                                    order.Total_Fees,
                                                                    order.Client_Order_Id
                                                                    );

                            //if( pendingOrder.Side == "SELL" )
                            //{
                            //    foreach( var pair in pendingAssociatedIds )
                            //    {
                            //        if( pair.Value == pendingOrder.ClientOrderId )
                            //        {
                            //            pendingOrder.AssociatedId = pair.Key;
                            //        }
                            //    }
                            //}

                            pendingOrders[ pendingOrder.ClientOrderId ] = pendingOrder;

                            await logger.LogPendingOrders( pendingOrders );

                            break;
                        }
                        else
                        {
                            Thread.Sleep( 1000 );
                            wait++;
                        }
                    }
                }
                else
                {
                    OrderInfo throwAway;
                    OrderInfoResponse throwAwayResp;
                    if( pendingOrders.ContainsKey( order.Client_Order_Id ) )
                    {
                        pendingOrders.TryRemove( order.Client_Order_Id, out throwAway );
                    }
                    if( sentOrders.ContainsKey( order.Client_Order_Id ) )
                    {
                        sentOrders.TryRemove( order.Client_Order_Id, out throwAwayResp );
                    }
                }
            }
            catch( Exception e)
            {
                Console.WriteLine( e.StackTrace );
                Console.WriteLine( e.Message );
            }
        }
        private async Task ProcessOpenOrderUpdate( WsOrder order, int tries )
        {
            try
            {
                bool canceled = false;
                bool filled = false;

                foreach( var id in recentCancels )
                {
                    if( order.Client_Order_Id == id )
                    {
                        canceled = true;
                        break;
                    }
                }

                foreach( var fill in recentFills )
                {
                    if( order.Client_Order_Id == fill.Client_Order_Id )
                    {
                        filled = true;
                    }
                }

                if( !canceled && !filled)
                {
                    if( activeOrders[ order.Product_Id ].ContainsKey( order.Client_Order_Id ) )
                    {
                        OrderInfo openOrderInfo;
                        activeOrders[ order.Product_Id ].TryRemove( order.Client_Order_Id, out openOrderInfo );

                        openOrderInfo.FilledSize = double.Parse( order.Cumulative_Quantity, culture );
                        openOrderInfo.Fee = double.Parse( order.Total_Fees, culture );

                        activeOrders[ order.Product_Id ][ order.Client_Order_Id ] = openOrderInfo;

                        await logger.LogActiveOrders( activeOrders );
                    }
                    else
                    {
                        // new order, merge with info from pendingOrders
                        if( pendingOrders.ContainsKey( order.Client_Order_Id ) )
                        {
                            OrderInfo pendingOrderInfo;
                            string associatedId = null;
                            List<string> associatedIds = new List<string>();
                            string throwaway;

                            pendingOrders.TryRemove( order.Client_Order_Id, out pendingOrderInfo );

                            RemoveAllPendingWithId( order.Client_Order_Id );

                            await logger.LogPendingOrders( pendingOrders );

                            pendingOrderInfo.Status = order.Status;
                            pendingOrderInfo.FilledSize = double.Parse( order.Cumulative_Quantity, culture );

                            if( pendingOrderInfo.Side == "SELL" )
                            {
                                foreach( var pair in pendingAssociatedIds )
                                {
                                    if( pair.Value == pendingOrderInfo.ClientOrderId )
                                    {
                                        associatedIds.Add( pair.Key );
                                    }
                                }

                                if( associatedIds.Count > 0)
                                {
                                    foreach( var id in associatedIds )
                                    {
                                        associatedOrders[ id ] = order.Client_Order_Id;

                                        pendingAssociatedIds.TryRemove( id, out throwaway );
                                    }

                                    await logger.LogAssociatedOrders( associatedOrders );
                                    await logger.LogPendingAssociated( pendingAssociatedIds );
                                }
                                else
                                {
                                    foreach( var pair in associatedOrders )
                                    {
                                        if( pair.Value == pendingOrderInfo.ClientOrderId )
                                        {
                                            associatedIds.Add( pair.Key );
                                        }
                                    }
                                    if( associatedIds.Count == 0 )
                                    {
                                        pendingOrderInfo = null;
                                    }
                                }
                            }

                            if( pendingOrderInfo != null )
                            {
                                activeOrders[ pendingOrderInfo.ProductId ][ pendingOrderInfo.ClientOrderId ] = pendingOrderInfo;

                                await logger.LogActiveOrders( activeOrders );
                            }
                            else
                            {
                                CancelOrder( order.Product_Id, order.Client_Order_Id, order.Order_Id );
                            }
                        }
                        // no pending order for "new" open? try get one... can't? cancel
                        else
                        {
                            if( tries < 3 )
                            {
                                FetchOrderToPending( order.Order_Id );
                                tries++;
                                ProcessOpenOrderUpdate( order, tries );
                            }
                            else
                            {
                                CancelOrder( order.Product_Id, order.Client_Order_Id, order.Order_Id );
                            }
                        }
                    }
                }
            }
            catch( Exception e )
            {
                Console.WriteLine( e.StackTrace );
                Console.WriteLine( e.Message );
            }
        }

        private void RemoveAllPendingWithId( string client_Order_Id )
        {
            try
            {
                OrderInfo throwAway;
                int count = pendingOrders.Count;

                for( int i = 0; i < count; i++ )
                {
                    if( pendingOrders.TryRemove( client_Order_Id, out throwAway ) )
                    {
                        count--;
                    }
                }
            }
            catch( Exception e )
            {
                Console.WriteLine( e.StackTrace );
                Console.WriteLine( e.Message );
            }
        }

        private void FetchOrderToPending( string order_Id )
        {
            try
            {
                RestResponse resp = reqMaker.SendAuthRequest( $@"api/v3/brokerage/orders/historical/{order_Id}",
                                                              Method.Get,
                                                              "" );
                if( resp.IsSuccessful )
                {
                    OrderHolder orderHolder = JsonConvert.DeserializeObject<OrderHolder>( resp.Content );
                    OrderInfo order = orderHolder.Order;
                    if( order.Status == "OPEN")
                    {
                        pendingOrders[ order.ClientOrderId ] = order;
                    }
                    else if( order.Status == "PENDING" )
                    {
                        pendingOrders[ order.ClientOrderId ] = order;
                    }
                    //else if( order.Status == "FILLED" )
                    //{
                    //    pendingOrders[ order.ClientOrderId ] = order;
                    //}
                    //else if( order.Status == "CANCELLED" )
                    //{
                    //    pendingOrders[ order.ClientOrderId ] = order;
                    //}

                    logger.LogPendingOrders( pendingOrders );
                }
                else
                {
                    throw new Exception( "?" );
                }
                
            }
            catch( Exception e )
            {
                Console.WriteLine( e.StackTrace );
                Console.WriteLine( e.Message );
            }
        }

        private void ProcessMessageOrders()
        {
            try
            {
                // copy messageOrderQueue
                Queue<WsMessageEvent> messages;
                lock( messageRoot )
                {
                    messages = new Queue<WsMessageEvent>( messageQueue );
                    messageQueue = new ConcurrentQueue<WsMessageEvent>();
                }



                foreach( var messageEvent in messages )
                {
                    if( messageEvent.Type == "snapshot" )
                    {

                        // copy active orders
                        ConcurrentDictionary<string, ConcurrentDictionary<string, OrderInfo>> previousActive =
                            new ConcurrentDictionary<string, ConcurrentDictionary<string, OrderInfo>>( activeOrders );

                        activeOrders = new ConcurrentDictionary<string, ConcurrentDictionary<string, OrderInfo>>();
                        foreach( var product in productInfos.Keys )
                        {
                            activeOrders[ product ] = new ConcurrentDictionary<string, OrderInfo>();
                        }

                        foreach( var order in messageEvent.Orders )
                        {
                            logger.LogTrackerOutput( $"{order.Status} - {order.Order_Id}" );
                            // check if any previously active orders are missing in these OPEN orders

                            // add any new active orders to active orders

                            // query for fills

                            // for fills: remove matched buys, remove ids, log fills

                            if( order.Status == "OPEN" )
                            {
                                ProcessOpenOrderUpdate( order, 1 );
                            }
                            else
                            {
                                throw new Exception( "huh?" );
                            }
                        }

                        ProcessPreviouslyActiveOrders( ref previousActive );

                    }
                    if( messageEvent.Type == "update" )
                    {
                        foreach( var order in messageEvent.Orders )
                        {
                            logger.LogTrackerOutput( $"{order.Status} - {order.Order_Id}" );
                            writer.Write( $"{order.Status} - {order.Order_Id}" );

                            //OPEN orders:
                            //  Not filled: Add to activeOrders, associatedOrders, remove from pendingOrders
                            //  Partially filled: Add or subtract from unMatchedBuyOrders, remove associatedOrder, log

                            //FILLED orders: Add or subtract from unMatchedBuyOrders, remove associatedOrders, log

                            //CANCELLED orders: Remove from activeOrders, associatedOrders, cancelOrders

                            //FAILED orders: remove from pendingOrders

                            if( order.Status == "PENDING" )
                            {
                                ProcessPendingOrderUpdate( order );
                            }
                            else if( order.Status == "OPEN" )
                            {
                                ProcessOpenOrderUpdate( order, 1 );
                            }
                            else if( order.Status == "FILLED" )
                            {
                                ProcessFilledOrderUpdate( order );
                            }
                            else if( order.Status == "CANCELLED" )
                            {
                                ProcessCancelledOrderUpdate( order );
                            }
                            else if( order.Status == "FAILED" )
                            {
                                ProcessFailedOrder( order );
                            }
                        }
                    }
                }
            }
            catch( Exception e)
            {
                Console.WriteLine( e.StackTrace );
                Console.WriteLine( e.Message );
            }
        }

        private void ProcessPreviouslyActiveOrders( ref ConcurrentDictionary<string, ConcurrentDictionary<string, OrderInfo>> previousActive )
        {
            try
            {

                List<OrderInfo> inactiveOrders = new List<OrderInfo>();

                foreach( var pair in previousActive )
                {
                    foreach( var innerPair in pair.Value )
                    {
                        if( !activeOrders.ContainsKey( pair.Key ) )
                        {
                            inactiveOrders.Add( innerPair.Value );
                        }
                        else
                        {
                            if( !activeOrders[ pair.Key ].ContainsKey( innerPair.Key ) )
                            {
                                inactiveOrders.Add( innerPair.Value );
                            }
                        }
                    }
                }

                int count = inactiveOrders.Count;
                OrderInfo order = null;

                for( int i = 0; i < count; i++ )
                {
                    order = inactiveOrders[ i ];

                    FillsHolder fills = FetchOrderFills( order.Order_Id );

                    if( fills != null )
                    {
                        if( fills.Fills.Length > 0 )
                        {
                            order.FillTradeIds = new List<string>();
                            foreach( var fill in fills.Fills )
                            {
                                order.FilledSize += fill.Size;
                                order.FillTradeIds.Add( fill.Trade_Id );
                            }

                            ProcessOrderFills( order );
                        }
                    }

                    string associatedId = null;
                    List<string> associatedIds = new List<string>();
                    foreach( var pair in associatedOrders )
                    {
                        if( pair.Value == order.ClientOrderId )
                        {
                            associatedIds.Add( pair.Key );
                        }
                    }

                    string throwAway;
                    foreach( var id in associatedIds )
                    {
                        associatedOrders.TryRemove( id, out throwAway );
                    }
                }

                logger.LogActiveOrders( activeOrders );
                logger.LogAssociatedOrders( associatedOrders );

            }
            catch( Exception e )
            {
                Console.WriteLine( e.StackTrace );
                Console.WriteLine( e.Message );
            }
        }

        private FillsHolder FetchOrderFills( string orderId )
        {
            try
            {
                RestResponse resp = reqMaker.SendAuthRequest( $@"api/v3/brokerage/orders/historical/fills?order_id={orderId}",
                                                                Method.Get,
                                                                "" );
                if( resp.IsSuccessful )
                {
                    return JsonConvert.DeserializeObject<FillsHolder>( resp.Content );
                }
                else
                {
                    throw new Exception( "Request failed" );
                }
            }
            catch( Exception e )
            {
                Console.WriteLine( e.StackTrace );
                Console.WriteLine( e.Message );
                return null;
            }
        }

        private void ProcessFailedOrder( WsOrder order )
        {
            try
            {

            }
            catch( Exception e )
            {
                Console.WriteLine( e.StackTrace );
                Console.WriteLine( e.Message );
            }
        }

        private async Task ProcessCancelledOrderUpdate( WsOrder order )
        {
            try
            {
                OrderInfo cancelledOrder;

                if( activeOrders[ order.Product_Id ].ContainsKey( order.Client_Order_Id ) )
                {
                    activeOrders[ order.Product_Id ].TryRemove( order.Client_Order_Id, out cancelledOrder );

                    string outString;
                    while( recentCancels.Count > 50 )
                    {
                        recentCancels.TryDequeue( out outString );
                    }

                    recentCancels.Enqueue( cancelledOrder.ClientOrderId );

                    cancelledOrder.Status = order.Status;
                    cancelledOrder.FilledSize = double.Parse( order.Cumulative_Quantity, culture );
                    cancelledOrder.Fee = double.Parse( order.Total_Fees, culture );

                    if( cancelledOrder.FilledSize > 0.0 )
                    {
                        // process fills
                        cancelledOrder.Status = "FILLED";
                        ProcessOrderFills( cancelledOrder );
                    }

                    await logger.LogActiveOrders( activeOrders );

                    if( pendingOrders.ContainsKey( order.Client_Order_Id ) )
                    {
                        pendingOrders.TryRemove( order.Client_Order_Id, out cancelledOrder );
                    }

                    if( sentOrders.ContainsKey( order.Client_Order_Id ) )
                    {
                        OrderInfoResponse cancelledOrderResponse;
                        sentOrders.TryRemove( order.Client_Order_Id, out cancelledOrderResponse );
                    }
                }
                else if( pendingOrders.ContainsKey( order.Client_Order_Id ) )
                {
                    pendingOrders.TryRemove( order.Client_Order_Id, out cancelledOrder );

                    string outString;
                    while( recentCancels.Count > 50 )
                    {
                        recentCancels.TryDequeue( out outString );
                    }

                    recentCancels.Enqueue( cancelledOrder.ClientOrderId );

                    cancelledOrder.Status = order.Status;
                    cancelledOrder.FilledSize = double.Parse( order.Cumulative_Quantity, culture );
                    cancelledOrder.Fee = double.Parse( order.Total_Fees, culture );

                    if( cancelledOrder.FilledSize > 0.0 )
                    {
                        // process fills
                        cancelledOrder.Status = "FILLED";
                        ProcessOrderFills( cancelledOrder );
                    }

                    if( sentOrders.ContainsKey( order.Client_Order_Id ) )
                    {
                        OrderInfoResponse cancelledOrderResponse;
                        sentOrders.TryRemove( order.Client_Order_Id, out cancelledOrderResponse );
                    }
                }

                string associated = null;
                string throwawayAssociated;
                foreach( var pair in associatedOrders )
                {
                    if( pair.Value == order.Client_Order_Id )
                    {
                        associated = pair.Key;
                    }
                }
                if( associated != null )
                {
                    associatedOrders.TryRemove( associated, out throwawayAssociated );
                }
            }
            catch( Exception e )
            {
                Console.WriteLine( e.StackTrace );
                Console.WriteLine( e.Message );
            }
        }

        private async Task ProcessOrderFills( OrderInfo order )
        {
            try
            {
                string updateOrderId = null;
                OrderInfo updateOrder = null;
                OrderInfo associatedOrder;
                List<string> associatedOrderIds;
                ProductInfo productInfo = productInfos[ order.ProductId ];
                int basePrecision = productInfo.BasePrecision;
                Math.Round( order.FilledSize, basePrecision );

                FillsHolder fillsHolder = FetchOrderFills( order.Order_Id );
                List<Fill> fills = new List<Fill>( fillsHolder.Fills );

                if( fillsHolder != null )
                {
                    if( fillsHolder.Fills.Length > 0 )
                    {
                        foreach( var fill in fillsHolder.Fills )
                        {
                            writer.Write( $"Fill {fill.Size} {fill.Product_Id} at {order.Price}" );
                            await logger.LogTrackerOutput( $"Fill {fill.Size} {fill.Product_Id} at {order.Price}" );

                            //
                                
                            //

                        }
                        if( order.Side == "BUY" )
                        {
                            // fills go in unMatchedOrders

                            //unMatchedOrders[ order.ProductId ][ order.ClientOrderId ] = order;

                            order.Size = 0;

                            foreach( var item in unMatchedOrders[ order.ProductId ].Values )
                            {
                                if( Math.Abs( item.Price - order.Price ) < orderSpreadPercent * item.Price )
                                {
                                    updateOrderId = item.ClientOrderId;
                                    break;
                                }
                            }

                            if( updateOrderId != null )
                            {
                                unMatchedOrders[ order.ProductId ].TryRemove( updateOrderId, out updateOrder );

                                if( updateOrder.FillTradeIds == null )
                                {
                                    updateOrder.FillTradeIds = new List<string>();
                                }

                                foreach( var fill in fillsHolder.Fills )
                                {
                                    if( !updateOrder.FillTradeIds.Contains( fill.Trade_Id ) )
                                    {
                                        updateOrder.FillTradeIds.Add( fill.Trade_Id );
                                        updateOrder.Price = ((updateOrder.Price * updateOrder.FilledSize) + ( order.Price * fill.Size ) )
                                                                        / (updateOrder.FilledSize + fill.Size);
                                        updateOrder.FilledSize += fill.Size;
                                       
                                    }
                                }

                                updateOrder.Price = Math.Round( updateOrder.Price, productInfos[ order.ProductId ].QuotePrecision );
                                updateOrder.FilledSize = Math.Round( updateOrder.FilledSize, productInfos[ order.ProductId ].BasePrecision );

                                unMatchedOrders[ updateOrder.ProductId ][ updateOrderId ] = updateOrder;

                            }
                            else
                            {

                                order.FillTradeIds = new List<string>();

                                foreach( var fill in fillsHolder.Fills )
                                {
                                    if( !order.FillTradeIds.Contains( fill.Trade_Id ) )
                                    {
                                        order.FillTradeIds.Add( fill.Trade_Id );
                                    }
                                }

                                order.Size = Math.Round( order.Size, productInfos[ order.ProductId ].BasePrecision );
                                order.Price = Math.Round( order.Size, productInfos[ order.ProductId ].QuotePrecision );

                                unMatchedOrders[ order.ProductId ][ order.ClientOrderId ] = order;

                            }
                        }
                        else
                        {
                            associatedOrderIds = new List<string>();

                            // fills subtract from unMatchedBuyOrders
                            foreach( var pair in associatedOrders )
                            {
                                if( pair.Value == order.ClientOrderId )
                                {
                                    associatedOrderIds.Add( pair.Key );
                                }
                            }

                            if( associatedOrderIds.Count > 0 )
                            {
                                foreach( var id in associatedOrderIds )
                                {
                                    if( unMatchedOrders[ order.ProductId ].ContainsKey( id ) )
                                    {
                                        unMatchedOrders[ order.ProductId ].TryRemove( id, out associatedOrder );

                                        if( associatedOrder.FillTradeIds == null )
                                        {
                                            associatedOrder.FillTradeIds = new List<string>();
                                        }

                                        if( order.FillTradeIds == null )
                                        {
                                            order.FillTradeIds = new List<string>();
                                        }

                                        foreach( var fill in fills )
                                        {
                                            if( !(order.FillTradeIds.Contains(fill.Trade_Id)) )
                                            {
                                                order.FillTradeIds.Add( fill.Trade_Id );
                                            }
                                        }

                                        int count = fills.Count;

                                        for( int i = 0; i < count; i++ )
                                        {
                                            if( !associatedOrder.FillTradeIds.Contains( fills[i].Trade_Id ) )
                                            {
                                                if( associatedOrder.FilledSize < fills[ i ].Size )
                                                {
                                                    associatedOrder.FillTradeIds.Add( fills[ i ].Trade_Id );
                                                    fills[ i ].Size = Math.Round( fills[ i ].Size - associatedOrder.FilledSize, basePrecision );
                                                    associatedOrder.FilledSize = 0;
                                                }
                                                else if( associatedOrder.FilledSize >= fills[ i ].Size )
                                                {
                                                    associatedOrder.FillTradeIds.Add( fills[ i ].Trade_Id );
                                                    associatedOrder.FilledSize = Math.Round( associatedOrder.FilledSize - fills[ i ].Size, basePrecision );
                                                    //fills[ i ].Size = 0;
                                                    fills.RemoveAt( i );
                                                    count--;
                                                }

                                                if( associatedOrder.FilledSize == 0 )
                                                {
                                                    break;
                                                }
                                            }
                                        }

                                        if( associatedOrder.FilledSize > 0 )
                                        {
                                            unMatchedOrders[ order.ProductId ][ associatedOrder.ClientOrderId ] = associatedOrder;
                                        }

                                        string throwawayAssociatedId;
                                        associatedOrders.TryRemove( id, out throwawayAssociatedId );
                                        await logger.LogAssociatedOrders( associatedOrders );
                                    }
                                    else
                                    {
                                        writer.Write( $"Order {order.Order_Id} has no associated order" );
                                    }
                                }
                            }
                            else
                            {
                                writer.Write( $"{order.Id} prolly filled already" );
                                //throw new Exception( "no associated unmatched order" );
                            }
                        }

                        await logger.LogUnMatchedOrders( unMatchedOrders );

                        await logger.LogFilledOrder( order );
                    }
                }
                else
                {
                    // try agen etc...
                }
            }
            catch( Exception e )
            {
                Console.WriteLine( e.StackTrace );
                Console.WriteLine( e.Message );
            }
        }

        private async Task ProcessFilledOrderUpdate( WsOrder order )
        {
            try
            {
                OrderInfo filledOrder;

                if( activeOrders[ order.Product_Id].ContainsKey(order.Client_Order_Id) )
                {
                    
                    activeOrders[ order.Product_Id ].TryRemove( order.Client_Order_Id, out filledOrder );
                    filledOrder.Status = order.Status;
                    filledOrder.FilledSize = double.Parse( order.Cumulative_Quantity, new CultureInfo( "En-Us" ) );

                    ProcessOrderFills( filledOrder );

                    await logger.LogActiveOrders( activeOrders );
                }
                else
                {
                    recentFills.Enqueue( order );
                    OrderInfo throwAway;

                    if( pendingOrders.ContainsKey( order.Client_Order_Id ) )
                    {
                        pendingOrders.TryRemove( order.Client_Order_Id, out throwAway );
                    }

                    RestResponse resp = reqMaker.SendAuthRequest( $@"api/v3/brokerage/orders/historical/{order.Order_Id}",
                                                              Method.Get,
                                                              "" );
                    if( resp.IsSuccessful )
                    {
                        OrderHolder holder = JsonConvert.DeserializeObject<OrderHolder>( resp.Content );
                        filledOrder = holder.Order;


                        filledOrder.FilledSize = double.Parse( order.Cumulative_Quantity, new CultureInfo( "En-Us" ) );
                        filledOrder.Status = order.Status;

                        ProcessOrderFills( filledOrder );
                    }
                }
            }
            catch( Exception e )
            {
                Console.WriteLine( e.StackTrace );
                Console.WriteLine( e.Message );
            }
        }

        private async void UserChannelUpdate(object source, UserChannelUpdateEventArgs e)
        {
            try
            {
                await Task.Run( () =>
                {
                    lock( messageRoot )
                    {
                        foreach( var item in e.messageEvent )
                        {
                            messageQueue.Enqueue( item );
                        }
                    }
                });
            }
            catch( Exception ex )
            {
                Console.WriteLine( ex.StackTrace );
                Console.WriteLine( ex.Message );
            }
        }

        private async void OnTimedEvent(Object source, ElapsedEventArgs e)
        {
            try
            {
                await Task.Run( () =>
                {
                    if( !processingMessages )
                    {
                        lock( dequeueRoot )
                        {
                            if( messageQueue.Count > 0 )
                            {
                                processingMessages = true;

                                ProcessMessageOrders();

                                processingMessages = false;
                            }
                        }
                    }
                });
            }
            catch( Exception ex )
            {
                Console.WriteLine( ex.StackTrace );
                Console.WriteLine( ex.Message );
            }
        }

        public ConcurrentDictionary<string, ConcurrentDictionary<string, OrderInfo>> ActiveOrders
        {
            get
            {
                return activeOrders;
            }
        }

        public ConcurrentDictionary<string, ConcurrentDictionary<string, OrderInfo>> UnMatched
        {
            get
            {
                return unMatchedOrders;
            }
        }
        public ConcurrentDictionary<string, string> Associated
        {
            get
            {
                return associatedOrders;
            }
        }

        private ConcurrentDictionary<string, OrderInfoResponse> sentOrders;
        private ConcurrentDictionary<string, OrderInfo> pendingOrders;
        private ConcurrentDictionary<string, ConcurrentDictionary<string, OrderInfo>> activeOrders;
        private ConcurrentDictionary<string, ConcurrentDictionary<string, OrderInfo>> unMatchedOrders;
        private ConcurrentDictionary<string, string> pendingAssociatedIds;
        private ConcurrentDictionary<string, string> associatedOrders;
        private ConcurrentQueue<WsMessageEvent> messageQueue;
        private ConcurrentQueue<string> recentCancels;
        private ConcurrentQueue<WsOrder> recentFills;

        private readonly DataFetcher fetcher;
        private readonly AsyncOrderLogger logger;
        private readonly SynchronizedConsoleWriter writer;
        private readonly RequestMaker reqMaker;
        private readonly ConcurrentDictionary<string, ProductInfo> productInfos;
        private readonly double orderSpreadPercent;
        bool processingMessages;
        private CultureInfo culture;
    }

    internal class CancelResponse
    {
        [JsonConstructor]
        public CancelResponse(cancelResults[] results)
        {
            Results = results;
        }

        public cancelResults[] Results { get; }
    }

    internal class cancelResults
    {
        [JsonConstructor]
        public cancelResults(bool success, string failure_reason, string order_id)
        {
            Success = success;
            Failure_Reason = failure_reason;
            Order_Id = order_id;
        }

        public bool Success { get; }
        public string Failure_Reason { get; }
        public string Order_Id { get; }
    }
}
