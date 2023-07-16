using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WebSocketSharp;

namespace CBApp1
{
    /// <summary>
    /// 
    /// </summary>
    public class OrderTracker
    {
        private readonly object trackRoot = new object();
        public OrderTracker(string profileId, string[] productIds, ref OrderLogger logger, ref SynchronizedConsoleWriter writer, 
            ref RequestMaker authReqMaker, ref ConcurrentDictionary<string, ProductInfo> productInfos)
        {
            try
            {
                this.logger = logger;
                this.writer = writer;

                this.productInfos = productInfos;

                currActive = new Dictionary<string, List<OrderInfo>>(logger.FileActiveOrders);
                prevActive = new Dictionary<string, List<OrderInfo>>();
                currAssociatedIds = new Dictionary<string, string>(logger.FileAssociatedIds);
                unMatchedBuys = new Dictionary<string, List<FillInfo>>(logger.FileUnMatchedBuyOrders);
                unMatchedBuyCounts = new Dictionary<string, int>();

                this.productIds = new List<string>();
                this.profileId = profileId;

                foreach (string id in productIds)
                {
                    this.productIds.Add( id );

                    //UpdateTrackedProduct( ref authReqMaker, id );
                }

                

                foreach( var product in productIds )
                {
                    
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
                Console.WriteLine(e.Message);
            }
            
        }

        public void TrackBuyOrder(OrderInfo order)
        {
            try
            {
                lock( trackRoot )
                {
                    if( currActive.ContainsKey( order.ProductId ) )
                    {
                        currActive[ order.ProductId ].Add( order );
                    }
                    else
                    {
                        currActive[ order.ProductId ] = new List<OrderInfo>();
                        currActive[ order.ProductId ].Add( order );
                    }
                }
            }
            catch( Exception e)
            {
                Console.WriteLine( e.StackTrace );
                Console.WriteLine( e.Message );
            }
            
        }

        public void TrackSellOrder( OrderInfo order , FillInfo[] associated)
        {
            try
            {
                lock( trackRoot )
                {
                    if( currActive.ContainsKey( order.ProductId ) )
                    {
                        currActive[ order.ProductId ].Add( order );
                    }
                    else
                    {
                        currActive[ order.ProductId ] = new List<OrderInfo>();
                        currActive[ order.ProductId ].Add( order );
                    }

                    foreach( var prevBuyOrder in associated )
                    {
                        currAssociatedIds[ prevBuyOrder.Order_Id ] = order.Id;
                    }
                }
            }
            catch( Exception e)
            {
                Console.WriteLine( e.StackTrace );
                Console.WriteLine( e.Message );
            }
            
        }

        public void UpdateTrackedProduct(ref RequestMaker authReqMaker, string productId)
        {
            try
            {
                lock( trackRoot )
                {
                    CopyCurrActive( productId );

                    FetchCbOrders( ref authReqMaker, productId );

                    FilterActiveOrders( productId );

                    // updates currAssociatedIds
                    List<FillInfo>[] filledOrders = ProcessOrders( ref authReqMaker, productId );

                    CountUnMatchedBuys( productId );

                    // update collections in logger
                    logger.UpdateLogs( productId, currActive, unMatchedBuys, currAssociatedIds, filledOrders );
                }
            }
            catch( Exception e)
            {
                Console.WriteLine( e.StackTrace );
                Console.WriteLine( e.Message );
            }
            
        }

        private void CountUnMatchedBuys( string productId )
        {
            try
            {
                if( unMatchedBuys.ContainsKey( productId ) )
                {
                    unMatchedBuyCounts[ productId ] = unMatchedBuys[ productId ].Count;
                }
                else
                {
                    unMatchedBuyCounts[ productId ] = 0;
                }
            }
            catch( Exception e)
            {
                Console.WriteLine( e.StackTrace );
                Console.WriteLine( e.Message );
            }
            
        }

        private void UpdateProductUnMatchedBuys( string productId , ref List<FillInfo> buyFills, ref List<FillInfo> sellFills )
        {
            try
            {
                int count;
                bool added = false;
                double size;
                double price;

                // remove matched buys from unMatchedBuys
                if( sellFills != null )
                {
                    if( sellFills.Count != 0 )
                    {
                        if( unMatchedBuys.ContainsKey( productId ) )
                        {
                            count = unMatchedBuys[ productId ].Count;

                            foreach( FillInfo sellFill in sellFills )
                            {
                                for( int i = 0; i < count; i++ )
                                {
                                    if( currAssociatedIds.ContainsKey( unMatchedBuys[ productId ][i].Order_Id ) )
                                    {
                                        if( currAssociatedIds[ unMatchedBuys[ productId ][ i ].Order_Id ] == sellFill.Order_Id )
                                        {

                                            if( sellFill.Size <= unMatchedBuys[ productId ][i].Size )
                                            {

                                                writer.Write( $"Subtracting {sellFill.Size} {sellFill.Product_Id.Split( '-' )[ 0 ]} from " +
                                                    $"{unMatchedBuys[ productId ][ i ].Size} {sellFill.Product_Id.Split( '-' )[ 0 ]}" );

                                                size = Math.Round( unMatchedBuys[ productId ][ i ].Size - sellFill.Size, productInfos[ productId ].BasePrecision );
                                                unMatchedBuys[ productId ][ i ].Size = size;
                                            }
                                            //else
                                            //{
                                            //    writer.Write( $"Removing {unMatchedBuys[ productId ][ i ].Product_Id} buy at {unMatchedBuys[ productId ][ i ].Price}" );
                                            //    unMatchedBuys[ productId ].RemoveAt( i );
                                            //    count--;
                                            //}

                                            if( unMatchedBuys[ productId ][ i ].Size <= 0 )
                                            {
                                                writer.Write( $"Removing {unMatchedBuys[ productId ][ i ].Product_Id} buy at {unMatchedBuys[ productId ][ i ].Price}" );
                                                unMatchedBuys[ productId ].RemoveAt( i );
                                                count--;
                                            }

                                            if( unMatchedBuys[ productId ].Count == 0 )
                                            {
                                                unMatchedBuys.Remove( productId );
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                // add buys to unMatchedBuys
                if( buyFills != null )
                {
                    if( buyFills.Count != 0 )
                    {
                        if( unMatchedBuys.ContainsKey( productId ) )
                        {
                            count = unMatchedBuys[ productId ].Count;

                            foreach( var fill in buyFills )
                            {
                                for( int i = 0; i < count; i++ )
                                {
                                    if( Math.Abs( unMatchedBuys[ productId ][ i ].Price - fill.Price) <
                                        fill.Price * 0.013 )
                                    {
                                        double compSize = fill.Size;
                                        double unMatchedSize = unMatchedBuys[ productId ][ i ].Size;
                                        double totalSize = compSize + unMatchedSize;
                                        double compShare = fill.Size / totalSize;
                                        double unMatchedShare = unMatchedSize / totalSize;

                                        // round here
                                        double averagePrice = ((fill.Price * compShare) +
                                            (unMatchedShare * unMatchedBuys[ productId ][ i ].Price));

                                        size = Math.Round( unMatchedBuys[ productId ][ i ].Size + fill.Size, productInfos[ productId ].BasePrecision );
                                        unMatchedBuys[ productId ][ i ].Size = size;
                                            
                                        price = Math.Round( averagePrice, productInfos[ fill.Product_Id ].QuotePrecision );
                                        unMatchedBuys[ productId ][ i ].Price = price;

                                        added = true;
                                        break;
                                    }
                                }
                            }

                            
                        }
                        else
                        {
                            unMatchedBuys[ productId ] = new List<FillInfo>();
                        }

                        if( !added )
                        {
                            foreach( var fill in buyFills )
                            {
                                unMatchedBuys[ productId ].Add( fill );
                            }
                        }

                        count = unMatchedBuys[ productId ].Count;

                        for( int i = 0; i < count; i++ )
                        {
                            size = Math.Round( unMatchedBuys[ productId ][ i ].Size, productInfos[ productId ].BasePrecision );
                            unMatchedBuys[ productId ][ i ].Size = size;
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

        private void FilterActiveOrders( string productId )
        {
            try
            {

                int count;

                if( prevActive.ContainsKey( productId ) )
                {
                    // previouly active productId orders

                    if( currActive.ContainsKey( productId ) )
                    {

                        // currently active productId orders

                        // compare and remove still active from prevActive

                        count = prevActive[ productId ].Count;

                        foreach( OrderInfo order in currActive[ productId ] )
                        {

                            if( count == 0 )
                            {
                                break;
                            }

                            for( int i = 0; i < count; i++ )
                            {
                                if( prevActive[ productId ][ i ].Id == order.Id )
                                {
                                    prevActive[ productId ].RemoveAt( i );
                                    count--;
                                }
                            }
                        }
                    }
                    else
                    {
                        // no currently active productId orders
                        // all orders in prevActive are no longer active
                    }
                }
                else
                {
                    // no previously active productId orders
                    // nothing to do here
                }
            }
            catch( Exception e)
            {
                Console.WriteLine( e.StackTrace );
                Console.WriteLine( e.Message );
            }
        }

        private List<FillInfo>[] ProcessOrders( ref RequestMaker authReqMaker, string productId )
        {
            try
            {
                // processing collections
                List<string> inactiveSellIds = null;

                // logging collections
                List<FillInfo> filledBuyOrders = null;
                List<FillInfo> filledSellOrders = null;

                // all orders in prevActive if ContainsKey( productId )
                // are inactive orders, filled or canceled
                if( prevActive.ContainsKey( productId ) )
                {
                    if( prevActive[ productId ].Count != 0 )
                    {
                        // Query for fills on each previously active order, buy or sell, fills are added to collections
                        // passed by reference
                        FilterFilledOrders( productId, ref authReqMaker, ref filledBuyOrders, ref filledSellOrders, ref inactiveSellIds );

                        // Remove matched buy orders, add new filled buy orders
                        UpdateProductUnMatchedBuys( productId, ref filledBuyOrders, ref filledSellOrders );

                        // Update currAssociatedIds using filled sell-order ids and remaining cancelled sell-order ids
                        UpdateAssociatedIds( productId, ref filledSellOrders, ref inactiveSellIds );
                    }

                    // Processing of previously active orders complete, zero orders or otherwise
                    // remove productId key from prevActive
                    prevActive.Remove( productId );

                }
                else
                {
                    // no previously active orders for processing
                }

                return new List<FillInfo>[] { filledBuyOrders, filledSellOrders };
            }
            catch( Exception e)
            {
                Console.WriteLine( e.StackTrace );
                Console.WriteLine( e.Message );
                return null;
            }
        }
        /// <summary>
        /// For every order in prevActive[ productId ] try to fetch fills,
        /// if successful, process each fill in ProcessOrderFills
        /// if at the end any fills where found returns true
        /// </summary>
        /// <param name="productId"></param>
        /// <param name="authReqMaker"></param>
        /// <param name="filledBuyOrders"></param>
        /// <param name="filledSellOrders"></param>
        /// <param name="inactiveSellIds"></param>
        private void FilterFilledOrders( string productId, ref RequestMaker authReqMaker,
                            ref List<FillInfo> filledBuyOrders, ref List<FillInfo> filledSellOrders, ref List<string> inactiveSellIds )
        {
            try
            {
                string queryString;
                RestResponse resp;
                List<FillInfo> fills;
                int count = prevActive[ productId ].Count;

                if( prevActive.ContainsKey( productId ) )
                {
                    // there is prevActive
                    if( count != 0 )
                    {
                        // see if each order has fills, 
                        for( int i = 0; i < count; i++ )
                        {
                            queryString = $"order_id={prevActive[ productId ][ i ].Id}&profile_id={profileId}&limit=100";

                            resp = authReqMaker.SendFillsRequest( queryString );

                            if( resp.IsSuccessful )
                            {

                                fills = JsonConvert.DeserializeObject<List<FillInfo>>( resp.Content );


                                // there is fills
                                count = ProcessOrderFills( productId, ref fills, ref filledBuyOrders, ref filledSellOrders,
                                    ref inactiveSellIds, count, i );
                            }

                            if( i == 10 )
                            {
                                Thread.Sleep( 1000 );
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="productId"></param>
        /// <param name="fills"></param>
        /// <param name="filledBuyOrders"></param>
        /// <param name="filledSellOrders"></param>
        /// <param name="inactiveSellIds"></param>
        /// <param name="count"></param>
        /// <param name="i"></param>
        /// <returns></returns>
        private int ProcessOrderFills(string productId, ref List<FillInfo> fills, ref List<FillInfo> filledBuyOrders,
                                        ref List<FillInfo> filledSellOrders, ref List<string> inactiveSellIds, int count, int i)
        {
            try
            {
                FillInfo newFill = null;

                if( fills.Count > 0 )
                {
                    if( prevActive[ productId ][ i ].Side == "buy" )
                    {

                        // no previous filled buy orders
                        if( filledBuyOrders == null )
                        {
                            filledBuyOrders = new List<FillInfo>();
                        }

                        // for every fill add to one fill to be logged
                        foreach( var fill in fills )
                        {
                            if( newFill == null )
                            {
                                newFill = new FillInfo( fill );
                            }
                            else
                            {
                                newFill.Size += fill.Size;
                            }
                        }

                        if( newFill.Size < prevActive[ productId ][ i ].Size )
                        {
                            //writer.Write( "partials?" );

                            // add partialfill log file...
                        }

                        Math.Round(newFill.Size, productInfos[newFill.Product_Id].BasePrecision);
                        filledBuyOrders.Add( newFill );

                    }
                    else if( prevActive[ productId ][ i ].Side == "sell" )
                    {
                        if( filledSellOrders == null )
                        {
                            filledSellOrders = new List<FillInfo>();
                        }
                        if( inactiveSellIds == null )
                        {
                            inactiveSellIds = new List<string>();
                        }

                        foreach( var fill in fills )
                        {
                            if( newFill == null )
                            {
                                newFill = new FillInfo( fill );
                            }
                            else
                            {
                                newFill.Size += fill.Size;
                            }
                        }

                        if( newFill.Size < prevActive[ productId ][ i ].Size )
                        {
                            //writer.Write( "partials?" );

                            // add partialfill log file...
                        }

                        Math.Round( newFill.Size, productInfos[ newFill.Product_Id ].BasePrecision );
                        filledSellOrders.Add( newFill );
                        inactiveSellIds.Add( prevActive[ productId ][ i ].Id );
                    }


                    prevActive[ productId ].RemoveAt( i );
                    count--;
                }

                return count;
            }
            catch( Exception e )
            {
                Console.WriteLine( e.StackTrace );
                Console.WriteLine( e.Message );
                return count;
            }
        }

        private void UpdateAssociatedIds( string productId, ref List<FillInfo> filledSellOrders, ref List<string> inactiveSellIds )
        {
            try
            {
                // inactive null or prevActive[ productId null
                //if( prevActive[ productId ].Count != 0 || inactiveSellIds.Count != 0 )
                //{

                //}
                // if prevActive still has sell orders, remove these from currAssociatedIds
                // if it contains any ids
                List<string> associatedIdsToRemove;

                if( currAssociatedIds.Count != 0 )
                {
                    associatedIdsToRemove = new List<string>();

                    if( prevActive.ContainsKey( productId ) )
                    {
                        if( prevActive[ productId ].Count > 0 )
                        {
                            // no fills were found, but inactive orders remain
                            if( inactiveSellIds == null )
                            {
                                inactiveSellIds = new List<string>();
                            }

                            foreach( OrderInfo order in prevActive[ productId ] )
                            {
                                // if order.Side == "sell" it must be associated to a
                                // key in currAssociatedIds
                                if( order.Side == "sell" )
                                {
                                    inactiveSellIds.Add( order.Id );
                                }
                            }
                        }
                    }

                    if( inactiveSellIds != null )
                    {
                        foreach( string sellId in inactiveSellIds )
                        {
                            foreach( var idPair in currAssociatedIds )
                            {
                                if( idPair.Value == sellId )
                                {
                                    associatedIdsToRemove.Add( idPair.Key );
                                }
                            }
                        }
                    }

                    if( associatedIdsToRemove.Count > 0 )
                    {
                        if( associatedIdsToRemove.Count > 0 )
                        {
                            foreach( var id in associatedIdsToRemove )
                            {
                                currAssociatedIds.Remove( id );
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
        private int FindFill( string productId, ref List<FillInfo> filledBuyOrders, 
                                ref List<FillInfo> filledSellOrders, ref List<string> inactiveSellIds, int count, FillInfo fill )
        {
            try
            {
                for( int i = 0; i < count; i++ )
                {
                    if( fill.Order_Id == prevActive[ productId ][ i ].Id )
                    {
                        // previously active order now filled, to be logged

                        if( prevActive[ productId ][ i ].Side == "buy" )
                        {
                            // filled buy order

                            filledBuyOrders.Add( fill );

                        }
                        else if( prevActive[ productId ][ i ].Side == "sell" )
                        {
                            // filled sell order

                            filledSellOrders.Add( fill );
                            inactiveSellIds.Add( fill.Order_Id );

                        }

                        // remove processed order from prevActive, decrease count
                        prevActive[ productId ].RemoveAt( i );
                        count--;
                        break;
                    }
                }
                return count;
            }
            catch( Exception e)
            {
                Console.WriteLine( e.StackTrace );
                Console.WriteLine( e.Message );
                return 0;
            }
            
        }

        private bool FetchCbOrders( ref RequestMaker authReqMaker, string productId )
        {
            try
            {
                bool requestSucceded = false;
                string queryString;
                RestResponse resp;
                List<OrderInfo> fetchedActiveOrders;

                queryString = $"profile_id={profileId}&product_id={productId}&sortedBy=created_at&sorting=desc&" +
                        $"limit=100&status=open&status=pending";

                resp = authReqMaker.GetOrdersRequest( queryString );

                for( int i = 0; i < 4; i++ )
                {
                    if( resp.IsSuccessful )
                    {

                        fetchedActiveOrders = JsonConvert.DeserializeObject<List<OrderInfo>>( resp.Content );

                        if( fetchedActiveOrders.Count != 0 )
                        {
                            // new active orders copied to currActive

                            currActive[ productId ] = new List<OrderInfo>( fetchedActiveOrders );
                        }
                        else
                        {
                            // no currently active orders, if prevActive has any orders they are filled, partly filled or canceled
                            // remove productId from currActive
                            currActive.Remove( productId );
                        }

                        requestSucceded = true;

                        break;

                    }
                    

                    Thread.Sleep( 70 );
                    resp = authReqMaker.GetOrdersRequest( queryString );
                }

                if( resp.StatusCode == System.Net.HttpStatusCode.NotFound )
                {
                    requestSucceded = false;
                }
                else if( resp.StatusCode == System.Net.HttpStatusCode.BadRequest )
                {
                    writer.Write( "hm" );
                }

                return requestSucceded;
            }
            catch( Exception e)
            {
                Console.WriteLine( e.StackTrace );
                Console.WriteLine( e.Message );
                return false;
            }
            
        }

        private bool CopyCurrActive( string productId )
        {
            try
            {
                // currActive has active orders of this product
                if( currActive.ContainsKey( productId ) )
                {
                    // copy to prevActive
                    prevActive[ productId ] = new List<OrderInfo>( currActive[ productId ] );
                    return true;
                }
                else
                {
                    // no active orders to copy
                    return false;
                }
            }
            catch( Exception e)
            {
                Console.WriteLine( e.StackTrace );
                Console.WriteLine( e.Message );
                return false;
            }
        }

        public bool CancelActiveOrder(ref RequestMaker authReqMaker, string orderId)
        {
            try
            {
                RestResponse cancelResp = authReqMaker.SendCancelRequest( orderId, profileId );
                writer.Write( $"{cancelResp.StatusDescription} {cancelResp.Content}" );

                if( cancelResp.StatusCode == System.Net.HttpStatusCode.OK )
                {
                    writer.Write( $"Cancel order resp: {cancelResp.Content}" );
                    return true;
                }
                else if( cancelResp.StatusCode == System.Net.HttpStatusCode.NotFound )
                {
                    return false;
                }
                else
                {
                    return false;
                }

            }
            catch( Exception e )
            {
                Console.WriteLine( e.Message );
                Console.WriteLine( e.StackTrace );
                return false;
            }
        }

        public Dictionary<string, List<OrderInfo>> CurrActive
        {
            get
            {
                lock( trackRoot )
                {
                    return currActive;
                }
                
            }
        }

        public Dictionary<string, string> CurrAssociated
        {
            get
            {
                lock( trackRoot )
                {
                    return currAssociatedIds;
                }
            }
        }

        public Dictionary<string, List<FillInfo>> UnMatchedBuys
        {
            get
            {
                lock( trackRoot )
                {
                    return unMatchedBuys;
                }
            }
        }

        public Dictionary<string, int> UnMatchedBuyCounts
        {
            get
            {
                lock( trackRoot )
                {
                    return unMatchedBuyCounts;
                }
            }
        }

        private string profileId;

        private OrderLogger logger;
        private SynchronizedConsoleWriter writer;

        private List<string> productIds;
        private Dictionary<string, List<OrderInfo>> currActive;
        private Dictionary<string, List<OrderInfo>> prevActive;
        private Dictionary<string, string> currAssociatedIds;
        private Dictionary<string, List<FillInfo>> unMatchedBuys;
        private Dictionary<string, int> unMatchedBuyCounts;

        private ConcurrentDictionary<string, ProductInfo> productInfos;
    }
}