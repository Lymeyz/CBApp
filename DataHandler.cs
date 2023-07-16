using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Timers;

namespace CBApp1
{
    public class DataHandler
    {
        //Get info for each product, create datafetcher which will recieve data from websocket
        //Store data and DataAnalyser will analyse it
        //Provide threadsafe collection for each product in list

        //Get historic data in between first 5 minute candle-update

        public DataHandler( ref System.Timers.Timer aTimer,
                           ref SynchronizedConsoleWriter writer,
                           ref Authenticator auth,
                           ref RequestMaker req,
                           params string[] products )
        {
            
            try
            {
                this.aTimer = aTimer;
                shortProductCandles = new ConcurrentDictionary<string, ConcurrentQueue<Candle>>();
                longProductCandles = new ConcurrentDictionary<string, ConcurrentQueue<Candle>>();
                Products = new List<string>();
                fetchedHistoric = new Dictionary<string, bool>();
                shortCandlesAdded = new Dictionary<string, int>();
                longCandlesAdded = new Dictionary<string, int>();
                this.writer = writer;

                foreach( var product in products )
                {
                    shortProductCandles[ product ] = new ConcurrentQueue<Candle>();
                    longProductCandles[ product ] = new ConcurrentQueue<Candle>();
                    fetchedHistoric[ product ] = false;
                    shortCandlesAdded[ product ] = 0;
                    longCandlesAdded[ product ] = 0;
                    Products.Add( product );
                }

                fetcher = new DataFetcher(ref aTimer, ref writer, ref auth, ref req, 5, products);
                Fetcher = fetcher;
                fetcher.CandlesComplete += this.CandlesCompleteEvent;
                ConstructedLongCandle += fetcher.ConstructedLongCandle;
            }
            catch(DataFetcherException e)
            {
                Console.WriteLine(e.Message);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
        }

        public ConcurrentDictionary<string, ConcurrentQueue<Candle>> ShortProductCandles
        {
            get
            {
                return shortProductCandles;
            }
        }

        public ConcurrentDictionary<string, ConcurrentQueue<Candle>> LongProductCandles 
        {
            get
            {
                return longProductCandles;
            }

        }

        public List<string> Products { get; }

        public DataFetcher Fetcher { get; }
        //after 7 minutes, combine historic data and new data

        private bool UpdateProductCandles(Dictionary<string, Candle> shortCandles, Dictionary<string, Candle> longCandles)
        {
            try
            {
                writer.Write($"Updating product candles at {DateTime.UtcNow} UTC");
                Candle copy;
                bool constructedCandleCollections = false;

                foreach (string product in shortCandles.Keys)
                {
                    if( shortCandles[ product ] != null )
                    {
                        copy = new Candle(shortCandles[product]);
                        shortProductCandles[product].Enqueue(copy);
                        TrimToHours(product, 24, ref shortProductCandles);
                    }

                    if( longCandles != null )
                    {
                        if( longCandles[ product ] != null )
                        {
                            copy = new Candle( longCandles[ product ] );
                            longProductCandles[ product ].Enqueue( copy );
                            TrimToHours( product, 168, ref shortProductCandles );
                        }
                    }
                    
                    // add historic short candles, construct last long candle from data up to latest short candle
                    if (shortCandlesAdded[product] > 0 && !fetchedHistoric[product])
                    {
                        // fetch 5 min historic candles
                        // older than last time minute%5==0 && second == 0
                        // DateTime.UtcNow.AddMinute(-DateTime.UtcNow.Minute%5)

                        DateTime shortEndTime = DateTime.UtcNow.AddMinutes( -(DateTime.UtcNow.Minute % 5) )
                                                               .AddSeconds( -DateTime.UtcNow.Second );

                        AddHistoricCandles( product, ref shortProductCandles, 300, "FIVE_MINUTE", DateTime.UtcNow.AddDays( -1 ), shortEndTime);

                        // fetch hourly historic candles
                        // older than last time when minute == 0 && second == 0

                        DateTime longEndTime = DateTime.UtcNow.AddHours( -1 )
                                                              .AddMinutes( -DateTime.UtcNow.Minute )
                                                              .AddSeconds(-DateTime.UtcNow.Second);

                        AddHistoricCandles( product, ref longProductCandles, 168, "ONE_HOUR", DateTime.UtcNow.AddDays( -7 ), longEndTime );

                        // construct partial candle for last hour
                        Candle longCandle = ConstructLongCandle( product, ref shortProductCandles, 168 );

                        ConstructedLongCandleEventArgs args = new ConstructedLongCandleEventArgs();
                        args.ProductId = product;
                        args.LongCandle = new Candle( longCandle );

                        OnConstructedLongCandle( args );

                        fetchedHistoric[product] = true;
                        TrimToHours(product, 24, ref shortProductCandles);
                        TrimToHours( product, 168, ref longProductCandles );

                        constructedCandleCollections = true;
                    }
                }

                return constructedCandleCollections;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
                return false;
            }
        }

        private Candle ConstructLongCandle( string product,
                                           ref ConcurrentDictionary<string, ConcurrentQueue<Candle>> shortProductCandles,
                                           int candleCount )
        {
            try
            {
                LimitedDateTimeList<Candle> currShortCandles = new LimitedDateTimeList<Candle>( shortProductCandles[ product], 300 );
                DateTime latestCandleTime = currShortCandles.Newest.Time;
                DateTime latestHourTime = latestCandleTime.AddMinutes( -latestCandleTime.Minute ).AddSeconds( -latestCandleTime.Second );

                Candle longCandle = new Candle(latestHourTime, -1, -1, -1, -1, -1);
                Candle currShortCandle = null;

                for( int i = 0; i < currShortCandles.Count; i++ )
                {
                    currShortCandle = currShortCandles.GetRemoveNewest();

                    if( currShortCandle.Time.Hour != latestHourTime.Hour )
                    {
                        break;
                    }

                    if( longCandle.Open == -1 || longCandle.Open != currShortCandle.Open )
                    {
                        longCandle.Open = currShortCandle.Open;
                    }
                    if( longCandle.Low == -1 || longCandle.Low > currShortCandle.Low )
                    {
                        longCandle.Low = currShortCandle.Low;
                    }
                    if( longCandle.High == -1 || longCandle.High < currShortCandle.High )
                    {
                        longCandle.High = currShortCandle.High;
                    }
                    if( longCandle.Close == -1 )
                    {
                        longCandle.Close = currShortCandle.Close;
                    }

                    if( longCandle.Volume == -1 )
                    {
                        longCandle.Volume = currShortCandle.Volume;
                    }
                    else
                    {
                        longCandle.Volume += currShortCandle.Volume;
                    }
                }

                if( latestCandleTime.Minute == 55 )
                {
                    longProductCandles[ product ].Enqueue( longCandle );
                    longCandle = new Candle( latestHourTime, -1, -1, -1, -1, -1 );
                }

                return longCandle;
            }
            catch( Exception e)
            {
                Console.WriteLine( e.Message );
                Console.WriteLine( e.StackTrace );
                return null;
            }
        }

        private void AddHistoricCandles( string product,
                                        ref ConcurrentDictionary<string, ConcurrentQueue<Candle>> candleCollection,
                                        int candleCount,
                                        string granularity,
                                        DateTime startTime,
                                        DateTime endTime )
        {
            try
            {
                LimitedDateTimeList<Candle> newCandles = new LimitedDateTimeList<Candle>(candleCount);
                LimitedDateTimeList<Candle> historicCandles = 
                    fetcher.GetProductHistoricCandles( product, granularity, startTime, endTime, candleCount);

                CompileCandles(product, ref candleCollection, ref historicCandles, ref newCandles, candleCount, granularity);

                candleCollection[product] = new ConcurrentQueue<Candle>();

                int count = newCandles.Count;
                for (int i = 0; i < count; i++)
                {
                    candleCollection[product].Enqueue(newCandles.GetRemoveOldest());

                    // dont 
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
        }
        private void CompileCandles(string product, ref ConcurrentDictionary<string, ConcurrentQueue<Candle>> candleCollection,
            ref LimitedDateTimeList<Candle> historicCandles, ref LimitedDateTimeList<Candle> newCandles, int candleCount, string granularity)
        {
            try
            {
                LimitedDateTimeList<Candle> currentCandles = new LimitedDateTimeList<Candle>( candleCollection[ product ], candleCount, true );

                newCandles = new LimitedDateTimeList<Candle>( candleCount );

                int count = historicCandles.Count;
                int granularitySecondsInt = 0;

                if( granularity == "FIVE_MINUTE" )
                {
                    granularitySecondsInt = 300;
                }
                else if( granularity == "ONE_HOUR" )
                {
                    granularitySecondsInt = 3600;
                }

                // FIX GRANULARITY

                for( int i = 0; i < count; i++ )
                {
                    if( currentCandles.Count != 0 )
                    {
                        if( historicCandles.Oldest.Time < currentCandles.Oldest.Time.AddSeconds( -(granularitySecondsInt - 2) ) )
                        {
                            newCandles.AddValue( historicCandles.GetRemoveOldest() );
                        }
                        else
                        {
                            historicCandles.GetRemoveOldest();
                        }
                    }
                    else
                    {
                        newCandles.AddValue(historicCandles.GetRemoveOldest());
                    }
                }

                count = currentCandles.Count;
                for (int i = 0; i < count; i++)
                {
                    newCandles.AddValue(currentCandles.GetRemoveOldest());
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
        }
        
        private void CheckCandleOrder(Queue<Candle> candles)
        {
            try
            {
                int count = candles.Count;

                if (count > 1)
                {
                    Candle candle1 = candles.Dequeue();
                    Candle candle2;

                    count = candles.Count;
                    for (int i = 0; i < count; i++)
                    {
                        candle2 = candles.Dequeue();
                        if (candle1.Time > candle2.Time)
                        {
                            throw new Exception("Candles wrong order");
                        }
                        candle1 = candle2;
                    }

                }
                else
                {
                    throw new Exception("Queue shouldn't be empty");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
        }

        //Async hantering av ws-data
        private void CandlesCompleteEvent(object source, CandlesCompleteEventArgs e)
        {
            try
            {
                foreach (var product in e.ShortCandles.Keys)
                {
                    if( e.ShortCandles[ product ] != null )
                    {
                        //Console.WriteLine($"New candle: {product.Key} \n" +
                        //                  $"----- Time: {product.Value.Time}");

                        shortCandlesAdded[ product ]++;
                    }
                    else
                    {

                    }

                    if( e.LongCandles != null )
                    {
                        if( e.LongCandles[ product ] != null )
                        {
                            longCandlesAdded[ product ]++;
                        }
                    }
                }


                // Update _productCandles collection, OBS! Only products in e.Candles
                bool constructedCandleCollections = UpdateProductCandles(e.ShortCandles, e.LongCandles);

                // If sufficient activity in product, add historic candles
                UpdatedProductCandlesEventArgs args = new UpdatedProductCandlesEventArgs();
                if( e.LongCandles != null || constructedCandleCollections )
                {
                    args.updatedLong = true;
                }
                else
                {
                    args.updatedLong = false;
                }
                

                UpdatedProductCandles( this, args );
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }

        private void TrimToHours(string product, int hours, ref ConcurrentDictionary<string, ConcurrentQueue<Candle>> candleCollection)
        {
            try
            {
                bool tooOld;
                Candle candle;
                do
                {
                    candleCollection[ product ].TryPeek( out candle );
                    DateTime sometime = DateTime.UtcNow.AddHours( -hours );
                    if( candle.Time < DateTime.UtcNow.AddHours( -hours ) )
                    {
                        tooOld = true;
                        candleCollection[ product ].TryDequeue( out candle );
                    }
                    else
                    {
                        tooOld = false;
                    }
                } while( tooOld );
            }
            catch( Exception e)
            {
                Console.WriteLine( e.Message );
                Console.WriteLine( e.StackTrace );
            }
            
        }

        protected virtual void OnUpdatedProductCandles( UpdatedProductCandlesEventArgs e )
        {
            EventHandler<UpdatedProductCandlesEventArgs> handler = UpdatedProductCandles;
            if( handler != null )
            {
                handler( this, e );
            }
        }

        protected virtual void OnConstructedLongCandle( ConstructedLongCandleEventArgs e )
        {
            EventHandler<ConstructedLongCandleEventArgs> handler = ConstructedLongCandle;
            if( handler != null )
            {
                handler( this, e );
            }
        }

        private Timer aTimer;
        private DataFetcher fetcher;
        private ConcurrentDictionary<string, ConcurrentQueue<Candle>> shortProductCandles;
        private ConcurrentDictionary<string, ConcurrentQueue<Candle>> longProductCandles;
        private Dictionary<string, bool> fetchedHistoric;
        private Dictionary<string, int> shortCandlesAdded;
        private Dictionary<string, int> longCandlesAdded;
        private SynchronizedConsoleWriter writer;

        public event EventHandler<UpdatedProductCandlesEventArgs> UpdatedProductCandles;
        public event EventHandler<ConstructedLongCandleEventArgs> ConstructedLongCandle;
    }

    public class UpdatedProductCandlesEventArgs : EventArgs
    {
        public bool updatedLong { get; set; }
    }
    public class ConstructedLongCandleEventArgs : EventArgs
    {
        public string ProductId { get; set; }
        public Candle LongCandle { get; set; }
    }
}
