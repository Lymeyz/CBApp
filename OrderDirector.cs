using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace CBApp1
{
    //Handles events raised by DataAnalyser, keeps track of previous and current orders
    public class OrderDirector
    {
        private readonly object tryRoot = new object();
        private readonly object trackRoot = new object();
        public OrderDirector( ref DataAnalyser analyser,
                             ref SynchronizedConsoleWriter writer,
                             ref RequestMaker reqMaker,
                             double eurAm,
                             int maxBuys,
                             ref System.Timers.Timer timer,
                             double orderSpreadPercent,
                             double requiredSellPercent )
        {
            try
            {
                this.eurAm = eurAm;
                this.maxBuys = maxBuys;
                this.orderSpreadPercent = orderSpreadPercent;
                this.requiredSellPercent = requiredSellPercent;

                productAliases = new Dictionary<string, string>();
                lastTries = new Dictionary<string, DateTime>();

                this.writer = writer;

                // UNSUBSCRIBED
                analyser.PreOrderReady += this.PreOrderReadyEvent;

                this.reqMaker = reqMaker;

                this.analyser = analyser;

                // get profile id
                //profileId = GetProfileId(profile);

                productInfos = analyser.DataHandler.Fetcher.ProductInfos;

                // get accounts
                accounts = new AccountManager(ref reqMaker);

                // NEW LOGGER
                aLogger = new AsyncOrderLogger();

                // NEW TRACKER
                wsTracker = new AsyncOrderTracker( productInfos.Keys.ToArray(),
                                                   ref analyser,
                                                   ref aLogger,
                                                   ref writer,
                                                   ref reqMaker,
                                                   ref productInfos,
                                                   ref timer,
                                                   orderSpreadPercent);




                //while( true )
                //{
                //    tracker.UpdateTracker( ref reqMaker );
                //}
                //while( true )
                //{
                //    PrelO prel1 = new PrelO( "ETH-EUR", DateTime.UtcNow, true );
                //    writer.Write( "Write ETH buy order: " );
                //    prel1.Price = double.Parse( Console.ReadLine(), new CultureInfo( "En-Us" ) );
                //    TryPlaceOrder( prel1 );
                //}


                //lastTry = DateTime.MinValue;

                //PrelO prel2 = new PrelO( "ETH-EUR", DateTime.UtcNow, false );
                //prel2.Price = 1441.44;
                //TryPlaceOrder( prel2 );

                //PrelO prel3 = new PrelO( "ETH-EUR", DateTime.UtcNow, true );
                //while( true )
                //{
                //    prel3.Price = 1341.45;
                //    TryPlaceOrder( prel3 );
                //    prel3.Price = 1341.46;
                //    TryPlaceOrder( prel3 );
                //    Thread.Sleep( 2000 );


                //}


                //PrelO prel4 = new PrelO( "ETH-EUR", DateTime.UtcNow, true );
                //prel4.Price = 1341.44;
                //TryPlaceOrder( prel4 );

                //tracker.UpdateTrackedProduct( ref reqMaker, "ETH-EUR" );

            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
                Console.WriteLine(e.Message);
            }
        }
        public async Task TestSendOrder( string input )
        {
            try
            {
                // currency-b-price
                OrderInfo toCancel = null;
                if( input.Split( '-' )[0] == "B" )
                {
                    if( double.Parse( input.Split( '-' )[ 1 ], new CultureInfo("En-Us") ) < 1560 )
                    {
                        if( wsTracker.ActiveOrders.ContainsKey( "ETH-EUR" ) )
                        {
                            foreach( var item in wsTracker.ActiveOrders[ "ETH-EUR" ].Where( i => i.Value.Side == "BUY" ) )
                            {
                                toCancel = item.Value;
                            }
                        }

                        if( toCancel != null )
                        {
                            if( await wsTracker.CancelOrder( toCancel.ProductId, toCancel.ClientOrderId, toCancel.Order_Id ))
                            {
                                PreOrder pre1 = new PreOrder( "ETH-EUR", DateTime.UtcNow, true );
                                pre1.Price = double.Parse( input.Split( '-' )[ 1 ], new CultureInfo( "En-Us" ) );

                                SendOrder( pre1, null );
                            }
                        }
                        else
                        {
                            PreOrder pre1 = new PreOrder( "ETH-EUR", DateTime.UtcNow, true );
                            pre1.Price = double.Parse( input.Split( '-' )[ 1 ], new CultureInfo( "En-Us" ) );

                            SendOrder( pre1, null );
                        }

                        
                    }
                    
                }
                else if( input.Split( '-' )[ 0 ] == "S" )
                {
                    if( double.Parse( input.Split( '-' )[ 1 ], new CultureInfo( "En-Us" ) ) > 1530 )
                    {
                        PreOrder pre1 = new PreOrder( "ETH-EUR", DateTime.UtcNow, false );
                        pre1.Price = double.Parse( input.Split( '-' )[ 1 ], new CultureInfo( "En-Us" ) );

                        List<OrderInfo> unMatchedOrders = new List<OrderInfo>();
                        List<OrderInfo> matchingOrders = new List<OrderInfo>();

                        if( wsTracker.UnMatched.ContainsKey( "ETH-EUR" ) )
                        {
                            foreach( var pair in wsTracker.UnMatched[ "ETH-EUR" ] )
                            {
                                unMatchedOrders.Add( pair.Value );
                            }
                        }

                        if( unMatchedOrders.Count > 0 )
                        {
                            foreach( var info in unMatchedOrders )
                            {
                                if( wsTracker.Associated.ContainsKey( info.ClientOrderId  ) )
                                {
                                    if( await wsTracker.CancelAssociatedOrders( "ETH-EUR", info.ClientOrderId ) )
                                    {
                                        matchingOrders.Add( info );
                                        pre1.Size += info.FilledSize;
                                    }
                                }
                                else
                                {
                                    matchingOrders.Add( info );
                                    pre1.Size += info.FilledSize;
                                }
                            }

                            if( matchingOrders.Count > 0 )
                            {
                                
                                SendOrder( pre1, unMatchedOrders.ToArray() );
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

        public async Task TestSendOrder2( string input )
        {
            try
            {
                // currency-b-price
                OrderInfo toCancel = null;
                if( input.Split( '-' )[ 0 ] == "B" )
                {
                    if( double.Parse( input.Split( '-' )[ 1 ], new CultureInfo( "En-Us" ) ) < 1560 )
                    {
                        PreOrder pre1 = new PreOrder( "ETH-EUR", DateTime.UtcNow, true );
                        pre1.Price = double.Parse( input.Split( '-' )[ 1 ], new CultureInfo( "En-Us" ) );

                        TryPlaceOrder( pre1 );
                    }

                }
                else if( input.Split( '-' )[ 0 ] == "S" )
                {
                    if( double.Parse( input.Split( '-' )[ 1 ], new CultureInfo( "En-Us" ) ) > 1530 )
                    {
                        PreOrder pre1 = new PreOrder( "ETH-EUR", DateTime.UtcNow, false );
                        pre1.Price = double.Parse( input.Split( '-' )[ 1 ], new CultureInfo( "En-Us" ) );

                        List<OrderInfo> unMatchedOrders = new List<OrderInfo>();
                        List<OrderInfo> matchingOrders = new List<OrderInfo>();

                        if( wsTracker.UnMatched.ContainsKey( "ETH-EUR" ) )
                        {
                            foreach( var pair in wsTracker.UnMatched[ "ETH-EUR" ] )
                            {
                                unMatchedOrders.Add( pair.Value );
                            }
                        }

                        if( unMatchedOrders.Count > 0 )
                        {
                            foreach( var info in unMatchedOrders )
                            {
                                if( wsTracker.Associated.ContainsKey( info.ClientOrderId ) )
                                {
                                    if( await wsTracker.CancelAssociatedOrders( "ETH-EUR", info.ClientOrderId ) )
                                    {
                                        matchingOrders.Add( info );
                                        pre1.Size += info.FilledSize;
                                    }
                                }
                                else
                                {
                                    matchingOrders.Add( info );
                                    pre1.Size += info.FilledSize;
                                }
                            }

                            if( matchingOrders.Count > 0 )
                            {
                                SendOrder( pre1, unMatchedOrders.ToArray() );
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

        private string GetProfileId(string profileName)
        {
            try
            {
                var resp = reqMaker.GetProfilesRequest();
                List<Profile> profiles;
                string id = "";

                if (resp.Content.Length != 0)
                {
                    profiles = JsonConvert.DeserializeObject<List<Profile>>(resp.Content);

                    foreach (Profile item in profiles)
                    {
                        if (item.Name == profileName)
                        {
                            id = item.Id;
                        }
                    }
                }

                return id;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
                Console.WriteLine(e.Message);
                return "";
            }
        }

        private async Task TryPlaceOrder(PreOrder prel)
        {
            try
            {
                accounts.FetchAccounts( ref reqMaker );

                //// Tracker updates
                //tracker.UpdateTrackedProduct( ref reqMaker, prel.ProductId );

                bool activeOrder = false;
                bool tooManyLogged = false;
                bool tooClose = false;
                bool recentBuy = false;

                int count;

                OrderInfo activeBuyOrder = null;
                List<FillInfo> fillsOnCanceled = new List<FillInfo>();

                if( prel.B )
                {
                    // check active orders
                    if( wsTracker.ActiveOrders[ prel.ProductId ].Count > 0 )
                    {
                        foreach( var pair in wsTracker.ActiveOrders[ prel.ProductId ] )
                        {
                            if( pair.Value.Side == "BUY" )
                            {
                                activeOrder = true;
                                activeBuyOrder = pair.Value;
                            }
                        }
                    }
                    else
                    {
                        // activeOrder = false
                    }

                    // check logged orders
                    if( wsTracker.UnMatched[ prel.ProductId].Count > maxBuys )
                    {
                        tooManyLogged = true;
                    }
                    
                    // check if there was a recent buy or a previous buy price is too close
                    OrderInfo trackedUnMatched = null;
                    prel.Price = Math.Round( prel.Price, productInfos[ prel.ProductId ].QuotePrecision );
                    count = wsTracker.UnMatched[ prel.ProductId ].Count;

                    // for every unmatched order in tracker
                    foreach( var pair in wsTracker.UnMatched[prel.ProductId] )
                    {
                        trackedUnMatched = pair.Value;

                        // if too recent
                        if( trackedUnMatched.Time > DateTime.UtcNow.AddMinutes( -30 ) )
                        {
                            tooClose = true;
                        }

                        if( Math.Abs( trackedUnMatched.Price - prel.Price ) < orderSpreadPercent * prel.Price )
                        {
                            tooClose = true;

                            //check if close order is incomplete
                            if( Math.Round( trackedUnMatched.Price * trackedUnMatched.FilledSize,
                                    productInfos[ prel.ProductId ].QuotePrecision ) < eurAm )
                            {
                                // close order is not complete
                                tooClose = false;
                                recentBuy = false;
                                prel.Complementary = true;
                                
                                // calculate remaining size etc
                                double trackedUnMatchedQuoteSize = trackedUnMatched.FilledSize * trackedUnMatched.Price;
                                double remainingQuoteSize = eurAm - trackedUnMatchedQuoteSize;
                                double complementaryBaseSize = Math.Round(remainingQuoteSize / prel.Price,
                                                            productInfos[ prel.ProductId].BasePrecision );
                                double complementaryQuoteSize = Math.Round( complementaryBaseSize * prel.Price,
                                                                        productInfos[ prel.ProductId ].QuotePrecision );

                                if( complementaryQuoteSize < productInfos[ prel.ProductId].QuoteMinSize )
                                {
                                    prel.Complementary = false;
                                    tooClose = true;
                                }
                                else if( complementaryQuoteSize < 0 )
                                {
                                    throw new Exception( "Maths wrong" );
                                }
                                else
                                {
                                    prel.Size = complementaryBaseSize;
                                }

                                // bold break statement
                                break;
                            }
                        }
                    }

                    if( (!(recentBuy || tooManyLogged || tooClose)) && activeOrder )
                    {
                        if( activeBuyOrder.Price != Math.Round( prel.Price, productInfos[ prel.ProductId ].QuotePrecision ) )
                        {
                            if( trackedUnMatched != null )
                            {
                                if( await wsTracker.CancelOrder( activeBuyOrder.ProductId, activeBuyOrder.ClientOrderId, activeBuyOrder.Order_Id ) )
                                {
                                    if( wsTracker.UnMatched[ prel.ProductId ][ trackedUnMatched.ClientOrderId ].FilledSize
                                        == trackedUnMatched.FilledSize )
                                    {
                                        if( accounts.Accounts[ prel.ProductId.Split( '-' )[ 1 ] ].BalanceDouble >= eurAm )
                                        {
                                            if( analyser.DataHandler.Fetcher.CheckUserSocket() )
                                            {
                                                await SendOrder( prel, null );
                                            }
                                            else
                                            {
                                                throw new Exception( "Usersocket disconnected" );
                                            }
                                        }
                                    }
                                    else
                                    {
                                        double[] complementarySizes =
                                            CalculateRemainingOrder( wsTracker.UnMatched[ prel.ProductId ][ trackedUnMatched.ClientOrderId ], prel );

                                        if( complementarySizes[ 1 ] >= productInfos[ prel.ProductId ].QuoteMinSize &&
                                            complementarySizes[ 0 ] >= productInfos[ prel.ProductId ].BaseMinSize )
                                        {
                                            prel.Size = complementarySizes[ 0 ];

                                            if( accounts.Accounts[ prel.ProductId.Split( '-' )[ 1 ] ].BalanceDouble >= eurAm )
                                            {
                                                if( analyser.DataHandler.Fetcher.CheckUserSocket() )
                                                {
                                                    await SendOrder( prel, null );
                                                }
                                                else
                                                {
                                                    throw new Exception( "Usersocket disconnected" );
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                OrderInfo cancelledOrder =
                                    await wsTracker.CancelReturnOrder( activeBuyOrder.ProductId, activeBuyOrder.ClientOrderId, activeBuyOrder.Order_Id );

                                if( cancelledOrder != null )
                                {
                                    if( ( cancelledOrder.FilledSize * cancelledOrder.Price ) < eurAm )
                                    {
                                        double[] remaining = CalculateRemainingOrder( cancelledOrder, prel );

                                        if( remaining[0] >= productInfos[prel.ProductId].BaseMinSize )
                                        {
                                            prel.Size = remaining[ 0 ];

                                            if( accounts.Accounts[ prel.ProductId.Split( '-' )[ 1 ] ].BalanceDouble >= eurAm )
                                            {
                                                if( analyser.DataHandler.Fetcher.CheckUserSocket() )
                                                {
                                                    await SendOrder( prel, null );
                                                }
                                                else
                                                {
                                                    throw new Exception( "Usersocket disconnected" );
                                                }
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    writer.Write( "No fills on cancelled order" );
                                }
                            }
                        }
                    }
                    else if( !(recentBuy || tooManyLogged || tooClose) )
                    {
                        if( accounts.Accounts[ prel.ProductId.Split( '-' )[ 1 ] ].BalanceDouble >= eurAm )
                        {
                            if( analyser.DataHandler.Fetcher.CheckUserSocket() )
                            {
                                await SendOrder( prel, null );
                            }
                            else
                            {
                                throw new Exception( "Usersocket disconnected" );
                            }
                        }
                    }
                }
                else
                {
                    List<OrderInfo> matchingBuyOrders;
                    double roundedPrelPrice = Math.Round( prel.Price, productInfos[ prel.ProductId ].QuotePrecision );
                    prel.Size = 0;

                    matchingBuyOrders = FindCancelAssociatedOrders( prel.ProductId, roundedPrelPrice, false ).Result;


                    if( matchingBuyOrders != null )
                    {
                        if( matchingBuyOrders.Count > 0)
                        {
                            
                            foreach( var order in matchingBuyOrders )
                            {
                                prel.Size += order.FilledSize;
                            }

                            prel.Size = Math.Round( prel.Size, productInfos[ prel.ProductId ].BasePrecision );

                            double accountSize = accounts.Accounts[ prel.ProductId.Split( '-' )[ 0 ] ].BalanceDouble;
                            writer.Write( $"AccountSize = {accountSize}, preliminary order size = {prel.Size}" );

                            if( accounts.Accounts[ prel.ProductId.Split( '-' )[ 0 ] ].BalanceDouble >= prel.Size
                                    && (prel.Size >= productInfos[ prel.ProductId ].BaseMinSize) )
                            {
                                if( analyser.DataHandler.Fetcher.CheckUserSocket() )
                                {
                                    await SendOrder( prel, matchingBuyOrders.ToArray() );
                                }
                                else
                                {
                                    throw new Exception( "Usersocket disconnected" );
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
                Console.WriteLine(e.Message);
            }
        }

        private double[] CalculateRemainingOrder( OrderInfo prevOrder, PreOrder preliminaryOrder )
        {
            try
            {
                // calculate remaining size etc
                double trackedUnMatchedQuoteSize = prevOrder.FilledSize * prevOrder.Price;
                double remainingQuoteSize = eurAm - trackedUnMatchedQuoteSize;
                double complementaryBaseSize = Math.Round( remainingQuoteSize / preliminaryOrder.Price,
                                            productInfos[ preliminaryOrder.ProductId ].BasePrecision );
                double complementaryQuoteSize = Math.Round( complementaryBaseSize * preliminaryOrder.Price,
                                                        productInfos[ preliminaryOrder.ProductId ].QuotePrecision );

                return new double[] { complementaryBaseSize, complementaryQuoteSize };
            }
            catch( Exception e )
            {
                Console.WriteLine( e.StackTrace );
                Console.WriteLine( e.Message );
                return null;
            }
        }

        private async Task<List<OrderInfo>> FindCancelAssociatedOrders( string productId, double prelPrice, bool sellOff )
        {
            try
            {
                List<OrderInfo> matchingBuyOrders = new List<OrderInfo>();
                Dictionary<string, string> hasActiveAssociated = new Dictionary<string, string>();
                List<string> activeSamePriceIds = new List<string>();
                int count;

                // find matching orders
                foreach( var pair in wsTracker.UnMatched[ productId ] )
                {
                    //if( prelPrice > requiredSellPercent * pair.Value.Price )
                    if ( (prelPrice - ( requiredSellPercent * pair.Value.Price )) >= ( 0.005 * requiredSellPercent * pair.Value.Price ) ||
                        ( prelPrice > requiredSellPercent * pair.Value.Price ))
                    {
                        matchingBuyOrders.Add( pair.Value );
                    }
                }

                if( sellOff )
                {
                    foreach( var pair in wsTracker.UnMatched[ productId ] )
                    {
                        if( (prelPrice < 0.98 * pair.Value.Price) &&
                            matchingBuyOrders.Where(o => o.ClientOrderId == pair.Value.ClientOrderId).ToList().Count == 0)
                        {
                            matchingBuyOrders.Add( pair.Value );
                        }
                    }
                }

                // find any orders associated to matching orders
                if( matchingBuyOrders.Count > 0 )
                {
                    foreach( var order in matchingBuyOrders )
                    {
                        if( wsTracker.Associated.ContainsKey( order.ClientOrderId ) )
                        {
                            hasActiveAssociated[ order.ClientOrderId ] = wsTracker.Associated[ order.ClientOrderId ];

                            if( wsTracker.ActiveOrders[ productId ].ContainsKey( hasActiveAssociated[ order.ClientOrderId ] ) )
                            {
                                if( wsTracker.ActiveOrders[ productId ][ hasActiveAssociated[ order.ClientOrderId ] ].Price
                                    == prelPrice )
                                {
                                    activeSamePriceIds.Add(
                                        wsTracker.ActiveOrders[ productId ][ hasActiveAssociated[ order.ClientOrderId ] ].ClientOrderId );
                                }
                            }
                        }
                    }
                }

                count = matchingBuyOrders.Count;
                for( int i = 0; i < count; i++ )
                {
                    if( hasActiveAssociated.ContainsKey( matchingBuyOrders[ i ].ClientOrderId ) )
                    {
                        if( !activeSamePriceIds.Contains( hasActiveAssociated[ matchingBuyOrders[ i ].ClientOrderId ] ) )
                        {
                            if( wsTracker.ActiveOrders[ productId ].ContainsKey( hasActiveAssociated[ matchingBuyOrders[ i ].ClientOrderId ] ) )
                            {
                                if( await wsTracker.CancelOrder( productId
                                                             ,
                                                             wsTracker.ActiveOrders[ productId ]
                                                             [ hasActiveAssociated[ matchingBuyOrders[ i ].ClientOrderId ] ].ClientOrderId
                                                             ,
                                                              wsTracker.ActiveOrders[ productId ]
                                                             [ hasActiveAssociated[ matchingBuyOrders[ i ].ClientOrderId ] ].Order_Id ) )
                                {
                                    if( wsTracker.UnMatched[ productId ].ContainsKey( matchingBuyOrders[ i ].ClientOrderId ) )
                                    {
                                        // cancelled and still in tracker, keep
                                    }
                                    else
                                    {
                                        hasActiveAssociated.Remove( matchingBuyOrders[ i ].ClientOrderId );
                                        matchingBuyOrders.RemoveAt( i );
                                        count--;
                                    }
                                }
                                else
                                {
                                    hasActiveAssociated.Remove( matchingBuyOrders[ i ].ClientOrderId );
                                    matchingBuyOrders.RemoveAt( i );
                                    count--;
                                }
                            }
                            else
                            {
                                // remove associated
                                hasActiveAssociated.Remove( matchingBuyOrders[ i ].ClientOrderId );
                                matchingBuyOrders.RemoveAt( i );
                                count--;
                            }
                        }
                        else
                        {
                            // same price, remove
                            matchingBuyOrders.RemoveAt( i );
                            count--;
                        }
                    }
                    else
                    {
                        // no active associated order, keep
                    }
                }

                return matchingBuyOrders;
            }
            catch( Exception e )
            {
                Console.WriteLine( e.StackTrace );
                Console.WriteLine( e.Message );
                return null;
            }
        }

        private async Task TryPlaceSellOff( PreOrder prelOrder )
        {
            try
            {

                List<OrderInfo> unMatched = await FindCancelAssociatedOrders( prelOrder.ProductId, prelOrder.Price, true );

                if( unMatched.Count > 0 )
                {
                    writer.Write( $"Sell off triggered for {prelOrder.ProductId} at {prelOrder.Price} " );
                    writer.Write( "Mathching orders:" );
                    foreach( var item in unMatched )
                    {
                        writer.Write( $"{item.FilledSize} {prelOrder.ProductId.Split( '-' )[0]} at {item.Price}" );
                    }
                    accounts.FetchAccounts( ref reqMaker );
                    double balance = Math.Round( accounts.Accounts[ prelOrder.ProductId.Split( '-' )[ 0 ] ].BalanceDouble,
                            productInfos[ prelOrder.ProductId ].BasePrecision );

                    if( balance > 0 )
                    {
                        if( unMatched.Count > 0 )
                        {
                            List<OrderInfo> associated = new List<OrderInfo>();
                            prelOrder.Size = 0;

                            foreach( var order in unMatched )
                            {
                                associated.Add( order );
                                prelOrder.Size += order.FilledSize;
                            }

                            if( prelOrder.Size >= productInfos[ prelOrder.ProductId ].BaseMinSize )
                            {
                                SendOrder( prelOrder, associated.ToArray() );
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

        private async Task SendOrder( PreOrder prel, OrderInfo[] associatedOrders )
        {
            try
            {
                LimitOrder order;
                string orderString;
                RestResponse orderResp = null;
                OrderInfoResponse respOrderInfo;
                double size;
                string sizeString;
                string priceString;
                string guidString;
                int sentCount = 0;

                if( prel.B )
                {
                    if( !prel.Complementary )
                    {
                        size = Math.Round( eurAm / prel.Price, productInfos[ prel.ProductId ].BasePrecision );
                    }
                    else
                    {
                        size = prel.Size;
                    }

                    sizeString = size.ToString( $"F{productInfos[ prel.ProductId ].BasePrecision}", new CultureInfo( "En-Us" ) );

                    do
                    {
                        priceString = prel.Price.ToString( $"F{productInfos[ prel.ProductId ].QuotePrecision}", new CultureInfo( "En-Us" ) );
                        guidString = Guid.NewGuid().ToString();

                        if( prel.ProductId.Split('-')[1] == "USD")
                        {
                            order = new LimitOrder( guidString,
                                               $"{prel.ProductId.Split( '-' )[ 0 ]}-USDC",
                                               "BUY",
                                               new OrderConfiguration( new LimitGtc( sizeString,
                                                                                    priceString,
                                                                                    true ) ) );
                        }
                        else
                        {
                            order = new LimitOrder( guidString,
                                               prel.ProductId,
                                               "BUY",
                                               new OrderConfiguration( new LimitGtc( sizeString,
                                                                                    priceString,
                                                                                    true ) ) );
                        }
                        

                        orderString = JsonConvert.SerializeObject( order );
                        orderResp = reqMaker.SendAuthRequest( $@"api/v3/brokerage/orders", Method.Post, orderString );

                        sentCount++;

                        if( orderResp.IsSuccessful )
                        {
                            respOrderInfo = JsonConvert.DeserializeObject<OrderInfoResponse>( orderResp.Content );

                            if( respOrderInfo.Success )
                            {
                                await wsTracker.AddOrder( respOrderInfo, null );

                                break;
                            }
                            else if( sentCount % 10 == 0 )
                            {
                                Thread.Sleep( 500 );
                            }
                            else
                            {
                                prel.Price -= productInfos[ prel.ProductId ].QuoteIncrement;
                            }
                        }
                        else
                        {
                            throw new Exception( "Request failed" );
                        }
                    } while( sentCount < 100 ); 
                }
                else
                {
                    size = prel.Size;
                    guidString = Guid.NewGuid().ToString();
                    sizeString = size.ToString( $"F{productInfos[ prel.ProductId ].BasePrecision}", new CultureInfo( "En-Us" ) );

                    do
                    {
                        priceString = prel.Price.ToString( $"F{productInfos[ prel.ProductId ].QuotePrecision}", new CultureInfo( "En-Us" ) );
                        if( prel.ProductId.Split( '-' )[ 1 ] == "USD" )
                        {
                            order = new LimitOrder( guidString,
                                               $"{prel.ProductId.Split( '-' )[ 0 ]}-USDC",
                                               "SELL",
                                               new OrderConfiguration( new LimitGtc( sizeString,
                                                                                    priceString,
                                                                                    true ) ) );
                        }
                        else
                        {
                            order = new LimitOrder( guidString,
                                               prel.ProductId,
                                               "SELL",
                                               new OrderConfiguration( new LimitGtc( sizeString,
                                                                                    priceString,
                                                                                    true ) ) );
                        }


                        orderString = JsonConvert.SerializeObject( order );
                        orderResp = reqMaker.SendAuthRequest( $@"api/v3/brokerage/orders", Method.Post, orderString );
                        sentCount++;

                        if( orderResp.IsSuccessful )
                        {
                            respOrderInfo = JsonConvert.DeserializeObject<OrderInfoResponse>( orderResp.Content );

                            if( respOrderInfo.Success )
                            {
                                writer.Write( $"Associating sell order {respOrderInfo.Success_Response.Client_Order_Id} with:" );
                                foreach( var orderInfo in associatedOrders )
                                {
                                    writer.Write( $"{orderInfo.ClientOrderId}" );
                                }
                                
                                await wsTracker.AddOrder( respOrderInfo, associatedOrders.Select<OrderInfo, string>( i => i.ClientOrderId ).ToArray() );

                                break;
                            }
                            else if( sentCount % 10 == 0 )
                            {
                                Thread.Sleep( 500 );
                            }
                            else
                            {
                                prel.Price += productInfos[ prel.ProductId ].QuoteIncrement;
                            }
                            //tracker.TrackSellOrder( respOrderInfo , associatedOrders);
                            //Thread.Sleep( 6000 );
                            //tracker.UpdateTrackedProduct( ref reqMaker, respOrderInfo.ProductId );

                            //writer.Write( $"Sell order sent {respOrderInfo.ProductId} - {respOrderInfo.Price} - {respOrderInfo.Size}" );
                        }
                        else
                        {
                            throw new Exception( "Request failed" );
                        }
                        

                    } while( sentCount < 100 );
                }
            }
            catch( Exception e)
            {
                Console.WriteLine( e.StackTrace );
                Console.WriteLine( e.Message );
            }
            
        }

        private void PreOrderReadyEvent(object source, PreOrderReadyEventArgs e)
        {
            try
            {
                lock( tryRoot )
                {
                    string type;

                    if( e.PreliminaryOrder.SellOff )
                    {
                        if( lastTries.ContainsKey(e.PreliminaryOrder.ProductId) )
                        {
                            if( lastTries[ e.PreliminaryOrder.ProductId ] < DateTime.UtcNow.AddSeconds( -30 ) )
                            {
                                if( analyser.DataHandler.Fetcher.CheckUserSocket() )
                                {
                                    //writer.Write( $"Selloff {e.PreliminaryOrder.ProductId} at {e.PreliminaryOrder.Price}" );
                                    TryPlaceSellOff( e.PreliminaryOrder );
                                    lastTries[ e.PreliminaryOrder.ProductId ] = DateTime.UtcNow;
                                }
                                else
                                {
                                    throw new UserSocketDownException();
                                }
                            }
                        }
                        else
                        {
                            if( analyser.DataHandler.Fetcher.CheckUserSocket() )
                            {
                                //writer.Write( $"Selloff {e.PreliminaryOrder.ProductId} at {e.PreliminaryOrder.Price}" );
                                TryPlaceSellOff( e.PreliminaryOrder );
                                lastTries[ e.PreliminaryOrder.ProductId ] = DateTime.UtcNow;
                            }
                            else
                            {
                                throw new UserSocketDownException();
                            }
                        }
                        
                    }
                    else if( lastTries.ContainsKey( e.PreliminaryOrder.ProductId ) )
                    {
                        if( lastTries[ e.PreliminaryOrder.ProductId ] < DateTime.UtcNow.AddSeconds( -30 ) )
                        {
                            if( e.PreliminaryOrder.B )
                            {
                                type = "buy";

                                if( analyser.DataHandler.Fetcher.CheckUserSocket() )
                                {
                                    TryPlaceOrder( e.PreliminaryOrder );
                                }
                                else
                                {
                                    throw new UserSocketDownException();
                                }
                            }
                            else
                            {
                                type = "sell";
                                if( analyser.DataHandler.Fetcher.CheckUserSocket() )
                                {
                                    TryPlaceOrder( e.PreliminaryOrder );
                                }
                                else
                                {
                                    throw new UserSocketDownException();
                                }
                            }

                            lastTries[ e.PreliminaryOrder.ProductId ] = DateTime.UtcNow;
                            writer.Write( $"Preliminary order {e.PreliminaryOrder.ProductId} {type} at {e.PreliminaryOrder.Price} - {DateTime.UtcNow}" );
                        }
                    }
                    else
                    {
                        if( e.PreliminaryOrder.B )
                        {
                            type = "buy";

                            if( analyser.DataHandler.Fetcher.CheckUserSocket() )
                            {
                                TryPlaceOrder( e.PreliminaryOrder );
                            }
                            else
                            {
                                throw new UserSocketDownException();
                            }
                        }
                        else
                        {
                            type = "sell";
                            if( analyser.DataHandler.Fetcher.CheckUserSocket() )
                            {
                                TryPlaceOrder( e.PreliminaryOrder );
                            }
                            else
                            {
                                throw new UserSocketDownException();
                            }
                        }

                        lastTries[ e.PreliminaryOrder.ProductId ] = DateTime.UtcNow;
                        writer.Write( $"Preliminary order {e.PreliminaryOrder.ProductId} {type} at {e.PreliminaryOrder.Price} - {DateTime.UtcNow}" );
                    }
                }
            }
            catch( UserSocketDownException ex )
            {
                
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine(ex.Message);
            }
        }

        private ConcurrentDictionary<string, ProductInfo> productInfos;
        //private Dictionary<string, int> buyCount;
        private Dictionary<string, DateTime> lastTries;
        private Dictionary<string, string> productAliases;

        private SynchronizedConsoleWriter writer;
        private AsyncOrderTracker wsTracker;
        private AsyncOrderLogger aLogger;
        private RequestMaker reqMaker;
        private AccountManager accounts;
        private DataAnalyser analyser;

        string profileId;
        double eurAm;
        int maxBuys;
        double orderSpreadPercent;
        double requiredSellPercent;
    }

    internal class UserSocketDownException : Exception
    {
        
    }
}
