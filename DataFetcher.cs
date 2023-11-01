using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Concurrent;
using WebSocketSharp;
using Newtonsoft.Json;
using System.Timers;
using System.Globalization;
using RestSharp;
using System.Net.Http;

namespace CBApp1
{
    //subscribes to websocket feed(s), recieves and processes data for DataHandler
    public class DataFetcher
    {
        private readonly object shortCandlesRoot = new object();
        private readonly object idsRoot = new object();
        private readonly object ticksRoot = new object();
        private readonly object matchesRoot = new object();
        private readonly object beatsRoot = new object();
        private readonly object dequeueRoot = new object();
        public DataFetcher( ref System.Timers.Timer aTimer,
                           ref SynchronizedConsoleWriter writer,
                           ref Authenticator auth,
                           ref RequestMaker req,
                           int candleSize,
                           params string[] products )
        {
            tickQueue = new ConcurrentQueue<Tick>();
            matchQueue = new ConcurrentQueue<Match>();
            beatQueue = new ConcurrentQueue<Heartbeat>();
            shortCandles = new ConcurrentDictionary<string, Candle>();
            longCandles = new ConcurrentDictionary<string, Candle>();
            constructedLongCandle = new ConcurrentDictionary<string, bool>();
            tradeIds = new ConcurrentDictionary<string, ConcurrentStack<int>>();
            firstTradeIds = new ConcurrentDictionary<string, int>();
            lastTradeIds = new ConcurrentDictionary<string, int>();
            this.products = new List<string>(products);
            this.productInfos = new ConcurrentDictionary<string, ProductInfo>();
            this.candleSize = candleSize;

            this.writer = writer;

            reqMaker = req;

            infoFetcher = new InfoFetcher(ref reqMaker);

            this.timer = aTimer;

            ProductInfo productInfo = null;
            
            foreach (string product in products)
            {
                shortCandles[ product ] = null;
                longCandles[ product ] = null;
                constructedLongCandle[ product ] = false;
                tradeIds[ product ] = new ConcurrentStack<int>();
                while( productInfo == null )
                {
                    productInfo = infoFetcher.GetProductInfo( product );
                    if( productInfo != null )
                    {
                        productInfos[ product ] = productInfo;
                        productInfo = null;
                        break;
                    }
                }
                
                firstTradeIds[ product ] = -1;
                lastTradeIds[ product ] = -1;
            }

            //TestCode();

            aTimer = new System.Timers.Timer(500);
            aTimer.AutoReset = true;
            aTimer.Enabled = true;

            //Create websocket, 
            marketSocketHandler = new WebSocketHandler( @"wss://advanced-trade-ws.coinbase.com",
                                                 auth,
                                                 new string[] { "market_trades", "heartbeats" },
                                                 products );
            marketSocket = marketSocketHandler.Ws;

            userSocketHandler = new WebSocketHandler( @"wss://advanced-trade-ws.coinbase.com",
                                                 auth,
                                                 new string[] { "user", "heartbeats" },
                                                 new string[] { } );
            userSocket = userSocketHandler.Ws;

            if( marketSocketHandler.TryConnectWebSocket() )
            {
                WaitToFive();
                candleStart = DateTime.UtcNow;
                aTimer.Elapsed += this.OnTimedEvent;
                marketSocket.OnMessage += this.MarketSocket_OnMessage;
            }
            else
            {
                throw new DataFetcherException( "Failed to connect Websocket!" );
            }
        }

        private void WaitToFive()
        {
            DateTime now = DateTime.Now;
            if (now.Minute % 5 != 0 ||
                (now.Minute % 5 == 0 &&
                !(now.Second < 3)))
            {
                int sleepTime = 5 - (now.Minute % 5);

                int count = ((sleepTime - 1) * 60) + (60 - now.Second);

                Console.WriteLine($"UTC {DateTime.UtcNow} : Waiting {count} seconds until next 5 minute mark to synchronize");
                while (count != 0)
                {

                    Thread.Sleep(1000);
                    count--;
                }
            }
            Console.WriteLine("Running");
        }
        private ProductInfo GetProductInfo(string product)
        {
            return infoFetcher.GetProductInfo(product);
        }
        private void TestCode()
        {
            GetProductHistoricCandles( "ETH-EUR", "FIVE_MINUTE", DateTime.UtcNow, DateTime.UtcNow.AddHours( -1 ), 200 );
        }
        public async Task<LimitedDateTimeList<Candle>> GetProductHistoricCandles(string productId, string granularity, DateTime startTime, 
            DateTime endTime, int candleCount)
        {
            try
            {
                // start and end switched
                int startUnix = GetUnixTime( startTime );
                int endUnix = GetUnixTime( endTime );
                string reqPath;
                string queryParams;
                string resp;

                reqPath =
                    $@"api/v3/brokerage/products/{productId}/candles";
                queryParams = 
                    $"?start={startUnix}&" +
                    $"end={endUnix}&" +
                    $"granularity={granularity}";

                resp = await reqMaker.SendAuthRequest( reqPath, queryParams, HttpMethod.Get, "" );

                if( resp != null )
                {
                    CandleHolder<Candle> holder2 = JsonConvert.DeserializeObject<CandleHolder<Candle>>( resp );

                    return new LimitedDateTimeList<Candle>( holder2.Candles, candleCount, true );
                }
                else
                {
                    return null;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
                return null;
            }
        }

        private int GetCurrentUnixTime()
        {
            return (int)DateTime.UtcNow.Subtract( new DateTime( 1970, 1, 1 ) ).TotalSeconds;
        }

        private int GetUnixTime(DateTime time)
        {
            return (int)time.Subtract( new DateTime( 1970, 1, 1 ) ).TotalSeconds;
        }

        //Get historic candles from last 24h
        public Dictionary<string, Stack<Candle>> GetHistoricCandles()
        {
            try
            {
                //Get 5-minute candles for last 24H
                Dictionary<string, Stack<Candle>> historicCandles = new Dictionary<string, Stack<Candle>>();
                int granularity = 300;
                DateTime startTime = DateTime.UtcNow.AddDays(-1);
                DateTime endTime = DateTime.UtcNow;

                SimpleRequestMaker reqMaker = new SimpleRequestMaker(@"https://api.exchange.coinbase.com");
                foreach (string product_id in products)
                {
                    string reqPath = $"/products/{product_id}/candles?granularity={granularity}&start={startTime.ToString("yyyy-MM-dd")}" +
                        $"T{startTime.ToString("HH")}" +
                        $"%3A{startTime.ToString("mm")}" +
                        $"%3A{startTime.ToString("ss")}" +
                        $".000000Z&" +
                        $"end={endTime.ToString("yyyy-MM-dd")}" +
                        $"T{endTime.ToString("HH")}" +
                        $"%3A{endTime.ToString("mm")}" +
                        $"%3A{endTime.ToString("ss")}" +
                        $".000000Z";

                    string candlesString = reqMaker.SendRequest(reqPath, RestSharp.Method.Get).Content;

                    //List<string[]> candleArrays = JsonConvert.DeserializeObject<List<string[]>>(candlesString);
                    //List<Candle> candleList = candleArrays.Select(a => new Candle(
                    //      new DateTime(1970, 1, 1).AddHours(1).AddSeconds(int.Parse(a[0])).ToString()
                    //    , a[1], a[2], a[3], a[4], a[5])
                    //).ToList();
                    List<string[]> candleArrays = JsonConvert.DeserializeObject<List<string[]>>(candlesString);
                    List<Candle> candleList = candleArrays.Select(a => new Candle(
                          new DateTime(1970, 1, 1).AddSeconds(int.Parse(a[0]))
                        , a[1], a[2], a[3], a[4], a[5])
                    ).ToList();

                    historicCandles.Add(product_id, new Stack<Candle>(candleList));
                }

                return historicCandles;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
                return null;
            }
            
        }

        private async Task ProcessMessage(string data)
        {
            try
            {
                //Create new candle(s) if required, update existing candles if needed, pass along 
                //candle(s) to DataHandler when minute is up
                WsMessage msg = JsonConvert.DeserializeObject<WsMessage>( data );

                if( msg.Channel == "user" )
                {
                    if( msg.Events.Length > 0 )
                    {
                        UserChannelUpdateEventArgs args = new UserChannelUpdateEventArgs();
                        args.messageEvent = msg.Events;
                        OnUserChannelUpdate( args );
                    }
                }
                else if( msg.Channel == "market_trades" )
                {
                    if( msg.Events.Length != 0 )
                    {
                        lock( matchesRoot )
                        {
                            if( msg.Events[ 0 ].Type != "snapshot" )
                            {
                                foreach( var trade in msg.Events[ 0 ].Trades )
                                {
                                    if( DateTime.UtcNow.Minute - trade.Time.Minute < DateTime.UtcNow.Minute % 5 &&
                                    (!(trade.Time < DateTime.UtcNow.AddMinutes( -5 ))) )
                                    {
                                        matchQueue.Enqueue( trade );
                                    }
                                    else
                                    {
                                        //writer.Write( "OLD MATCH " + data );
                                    }
                                    //writer.Write( $"{trade.Time}: {trade.Product_Id} at {trade.Price}, size: {trade.Size}" );
                                }
                            }
                            else
                            {
                                //writer.Write( $"Snapshot message: { data }");
                            }

                        }
                    }
                }
                else if( msg.Channel == "subscriptions" )
                {
                    //writer.Write( $"SUBSCRIPTION: {data}");
                }
                //else if( typeObject.Type == "heartbeat" )
                //{
                //    Heartbeat heartbeatMsg = JsonConvert.DeserializeObject<Heartbeat>( data );

                //    lock( beatsRoot )
                //    {
                //        beatQueue.Enqueue( heartbeatMsg );
                //    }
                //}
                else
                {
                    if( msg.Channel != "heartbeats" )
                    {
                        writer.Write( data );
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
        }

        private void ResetIds()
        {
            try
            {
                lock (idsRoot)
                {
                    if (copiedIds)
                    {
                        foreach (var product in products)
                        {
                            firstTradeIds[product] = -1;
                            lastTradeIds[product] = -1;
                            tradeIds[product] = new ConcurrentStack<int>();
                        }
                        copiedIds = false;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
        }

        private void ResetCandles()
        {
            try
            {
                lock (shortCandlesRoot)
                {
                    if (copiedCandles)
                    {
                        shortCandles.Clear();
                        foreach (var product in products)
                        {
                            shortCandles[product] = null;
                        }
                        copiedCandles = false;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
        }

        private void TryDequeue()
        {
            try
            {
                if (!dequeueing)
                {
                    lock (dequeueRoot)
                    {
                        if (matchQueue.Count > 0)
                        {
                            dequeueing = true;

                            DequeueMatchesAndIds();

                            dequeueing = false;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
        }
        private void DequeueMatchesAndIds()
        {
            try
            {
                Queue<Match> matches;
                Queue<Heartbeat> beats;
                lock (matchesRoot)
                {
                    //copy matches
                    matches = new Queue<Match>(matchQueue);
                    matchQueue = new ConcurrentQueue<Match>();
                }
                lock (beatsRoot)
                {
                    //copy beats
                    beats = new Queue<Heartbeat>(beatQueue);
                    beatQueue = new ConcurrentQueue<Heartbeat>();
                }
                lock (shortCandlesRoot)
                {
                    //if both queues not 0 dequeue one of each... and so on.
                    int matchCount = matches.Count;
                    int beatCount = beats.Count;
                    Match qMatch;
                    Heartbeat qBeat;

                    if( beatCount == 0 )
                    {
                        while( matchCount != 0 )
                        {
                            qMatch = matches.Dequeue();
                            TryAddToCandles( qMatch, ref shortCandles );
                            AddTradeIds( qMatch );
                            matchCount = matches.Count;
                        }
                    }
                    else
                    {
                        while( matchCount != 0 || beatCount != 0 )
                        {
                            if( matchCount != 0 && beatCount != 0 )
                            {
                                qMatch = matches.Dequeue();
                                qBeat = beats.Dequeue();
                                TryAddToCandles( qMatch, ref shortCandles );

                                if( qMatch.Trade_Id < qBeat.Last_Trade_Id )
                                {
                                    AddTradeIds( qMatch );
                                    AddTradeId( qBeat );
                                }
                                else if( qMatch.Trade_Id == qBeat.Last_Trade_Id )
                                {
                                    AddTradeIds( qMatch );
                                }
                                else
                                {
                                    AddTradeId( qBeat );
                                    AddTradeIds( qMatch );
                                }
                            }
                            else if( matchCount == 0 )
                            {
                                qBeat = beats.Dequeue();
                                AddTradeId( qBeat );
                            }
                            else if( beatCount == 0 )
                            {
                                qMatch = matches.Dequeue();
                                TryAddToCandles( qMatch, ref shortCandles );
                                AddTradeIds( qMatch );
                            }
                            matchCount = matches.Count;
                            beatCount = beats.Count;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
        }

        private void TryAddToCandles(Match match, ref ConcurrentDictionary<string, Candle> currentShortCandles)
        {
            try
            {
                Candle currentCandle;
                Candle newCandle = null;

                ShortCandleUpdateEventArgs args = new ShortCandleUpdateEventArgs();
                args.ProductId = match.Product_Id;

                // compare current short candle to new match, change current if needed
                currentCandle = currentShortCandles[ match.Product_Id ];
                CompareCandles( match, ref currentShortCandles, currentCandle, ref newCandle );
                if( newCandle!= null )
                {
                    args.NewShortCandle = new Candle( newCandle );
                }
                else
                {
                    throw new Exception( "Null candle" );
                }
                
                //// compare current long candle to new match, change current if needed
                //if( constructedLongCandle[match.Product_Id] )
                //{
                //    currentCandle = longCandles[ match.Product_Id ];
                //    CompareCandles( match, longCandles, currentCandle, ref newCandle );
                //    args.NewLongCandle = new Candle( newCandle );
                //}
                //else
                //{
                //    args.NewLongCandle = null;
                //}
                

                OnShortCandleUpdate( args );
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
            
        }

        private void CompareCandles( Match match, ref ConcurrentDictionary<string, Candle> currentCandles, Candle currentCandle, ref Candle newCandle )
        {
            if( currentCandle == null )
            {
                newCandle = new Candle( candleStart, match.Price, match.Price, match.Price, match.Price, match.Size );

                currentCandles[ match.Product_Id ] = newCandle;

            }
            else if( currentCandle != null )
            {

                double matchPrice = double.Parse( match.Price, new CultureInfo( "En-Us" ) );

                if( !(( matchPrice - currentCandle.High ) > 0.02*currentCandle.High ) &&
                    !(( currentCandle.Low - matchPrice ) > 0.02 * currentCandle.Low) )
                {
                    newCandle = new Candle( currentCandle.Time, 0, 0, 0, 0, 0 );
                    newCandle.Close = double.Parse( match.Price, new CultureInfo( "En-Us" ) );
                    newCandle.Open = currentCandle.Open;
                    newCandle.Volume = currentCandle.Volume + double.Parse( match.Size, new CultureInfo( "En-Us" ) );

                    if( double.Parse( match.Price, new CultureInfo( "En-Us" ) ) > currentCandle.High )
                    {
                        newCandle.High = double.Parse( match.Price, new CultureInfo( "En-Us" ) );
                    }
                    else
                    {
                        newCandle.High = currentCandle.High;
                    }
                    if( double.Parse( match.Price, new CultureInfo( "En-Us" ) ) < currentCandle.Low )
                    {
                        newCandle.Low = double.Parse( match.Price, new CultureInfo( "En-Us" ) );
                    }
                    else
                    {
                        newCandle.Low = currentCandle.Low;
                    }

                    currentCandles[ match.Product_Id ] = newCandle;
                }
                else
                {
                    currentCandles[ match.Product_Id ] = currentCandle;
                }
            }

        }

        private void AddTradeIds(Match matchmsg)
        {
            try
            {
                lock (idsRoot)
                {
                    int matchId = matchmsg.Trade_Id;
                    int peekId;
                    tradeIds[matchmsg.Product_Id].TryPeek(out peekId);

                    if (tradeIds[matchmsg.Product_Id].Count == 0)
                    {
                        tradeIds[matchmsg.Product_Id].Push(matchId);
                    }
                    else if (peekId < matchId)
                    {
                        tradeIds[matchmsg.Product_Id].Push(matchId);
                    }

                    if (firstTradeIds[matchmsg.Product_Id] == -1)
                    {
                        firstTradeIds[matchmsg.Product_Id] = matchmsg.Trade_Id;
                    }

                    if (lastTradeIds[matchmsg.Product_Id] < matchmsg.Trade_Id ||
                        lastTradeIds[matchmsg.Product_Id] == -1)
                    {
                        lastTradeIds[matchmsg.Product_Id] = matchmsg.Trade_Id;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
        }

        private void AddTradeId(Heartbeat heartbeatMsg)
        {
            try
            {
                // add tradeIds from current time-period,
                // at end, check if any are missing and 
                // 'get them'
                lock (idsRoot)
                {
                    if (firstTradeIds[heartbeatMsg.Product_Id] == -1)
                    {
                        firstTradeIds[heartbeatMsg.Product_Id] = heartbeatMsg.Last_Trade_Id;
                    }

                    if (lastTradeIds[heartbeatMsg.Product_Id] < heartbeatMsg.Last_Trade_Id ||
                        lastTradeIds[heartbeatMsg.Product_Id] == -1)
                    {
                        lastTradeIds[heartbeatMsg.Product_Id] = heartbeatMsg.Last_Trade_Id;
                    }
                }
                
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
        }
        private void CopyCandlesAndIds(ref Caid candlesAndIds)
        {
            try
            {
                Dictionary<string, Candle> copiedShortCandles = new Dictionary<string, Candle>();
                Dictionary<string, Stack<int>> copiedIds = new Dictionary<string, Stack<int>>();
                Dictionary<string, int> firstIds = new Dictionary<string, int>();
                Dictionary<string, int> lastIds = new Dictionary<string, int>();

                lock (shortCandlesRoot)
                {
                    foreach (var product in products)
                    {
                        copiedShortCandles[product] = shortCandles[product];
                        shortCandles[product] = null;
                    }
                }                
                lock (idsRoot)
                {
                    foreach (var product in products)
                    {
                        copiedIds[product] = new Stack<int>(tradeIds[product]);
                        firstIds[product] = firstTradeIds[product];
                        lastIds[product] = lastTradeIds[product];

                        tradeIds[product].Clear();
                        firstTradeIds[product] = -1;
                        lastTradeIds[product] = -1;
                    }
                }

                candlesAndIds = new Caid(copiedShortCandles, copiedIds, firstIds, lastIds);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
        }

        private void FindAddMissingTrades(ref Caid candlesAndIds)
        {
            try
            {
                Dictionary<string, List<int>> missingIds = new Dictionary<string, List<int>>();
                Dictionary<string, Queue<Trade>> missingTrades = new Dictionary<string, Queue<Trade>>();
                Trade[] trades;
                Queue<Trade> tradeQueue;
                Trade currentTrade;
                Candle currentCandle;
                Candle newCandle;
                SimpleRequestMaker simp;
                string respContent;
                int limit = 500;

                foreach (string product in products)
                {
                    // Find missing ids between first and last ids
                    // added from heartbeats and match messages
                    if (!(candlesAndIds.FirstIds[product] == -1 ||
                        candlesAndIds.LastIds[product] == -1 ||
                        candlesAndIds.LastIds[product] - candlesAndIds.FirstIds[product] == 1 ||
                        candlesAndIds.LastIds[product] - candlesAndIds.FirstIds[product] == 0 ||
                        candlesAndIds.TradeIds[product].Count == 0 ||
                        candlesAndIds.TradeIds[product].Count == 1 ||
                        candlesAndIds.LastIds[product] - candlesAndIds.FirstIds[product] < 0))
                    {

                        missingIds[product] = FindMissingIds(product, ref candlesAndIds);

                        if (missingIds[product].Count != 0)
                        {
                            // Fetch ids between first and last
                            simp = new SimpleRequestMaker($@"https://api.exchange.coinbase.com/products/{product}/trades");
                            RestResponse resp = simp.SendRequest($@"?limit={limit}&before={candlesAndIds.FirstIds[product]}&" +
                                                            $@"after={candlesAndIds.LastIds[product]}",
                                                            Method.Get);
                            respContent = resp.Content;
                            trades = JsonConvert.DeserializeObject<Trade[]>(respContent);
                            if (trades == null)
                            {
                                Console.WriteLine("Stoppp");
                            }
                            tradeQueue = new Queue<Trade>(trades);

                            // Add missing trades to candles
                            missingTrades[product] = new Queue<Trade>();

                            int count = tradeQueue.Count;

                            for (int i = 0; i < count; i++)
                            {
                                currentTrade = tradeQueue.Dequeue();

                                if (missingIds[product].Count == 0)
                                {
                                    break;
                                }

                                int missingIdsCount = missingIds[product].Count;

                                for (int ii = 0; ii < missingIdsCount; ii++)
                                {
                                    if (missingIds[product][ii] == currentTrade.Trade_Id)
                                    {
                                        currentCandle = candlesAndIds.ShortCandles[product];
                                        double high = currentCandle.High;
                                        double low = currentCandle.Low;
                                        double volume = currentCandle.Volume;

                                        if (currentTrade.Price > high)
                                        {
                                            high = currentTrade.Price;
                                        }
                                        if (currentTrade.Price < low)
                                        {
                                            low = currentTrade.Price;
                                        }

                                        volume += currentTrade.Size;

                                        newCandle = new Candle(currentCandle.Time, low, high, currentCandle.Open,
                                                                currentCandle.Close, volume);

                                        candlesAndIds.ShortCandles[product] = newCandle;
                                        missingIds[product].RemoveAt(ii);
                                        missingIdsCount--;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
        }

        private List<int> FindMissingIds(string product, ref Caid candlesAndIds)
        {
            try
            {
                // check first and last tradeids
                // check gathered tradeids
                List<int> missingIds = new List<int>();
                //Queue<int> productIds = new Queue<int>(tradeIds[product]);
                Queue<int> productIdsQueue = new Queue<int>(candlesAndIds.TradeIds[product]);
                int first = candlesAndIds.FirstIds[product];
                int last = candlesAndIds.LastIds[product];
                int current;
                int previous = productIdsQueue.Dequeue();;
                int count = productIdsQueue.Count;

                for (int i = 0; i < count; i++)
                {
                    current = productIdsQueue.Dequeue();
                    if (current - previous > 1)
                    {
                        for (int ii = 1; ii < current - previous; ii++)
                        {
                            missingIds.Add(previous + ii);
                        }
                    }
                    else if (current - previous == 1)
                    {
                        // nope, nothing wrong here
                    }
                    else
                    {
                        throw new Exception("Trade ids not in order!");
                    }
                    previous = current;

                }

                return missingIds;
            }
            catch (Exception e)
            {
                Console.WriteLine( e.Message );
                Console.WriteLine( e.StackTrace );
                return null;
            }
        }

        private void UpdateLongCandles( ref Caid candlesAndIds, bool longPassed )
        {
            try
            {
                // update current long candles

                Dictionary<string, Candle> currentLongCandles = new Dictionary<string, Candle>( longCandles );
                Candle currentShortCandle = null;
                Candle currentLongCandle = null;

                // copy current long candles
                foreach( var product in products )
                {
                    if( constructedLongCandle[product] )
                    {
                        currentLongCandle = currentLongCandles[ product ];
                        currentShortCandle = candlesAndIds.ShortCandles[ product ];

                        if( currentLongCandle == null )
                        {
                            if( currentShortCandle != null )
                            {
                                currentLongCandle = new Candle( currentShortCandle );
                                currentLongCandle.Time.AddMinutes( currentLongCandle.Time.Minute );
                                currentLongCandle.Time.AddSeconds( currentLongCandle.Time.Second );

                                currentLongCandles[ product ] = currentLongCandle;
                            }
                            else
                            {
                                // do nothing
                            }
                        }
                        else
                        {
                            if( currentShortCandle != null )
                            {
                                currentLongCandle.Close = currentShortCandle.Close;

                                if( currentShortCandle.Low < currentLongCandle.Low )
                                {
                                    currentLongCandle.Low = currentShortCandle.Low;
                                }

                                if( currentShortCandle.High > currentLongCandle.High )
                                {
                                    currentLongCandle.High = currentShortCandle.High;
                                }

                                currentLongCandle.Volume += currentShortCandle.Volume;

                                currentLongCandles[ product ] = currentLongCandle;
                            }
                            else
                            {
                                // do nothing
                                
                            }
                        }
                    }
                    else
                    {
                        currentLongCandles[ product ] = null;
                    }
                }

                if( longPassed )
                {

                    //
                    // pass all long candles to handler
                    //

                    candlesAndIds.LongCandles = new Dictionary<string, Candle>();

                    // copy current long candles
                    foreach( var product in products )
                    {
                        candlesAndIds.LongCandles[ product ] = currentLongCandles[ product ];
                        longCandles[ product ] = null;
                    }
                }
                else
                {

                    //
                    // raise event
                    //

                    LongCandleUpdateEventArgs args = new LongCandleUpdateEventArgs();
                    args.NewLongCandles = new Dictionary<string, Candle>();

                    // copy current long candles to event args

                    foreach( var product in products )
                    {
                        args.NewLongCandles[ product ] = currentLongCandles[ product ];
                        longCandles[ product ] = currentLongCandles[ product ];
                    }

                    OnLongCandleUpdate( args );
                }
            }
            catch( Exception e )
            {
                Console.WriteLine( e.Message );
                Console.WriteLine( e.StackTrace );
            }   
        }

        public bool CheckUserSocket()
        {
            try
            {
                if( userSocket.Ping() )
                {
                    return true;
                }
                else
                {
                    Thread.Sleep( 500 );
                    if( userSocket.ReadyState != WebSocketState.Open ||
                        userSocket.ReadyState != WebSocketState.Connecting )
                    {
                        int tries = 0;
                        while( tries < 10 )
                        {
                            tries++;
                            if( userSocketHandler.TryConnectWebSocket() )
                            {
                                return true;
                            }
                        }

                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
            }
            catch( Exception e )
            {
                writer.Write( e.Message );
                writer.Write( e.StackTrace );
                return false;
            }
        }

        public bool CheckMarketSocket()
        {
            try
            {
                if( marketSocket.Ping() )
                {
                    return true;
                }
                else
                {
                    Thread.Sleep( 500 );

                    if( marketSocket.ReadyState != WebSocketState.Open ||
                        marketSocket.ReadyState != WebSocketState.Connecting )
                    {
                        int tries = 0;
                        while( tries < 10 )
                        {
                            tries++;
                            if( marketSocketHandler.TryConnectWebSocket() )
                            {
                                return true;
                            }
                        }

                        return false;
                    }
                    else
                    {
                        return true;
                    }
                     
                }
            }
            catch( Exception e )
            {
                writer.Write( e.Message );
                writer.Write( e.StackTrace );
                return false;
            }
        }

        public void SubscribeToUserChannel()
        {
            try
            {
                if( userSocketHandler.TryConnectWebSocket() )
                {
                    userSocket.OnMessage += this.UserSocket_OnMessage;
                }
            }
            catch( Exception e )
            {
                Console.WriteLine( e.Message );
                Console.WriteLine( e.StackTrace );
            }
        }

        //async
        public async void MarketSocket_OnMessage(object sender, MessageEventArgs e)
        {
            await ProcessMessage(e.Data);
        }

        public async void UserSocket_OnMessage(object sender, MessageEventArgs e)
        {
            WsMessage message = JsonConvert.DeserializeObject<WsMessage>( e.Data );
            MessageType typeObject = JsonConvert.DeserializeObject<MessageType>( e.Data );
            bool errorBool = false;

            if( typeObject.Type == "error" )
            {
                errorBool = true;

                userSocket.Close();

                Thread.Sleep( 500 );

                if( userSocket.ReadyState == WebSocketState.Closed
                    || userSocket.ReadyState == WebSocketState.Closing )
                {
                    if( userSocketHandler.TryConnectWebSocket() )
                    {
                        errorBool = false;
                    }
                }
                else
                {
                    errorBool = false;
                }
            }
            if( !errorBool )
            {
                if( message != null )
                {
                    if( message.Events != null )
                    {
                        await ProcessMessage( e.Data );
                    }
                }
            }   
        }

        protected virtual void OnShortCandleUpdate(ShortCandleUpdateEventArgs e)
        {
            EventHandler<ShortCandleUpdateEventArgs> handler = ShortCandleUpdate;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        protected virtual void OnLongCandleUpdate(LongCandleUpdateEventArgs e)
        {
            EventHandler<LongCandleUpdateEventArgs> handler = LongCandleUpdate;
            if( handler!= null )
            {
                handler( this, e );
            }
        }

        protected virtual void OnCandlesComplete(CandlesCompleteEventArgs e)
        {
            EventHandler<CandlesCompleteEventArgs> handler = CandlesComplete;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        protected virtual void OnUserChannelUpdate(UserChannelUpdateEventArgs e)
        {
            EventHandler<UserChannelUpdateEventArgs> handler = UserChannelUpdate;
            if( handler != null )
            {
                handler( this, e );
            }
        }

        public void ConstructedLongCandle(object source, ConstructedLongCandleEventArgs e)
        {
            try
            {
                if( !(e.LongCandle.Open == -1
                    || e.LongCandle.Close == -1
                    || e.LongCandle.High == -1
                    || e.LongCandle.Low == -1) )
                {
                    longCandles[ e.ProductId ] = e.LongCandle;
                }
                else
                {
                    longCandles[ e.ProductId ] = null;
                }
                
                constructedLongCandle[ e.ProductId ] = true;

            }
            catch( Exception ex)
            {
                Console.WriteLine( ex.Message );
                Console.WriteLine( ex.StackTrace );
            }
        }

        private async void OnTimedEvent(Object source, ElapsedEventArgs e)
        {
            try
            {
                // if time since start >=5 min, copy candles and raise CandlesComplete-event...
                // reset candleStart
                if( (DateTime.UtcNow - candleStart).Minutes >= 5 )
                {
                    candleStart = DateTime.UtcNow;
                    lock( dequeueRoot )
                    {
                        int seconds = candleStart.Second;
                        DateTime synchronized = candleStart.AddSeconds( -seconds );

                        candleStart = synchronized;

                        if( candleStart.Minute % 5 != 0 ||
                        (candleStart.Minute == 0 &&
                        !(candleStart.Second < 3)) )
                        {
                            throw new Exception( "candleStart unsynchronized!" );
                        }


                        CandlesCompleteEventArgs args = new CandlesCompleteEventArgs();
                        Caid candlesAndIds = null;

                        if( DateTime.UtcNow.Minute != 0 )
                        {
                            // only update fiveMinCandles
                            CopyCandlesAndIds( ref candlesAndIds );
                            //FindAddMissingTrades( ref candlesAndIds );
                            args.ShortCandles = new Dictionary<string, Candle>( candlesAndIds.ShortCandles );

                            // raise event to update current long candles in analyser

                            UpdateLongCandles( ref candlesAndIds, false );
                            args.LongCandles = null;

                            OnCandlesComplete( args );
                        }
                        else
                        {
                            // hour passed, update hourCandles
                            CopyCandlesAndIds( ref candlesAndIds );
                            //FindAddMissingTrades( ref candlesAndIds );
                            args.ShortCandles = new Dictionary<string, Candle>( candlesAndIds.ShortCandles );

                            UpdateLongCandles( ref candlesAndIds, true );
                            args.LongCandles = candlesAndIds.LongCandles;

                            OnCandlesComplete( args );
                        }
                    }
                }
                else
                {
                    await Task.Run( () =>
                    {
                        TryDequeue();
                    } );
                }

                if( e.SignalTime.Second % 2 == 0 )
                {
                    if( CheckUserSocket() )
                    {

                    }
                    else
                    {
                        throw new Exception( "UserSocket down" );
                    }

                    //if( CheckMarketSocket() )
                    //{

                    //}
                    //else
                    //{
                        //throw new Exception( "MarketSocket down" );
                    //}
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }

        public ConcurrentDictionary<string, ProductInfo> ProductInfos
        {
            get
            {
                return productInfos;
            }
        }

        private System.Timers.Timer timer;
        private SynchronizedConsoleWriter writer;
        private readonly WebSocketHandler marketSocketHandler;
        private readonly WebSocket marketSocket;
        private readonly WebSocketHandler userSocketHandler;
        private readonly WebSocket userSocket;
        private RequestMaker reqMaker;
        private InfoFetcher infoFetcher;

        private ConcurrentQueue<Tick> tickQueue;
        private ConcurrentQueue<Match> matchQueue;
        private ConcurrentQueue<Heartbeat> beatQueue;
        private ConcurrentDictionary<string, Candle> shortCandles;
        private ConcurrentDictionary<string, Candle> longCandles;
        private ConcurrentDictionary<string, int> lastTradeIds;
        private ConcurrentDictionary<string, int> firstTradeIds;
        private ConcurrentDictionary<string, ConcurrentStack<int>> tradeIds;
        private ConcurrentDictionary<string, ProductInfo> productInfos;
        private List<string> products;
        private ConcurrentDictionary<string, bool> constructedLongCandle;

        private DateTime candleStart;
        private int candleSize;
        private bool dequeueing;
        private bool copiedCandles;
        private bool copiedIds;

        public event EventHandler<CandlesCompleteEventArgs> CandlesComplete;
        public event EventHandler<ShortCandleUpdateEventArgs> ShortCandleUpdate;
        public event EventHandler<LongCandleUpdateEventArgs> LongCandleUpdate;
        public event EventHandler<UserChannelUpdateEventArgs> UserChannelUpdate;
    }

    public class UserChannelUpdateEventArgs : EventArgs
    {
        public WsMessageEvent[] messageEvent { get; set; }
    }

    public class ShortCandleUpdateEventArgs : EventArgs
    {
        public string ProductId { get; set; }
        public Candle NewShortCandle { get; set; }
    }
    public class LongCandleUpdateEventArgs : EventArgs
    {
        public Dictionary<string, Candle> NewLongCandles;
    }
    public class CandlesCompleteEventArgs : EventArgs
    {
        public Dictionary<string, Candle> ShortCandles { get; set; }
        public Dictionary<string, Candle> LongCandles { get; set; }
    }
}
