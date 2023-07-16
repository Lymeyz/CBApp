using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Timers;
using System.Threading;

namespace CBApp1
{
    //Analyses data presented by DataHandler and raises events based on conditions to be defined
    //Events are handled by OrderDirector

    public class DataAnalyser
    {
        private readonly object analyserShortCandlesRoot = new object();
        private readonly object analyserLongCandlesRoot = new object();
        private readonly object analyserCurrentLongCandlesRoot = new object();
        private readonly object analyserCurrentShortCandlesRoot = new object();
        public DataAnalyser(ref DataHandler dataHandler, ref System.Timers.Timer aTimer, ref SynchronizedConsoleWriter writer)
        {
            this.writer = writer;
            this.dataHandler = dataHandler;
            this.dataHandler.UpdatedProductCandles += UpdatedProductCandles;
            dataHandler.Fetcher.ShortCandleUpdate += ShortCandleUpdateEvent;
            dataHandler.Fetcher.LongCandleUpdate += LongCandleUpdateEvent;
            aTimer.Elapsed += this.OnTimedEvent;

            shortProductCandles = new Dictionary<string, Queue<Candle>>();
            longProductCandles = new Dictionary<string, Queue<Candle>>();
            shortEmas = new ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentStack<Ema>>>();
            longEmas = new ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentStack<Ema>>>();
            shortEmaSlopes = new ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentStack<Ema>>>();
            longEmaSlopes = new ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentStack<Ema>>>();
            shortSlopes = new ConcurrentDictionary<string, ConcurrentStack<SlopeCandle>>();
            shortMinMax24 = new ConcurrentDictionary<string, HighLow>();
            shortMinMax = new ConcurrentDictionary<string, HighLow>();
            longMinMax = new ConcurrentDictionary<string, HighLow>();
            currentShortCandles = new ConcurrentDictionary<string, Candle>();
            currentLongCandles = new ConcurrentDictionary<string, Candle>();
            calculatedShortEmas = new ConcurrentDictionary<string, bool>();
            calculatedLongEmas = new ConcurrentDictionary<string, bool>();
            prelOs = new ConcurrentDictionary<string, PreOrder>();
            results = new ConcurrentDictionary<string, LongAnalysisResult>();

            productInfos = new ConcurrentDictionary<string, ProductInfo>(dataHandler.Fetcher.ProductInfos);

            foreach (var product in dataHandler.ShortProductCandles.Keys)
            {
                shortSlopes[ product ] = null;
                shortEmas[product] = null;
                longEmas[ product ] = null;
                shortEmaSlopes[product] = null;
                longEmaSlopes[ product ] = null;
                currentShortCandles[ product ] = new Candle( DateTime.MinValue, 0, 0, 0, 0, 0 );
                calculatedShortEmas[product] = false;
                calculatedLongEmas[ product ] = false;
                prelOs[ product ] = null;
                results[ product ] = null;
            }

            analysisRunning = false;
        }

        //periods in ascending order
        private void CalculateAllEmas( Dictionary<string, Queue<Candle>> productCandles,
                                      ref ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentStack<Ema>>> currentAnalyserEmas,
                                      ref ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentStack<Ema>>> currentAnalyserEmaSlopes,
                                      ref ConcurrentDictionary<string, bool> calculatedEmas,
                                      params int[] periods )
        {
            try
            {
                foreach (string product in productCandles.Keys)
                {
                    // Fetch new candles, oldest is first
                    List<Candle> candleList = new List<Candle>( productCandles[ product] );
                    Dictionary<int, LimitedDateTimeList<Ema>> currentEmas;
                    Dictionary<int, LimitedDateTimeList<Ema>> currEmaSlopes;

                    if ( currentAnalyserEmas[ product ] == null)
                    {
                        currentAnalyserEmas[ product ] = new ConcurrentDictionary<int, ConcurrentStack<Ema>>();
                        currentAnalyserEmaSlopes[ product] = new ConcurrentDictionary<int, ConcurrentStack<Ema>>();
                        currentEmas = null;
                        currEmaSlopes = null;
                        CalculateProductEmas(product, ref candleList, ref currentEmas, ref currEmaSlopes, periods);
                        calculatedEmas[ product ] = true;
                    }
                    else
                    {
                        currentEmas = new Dictionary<int, LimitedDateTimeList<Ema>>();
                        currEmaSlopes = new Dictionary<int, LimitedDateTimeList<Ema>>();
                        foreach (int period in periods)
                        {
                            currentEmas[period] = new LimitedDateTimeList<Ema>( currentAnalyserEmas[ product][period], 300, true);
                            currEmaSlopes[period] = new LimitedDateTimeList<Ema>( currentAnalyserEmaSlopes[ product][period], 300, true);
                        }
                        CalculateProductEmas(product, ref candleList, ref currentEmas, ref currEmaSlopes, periods);
                    }

                    foreach (int period in periods)
                    {
                        currentAnalyserEmas[ product][period] = new ConcurrentStack<Ema>();
                        currentAnalyserEmaSlopes[ product][period] = new ConcurrentStack<Ema>();
                        int count = currentEmas[period].Count;
                        for (int i = 0; i < count; i++)
                        {
                            currentAnalyserEmas[ product ][ period ].Push( currentEmas[ period ].GetRemoveOldest() );
                        }

                        count = currEmaSlopes[period].Count;
                        for (int i = 0; i < count; i++)
                        {
                            currentAnalyserEmaSlopes[ product ][ period ].Push( currEmaSlopes[ period ].GetRemoveOldest() );
                        }
                    }

                    calculatedEmas[ product] = true;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
        }

        //Do LDT tests - done
        private void CalculateProductEmas( string product,
                                          ref List<Candle> candleList,
                                          ref Dictionary<int, LimitedDateTimeList<Ema>> currEmas,
                                          ref Dictionary<int, LimitedDateTimeList<Ema>> currEmaSlopes,
                                          int[] periods )
        {
            try
            {
                Dictionary<int, Ema> latestEmas = null;
                Dictionary<int, double> smas = new Dictionary<int, double>();
                Dictionary<int, double> ks = new Dictionary<int, double>();

                Ema currEma = null;
                Ema currSlope = null;
                double emaPrice;
                int count = candleList.Count;
                double SMA;

                // Calculate Ks
                foreach (int period in periods)
                {
                    ks[period] = 2.0 / (period + 1.0);
                }

                // check if there are previously calculated emas
                if (currEmas == null)
                {
                    // no old emas, initialize currEmas
                    currEmas = new Dictionary<int, LimitedDateTimeList<Ema>>();
                    // initalize currEmaSlopes
                    currEmaSlopes = new Dictionary<int, LimitedDateTimeList<Ema>>();

                    foreach (int period in periods)
                    {
                        currEmas[period] = new LimitedDateTimeList<Ema>(300);
                        currEmaSlopes[period] = new LimitedDateTimeList<Ema>(300);
                    }
                }
                else
                {
                    // there are old emas, find latest emas
                    latestEmas = new Dictionary<int, Ema>();
                    foreach (int period in periods)
                    {
                        if (currEmas[period].Count != 0)
                        {
                            latestEmas[period] = currEmas[period].Newest;
                        }
                    }
                }

                // if no old emas, calculate SMA of each first length
                // then proceed with ema-calculation
                if (latestEmas == null)
                {
                    // SMAs-calculation
                    foreach (int period in periods)
                    {
                        SMA = candleList[0].Avg;

                        for (int i = 1; i < period; i++)
                        {
                            SMA += candleList[i].Avg;
                        }

                        SMA = SMA / period;

                        smas[period] = SMA;
                    }

                    // EMAs-calculation seeded by SMAs
                    for (int i = periods.Min(); i < count; i++)
                    {
                        foreach (int period in periods)
                        {
                            if (i == period)
                            {
                                emaPrice = (candleList[i].Avg * ks[period]) + (smas[period] * (1 - ks[period]));
                                currEma = new Ema(period,
                                emaPrice,
                                candleList[i].Time);
                                currEmas[period].AddValue(currEma);
                            }
                            else if (i == count-1)
                            {
                                emaPrice = (candleList[i].Avg * ks[period]) + (currEmas[period].Newest.Price * (1 - ks[period]));
                                currEma = new Ema(period,
                                                    emaPrice,
                                                    candleList[i].Time);
                                currSlope = new Ema(period,
                                                    emaPrice - currEmas[period].Newest.Price,
                                                    currEma.Time);
                                currEmas[period].AddValue(currEma);
                                currEmaSlopes[period].AddValue(currSlope);
                            }
                            else if (i > period)
                            {
                                emaPrice = (candleList[i].Avg * ks[period]) + (currEmas[period].Newest.Price * (1 - ks[period]));
                                currEma = new Ema(period,
                                                    emaPrice,
                                                    candleList[i].Time);
                                currSlope = new Ema(period,
                                                    emaPrice - currEmas[period].Newest.Price,
                                                    currEma.Time);
                                currEmas[period].AddValue(currEma);
                                currEmaSlopes[period].AddValue(currSlope);
                            }
                        }
                    }
                }
                else
                {
                    // Find index of candle used in latest ema
                    Dictionary<int, int> indices = new Dictionary<int, int>();
                    for (int i = candleList.Count - 1; i >= 0; i--)
                    {
                        foreach (int period in latestEmas.Keys)
                        {
                            if (latestEmas[period] != null)
                            {
                                if (Math.Abs((candleList[i].Time - latestEmas[period].Time).TotalSeconds) <= 10)
                                {
                                    indices[period] = i + 1;
                                }
                            }
                        }
                    }

                    // EMAs calculation based on last Emas
                    foreach (int period in indices.Keys)
                    {
                        for (int i = indices[period]; i < candleList.Count; i++)
                        {
                            double emaPricePart1 = (candleList[i].Avg * ks[period]);
                            double emaPricePart2 = (currEmas[period].Newest.Price * (1 - ks[period]));
                            emaPrice = emaPricePart1 + emaPricePart2;
                            //emaPrice = (candleList[i].Avg * ks[period]) + (currEmas[i].Newest.Price * (1 - ks[period]));
                            currEma = new Ema(period,
                                emaPrice,
                                candleList[i].Time);
                            currSlope = new Ema(period,
                                                emaPrice - currEmas[period].Newest.Price,
                                                currEma.Time);

                            if( currEmas[period].Newest!=null )
                            {
                                if( currEma.Time.Hour == currEmas[ period ].Newest.Time.Hour
                                    && currEma.Time.Minute == currEmas[ period ].Newest.Time.Minute
                                    && currEma.Time.Day == currEmas[ period ].Newest.Time.Day )
                                {
                                    currEmas[ period ].GetRemoveNewest();
                                    currEmas[ period ].AddValue( currEma );
                                }
                                else
                                {
                                    currEmas[ period ].AddValue( currEma );
                                }
                            }
                            else
                            {
                                currEmas[ period ].AddValue( currEma );
                            }

                            if( currEmaSlopes[ period ].Newest != null )
                            {
                                if( currSlope.Time.Hour == currEmaSlopes[ period ].Newest.Time.Hour
                                    && currSlope.Time.Minute == currEmaSlopes[ period ].Newest.Time.Minute 
                                    && currSlope.Time.Day == currEmaSlopes[ period ].Newest.Time.Day )
                                {
                                    currEmaSlopes[ period ].GetRemoveNewest();
                                    currEmaSlopes[ period ].AddValue( currSlope );
                                }
                                else
                                {
                                    currEmaSlopes[ period ].AddValue( currSlope );
                                }
                            }
                            else
                            {
                                currEmaSlopes[ period ].AddValue( currSlope );
                            }
                            //currEmas[period].AddValue(currEma);
                            //currEmaSlopes[period].AddValue(currSlope);
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

        private void CalculateAnEma(string product, int length, double k, ref List<Candle> candleList,
                                    ref Stack<Ema> emaStack)
        {
            try
            {
                Ema currEma = null;
                Ema prevEma = null;
                double emaPrice;
                int count = candleList.Count;

                //calculate SMA of first length candles
                double SMA = candleList[0].Avg;
                for (int i = 0; i < length; i++)
                {
                    SMA += candleList[i].Avg;
                }
                SMA = SMA / length;

                for (int i = length; i < count; i++)
                {
                    //start with previously calculated SMA-value
                    //calculate EMAs
                    if (i == length)
                    {
                        emaPrice = (candleList[i].Avg * k) + (SMA * (1 - k));
                        currEma = new Ema(length,
                            emaPrice,
                            candleList[i].Time);
                        emaStack.Push(currEma);
                    }
                    else
                    {
                        emaPrice = (candleList[i].Avg * k) + (prevEma.Price * (1 - k));
                        currEma = new Ema(length,
                            emaPrice,
                            candleList[i].Time);
                        emaStack.Push(currEma);
                    }

                    prevEma = currEma;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
        }

        //build or update slope-collection
        // FINISH THIS
        private void SlopesAndMinMax(string product, Queue<Candle> candles)
        {
            try
            {
                //Find all tangentlines between highest and lowest price 
                //of each individual candle...
                LimitedDateTimeList<SlopeCandle> newSlopes;
                Stack<SlopeCandle> newSlopeStack;
                Candle candle1;
                Candle candle2;
                double max;
                double min;
                double minVolume;
                double maxVolume;
                int count;

                //get current slope stack
                newSlopes = new LimitedDateTimeList<SlopeCandle>(shortSlopes[product], 300);

                //oldest candles
                candle1 = candles.Dequeue();
                candle2 = candles.Dequeue();
                max = Math.Max(candle1.High, candle2.High);
                min = Math.Min(candle1.Low, candle2.Low);
                minVolume = Math.Min(candle1.Volume, candle2.Volume);
                maxVolume = Math.Max(candle1.Volume, candle2.Volume);
                count = candles.Count;

                newSlopeStack = new Stack<SlopeCandle>();

                //build new slope collection
                //OBS ACCOUNT FOR ALREADY EXISTING SLOPES.
                for (int i = 0; i < count + 1; i++)
                {

                    if (Math.Max(candle1.High, candle2.High) > max)
                    {
                        max = Math.Max(candle1.High, candle2.High);
                    }
                    if (Math.Min(candle1.Low, candle2.Low) < min)
                    {
                        min = Math.Min(candle1.Low, candle2.Low);
                    }
                    if (Math.Max(candle1.Volume, candle2.Volume) > maxVolume)
                    {
                        maxVolume = Math.Max(candle1.Volume, candle2.Volume);
                    }
                    if (Math.Min(candle1.Volume, candle2.Volume) < minVolume)
                    {
                        minVolume = Math.Min(candle1.Volume, candle2.Volume);
                    }

                    //candles hanteras från nyast till äldst
                    if (newSlopes.Count != 0)
                    {
                        if (newSlopes.Newest.Time < candle1.Time)
                        {
                            newSlopes.AddValue(new SlopeCandle(candle2.High - candle1.High,
                                                            candle2.Low - candle1.Low,
                                                            candle2.Volume - candle1.Volume,
                                                                candle1.Time));
                        }
                    }
                    else
                    {
                        newSlopes.AddValue(new SlopeCandle(candle2.High - candle1.High,
                                                        candle2.Low - candle1.Low,
                                                        candle2.Volume - candle1.Volume,
                                                            candle1.Time));
                    }


                    if (candles.Count != 0)
                    {
                        candle1 = candle2;
                        candle2 = candles.Dequeue();
                    }

                }

                shortMinMax24[product] = new HighLow(max, min, maxVolume, minVolume);
                shortSlopes[product] = new ConcurrentStack<SlopeCandle>();
                while (newSlopes.Count != 0)
                {
                    shortSlopes[product].Push(newSlopes.GetRemoveOldest());
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
        }

        private async void UpdatedProductCandles(object sender, UpdatedProductCandlesEventArgs e )
        {
            try
            {
                await Task.Run( () =>
                {
                    ConcurrentQueue<Candle> currentQueue;
                    //Copy candles 
                    lock( analyserShortCandlesRoot )
                    {
                        shortProductCandles = new Dictionary<string, Queue<Candle>>();
                        foreach( var product in dataHandler.ShortProductCandles.Keys )
                        {
                            currentQueue = dataHandler.ShortProductCandles[ product ];
                            if( currentQueue.Count > 150 )
                            {
                                shortProductCandles[ product ] = new Queue<Candle>( currentQueue );
                            }
                            results[ product ] = null;
                            prelOs[ product ] = null;
                        }
                    }

                    //Console.WriteLine($"Calculating emas at {DateTime.UtcNow} UTC");

                    CalculateAllEmas( shortProductCandles,
                                     ref shortEmas,
                                     ref shortEmaSlopes,
                                     ref calculatedShortEmas,
                                     6,
                                     12,
                                     26 );

                    FindMinMaxes( shortProductCandles,
                                 ref shortMinMax,
                                 12 );

                    if( e.updatedLong )
                    {
                        lock( analyserLongCandlesRoot )
                        {
                            longProductCandles = new Dictionary<string, Queue<Candle>>();
                            foreach( var product in dataHandler.LongProductCandles.Keys )
                            {
                                currentQueue = dataHandler.LongProductCandles[ product ];
                                if( currentQueue.Count > 70 )
                                {
                                    longProductCandles[ product ] = new Queue<Candle>( currentQueue );
                                }
                            }
                        }
                        // Calc long emas...

                        CalculateAllEmas( longProductCandles,
                                         ref longEmas,
                                         ref longEmaSlopes,
                                         ref calculatedLongEmas,
                                         6,
                                         12 );

                        //Queue<Ema> currentShortEmaSQ;
                        //Queue<Ema> currentLongEmaSQ;
                        //Queue<Ema> currentLongEmaQ;
                        //Queue<Ema> currentShortEmaQ;
                        //foreach( var prod in longEmaSlopes.Keys )
                        //{
                        //    if( longEmaSlopes[prod] != null && longEmas[prod] != null )
                        //    {
                        //        currentShortEmaSQ = new Queue<Ema>( longEmaSlopes[ prod ][ 15 ] );
                        //        currentLongEmaSQ = new Queue<Ema>( longEmaSlopes[ prod ][ 30 ] );
                        //        currentLongEmaQ = new Queue<Ema>( longEmas[ prod ][ 30 ] );
                        //        currentShortEmaQ = new Queue<Ema>( longEmas[ prod ][ 15 ] );
                        //        Ema sS;
                        //        Ema lS;
                        //        Ema s;
                        //        Ema l;

                        //        writer.Write( $"Last 5 {prod} long ema-slopes: " );

                        //        for( int i = 0; i < 7; i++ )
                        //        {
                        //            sS = currentShortEmaSQ.Dequeue();
                        //            lS = currentLongEmaSQ.Dequeue();
                        //            l = currentLongEmaQ.Dequeue();
                        //            s = currentShortEmaQ.Dequeue();

                        //            if( i < 6 )
                        //            {
                        //                writer.Write( $"ema: {s.Time:HH-mm-ss} - {s.Price}, {l.Time:HH-mm-ss} - {l.Price}" );
                        //                writer.Write( $"slope: {sS.Time:HH-mm-ss} - {sS.Price}, {lS.Time:HH-mm-ss} - {lS.Price}" );
                        //            }
                        //            else
                        //            {
                        //                writer.Write( $"Short ema: {s.Time:HH-mm-ss} - {s.Price}, Long ema: {l.Time:HH-mm-ss} - {l.Price}" );
                        //            }
                        //        }
                        //    }
                        //}
                        
                        // FindMinMaxes()
                        
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }

        private void FindMinMaxes( Dictionary<string, Queue<Candle>> currentAnalyserCandles,
                                   ref ConcurrentDictionary<string, HighLow> minMaxes,
                                  int hours )
        {
            LimitedDateTimeList<Candle> currCandles;
            //LinkedList<double> topList;
            //LinkedList<double> minList;

            int count;
            Candle candle1;
            Candle candle2;
            double min;
            double max;
            double volMin;
            double volMax;
            foreach (string product in currentAnalyserCandles.Keys)
            {
                //topList = new LinkedList<double>();
                //minList = new LinkedList<double>();

                currCandles = new LimitedDateTimeList<Candle>( currentAnalyserCandles[ product], 300);
                candle1 = currCandles.GetRemoveNewest();
                candle2 = currCandles.GetRemoveNewest();

                min = Math.Min(candle1.Avg, candle2.Avg);
                max = Math.Max(candle1.Avg, candle2.Avg);
                volMin = Math.Min(candle1.Volume, candle2.Volume);
                volMax = Math.Max(candle1.Volume, candle2.Volume);

                count = currCandles.Count;
                for (int i = 0; i < count; i++)
                {
                    if (max < Math.Max(candle1.Avg, candle2.Avg))
                    {
                        max = Math.Max(candle1.Avg, candle2.Avg);
                    }

                    if (min > Math.Min(candle1.Avg, candle2.Avg))
                    {
                        min = Math.Min(candle1.Avg, candle2.Avg);
                    }

                    if (volMax < Math.Max(candle1.Volume, candle2.Volume))
                    {
                        volMax = Math.Max(candle1.Volume, candle2.Volume);
                    }

                    if (volMin > Math.Min(candle1.Volume, candle2.Volume))
                    {
                        volMin = Math.Min(candle1.Volume, candle2.Volume);
                    }

                    if (candle2.Time < DateTime.UtcNow.AddHours(-hours))
                    {
                        break;
                    }

                    candle1 = candle2;
                    candle2 = currCandles.GetRemoveNewest();
                }

                minMaxes[ product] = new HighLow(max, min, volMax, volMin);
            }
        }

        private void PrintEmasAndEmaSlopes(string product)
        {
            LimitedDateTimeList<Ema> currEmas;
            LimitedDateTimeList<Ema> currEmaSlopes;
            Ema currEma;
            Ema currSlope;
            //for each period, print 10 emas and 10 emaslopes next to eachother
            Console.WriteLine("");
            Console.WriteLine($"emas and emaslopes for {product}");
            foreach (int period in shortEmas[product].Keys)
            {
                currEmas = new LimitedDateTimeList<Ema>(shortEmas[product][period], 300);
                currEmaSlopes = new LimitedDateTimeList<Ema>(shortEmaSlopes[product][period], 300);
                
                Console.WriteLine($"ema{period}  -  ema slope{period}");
                for (int i = 0; i < 10; i++)
                {
                    currEma = currEmas.GetRemoveNewest();
                    currSlope = currEmaSlopes.GetRemoveNewest();
                    Console.WriteLine($"{currEma.Price} {currEma.Time} - {currSlope.Price} {currSlope.Time}");
                }
                Console.WriteLine("");
            }
        }

        private void CalculateAllSlopes()
        {
            try
            {
                foreach (var product in shortProductCandles.Keys)
                {
                    CalculateProductSlopes(product);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
                Console.WriteLine(e.Message);
            }
        }

        private void CalculateProductSlopes(string product)
        {
            try
            {
                LimitedDateTimeList<SlopeCandle> currSlopes;
                Queue<Candle> currCandles = new Queue<Candle>(shortProductCandles[product]);
                Candle candle1 = currCandles.Dequeue();
                Candle candle2 = currCandles.Dequeue();
                double max = Math.Max(candle1.High, candle2.High);
                double min = Math.Min(candle1.Low, candle2.Low);
                double minVolume = Math.Max(candle1.Volume, candle2.Volume);
                double maxVolume = Math.Min(candle1.Volume, candle2.Volume);
                int count = currCandles.Count;

                if (shortSlopes[product] == null)
                {
                    currSlopes = new LimitedDateTimeList<SlopeCandle>(true, 300);
                }
                else
                {
                    currSlopes = new LimitedDateTimeList<SlopeCandle>(shortSlopes[product], 300, true);
                }

                //calculate slopes
                
                for (int i = 0; i < count + 1; i++)
                {
                    if (Math.Max(candle1.High, candle2.High) > max)
                    {
                        max = Math.Max(candle1.High, candle2.High);
                    }
                    if (Math.Min(candle1.Low, candle2.Low) < min)
                    {
                        min = Math.Min(candle1.Low, candle2.Low);
                    }
                    if (Math.Max(candle1.Volume, candle2.Volume) > maxVolume)
                    {
                        maxVolume = Math.Max(candle1.Volume, candle2.Volume);
                    }
                    if (Math.Min(candle1.Volume, candle2.Volume) < minVolume)
                    {
                        minVolume = Math.Min(candle1.Volume, candle2.Volume);
                    }

                    if (currSlopes.Newest!=null)
                    {
                        if (candle2.Time>currSlopes.Newest.Time)
                        {
                            currSlopes.AddValue(new SlopeCandle(candle2.High -candle1.High,
                                                            candle2.Low - candle1.Low,
                                                            candle2.Volume - candle1.Volume,
                                                                candle2.Time));
                        }
                    }
                    else
                    {
                        currSlopes.AddValue(new SlopeCandle(candle2.High -candle1.High,
                                                            candle2.Low - candle1.Low,
                                                            candle2.Volume - candle1.Volume,
                                                                candle2.Time));
                    }

                    if (currCandles.Count != 0)
                    {
                        candle1 = candle2;
                        candle2 = currCandles.Dequeue();
                    }
                }

                shortMinMax24[product] = new HighLow(max, min, maxVolume, minVolume);
                shortSlopes[product] = new ConcurrentStack<SlopeCandle>();
                while(currSlopes.Count!=0)
                {
                    shortSlopes[product].Push(currSlopes.GetRemoveOldest());
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
                Console.WriteLine(e.Message);
            }
        }
        private async void OnTimedEvent(Object source, ElapsedEventArgs e)
        {
            try
            {
                if (shortProductCandles.Values.Count != 0)
                {
                    if (!((e.SignalTime.Minute % 5 == 0) && (e.SignalTime.Second > 49 || e.SignalTime.Second < 10)))
                    {
                        if (!analysisRunning)
                        {
                            analysisRunning = true;
                            await Task.Run(() =>
                            {
                                AnalyseData();
                            });
                            analysisRunning = false;
                        }
                        //else
                        //{

                        //}
                    }
                    //else
                    //{
                    //    Console.WriteLine("Close to new candle");
                    //}
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine(ex.Message);
            }
            
        }


        private void AnalyseData()
        {
            try
            {
                bool hasShortEmas;
                bool hasLongEmas;
                double k;
                double emaPrice;
                Ema newestEma;
                Dictionary<int, Ema> currentEmas;
                Dictionary<int, Ema> currentLongEmas;
                Dictionary<int, Ema> currentEmaSlopes;
                Dictionary<int, Ema> currentLongEmaSlopes;
                Dictionary<string, Candle> currentLongCandlesCopy;
                List<string> longAnalysedProducts = new List<string>();
                int[] emaPeriods;
                
                Dictionary<string, AnalysisParameters> productShortAnalysisParameters = new Dictionary<string, AnalysisParameters>();
                // analyse long-time candles
                // 

                lock( analyserCurrentLongCandlesRoot )
                {
                    currentLongCandlesCopy = new Dictionary<string, Candle>( currentLongCandles );
                }

                foreach( string product in longProductCandles.Keys )
                {
                    hasLongEmas = true;
                    emaPeriods = new int[] { 6, 12 };

                    if( longProductCandles.ContainsKey( product ) && currentLongCandlesCopy.ContainsKey( product ) )
                    {
                        if( longProductCandles[ product ] != null && currentLongCandlesCopy[ product ] != null )
                        {
                            foreach( int period in emaPeriods )
                            {
                                if( longEmas[ product ][ period ] != null )
                                {
                                    if( longEmas[ product ][ period ].Count == 0 )
                                    {
                                        hasLongEmas = false;
                                    }
                                }
                                else
                                {
                                    hasLongEmas = false;
                                }
                            }

                            if( hasLongEmas )
                            {

                                currentLongEmas = new Dictionary<int, Ema>();
                                currentLongEmaSlopes = new Dictionary<int, Ema>();

                                foreach( int period in emaPeriods )
                                {
                                    k = 2.0 / (period + 1);
                                    longEmas[ product ][ period ].TryPeek( out newestEma );
                                    emaPrice = (currentLongCandlesCopy[ product ].Close * k) + (newestEma.Price * (1 - k));
                                    currentLongEmas[ period ] = new Ema( period,
                                                                         emaPrice,
                                                                         currentLongCandlesCopy[ product ].Time );
                                    currentLongEmaSlopes[ period ] = new Ema( period,
                                                                        emaPrice - newestEma.Price,
                                                                        currentLongEmas[ period ].Time );
                                }

                                // do long analysis

                                AnalyseProductLongTerm( product,
                                                        0.005,
                                                        0.05,
                                                        0.008,
                                                        0.18,
                                                        0.95,
                                                        emaPeriods.Min<int>(),
                                                        emaPeriods.Max<int>(),
                                                        ref longEmas,
                                                        ref currentLongEmas,
                                                        ref currentLongEmaSlopes,
                                                        ref currentLongCandlesCopy);
                                // SellOff event --> director



                                // do short analysis

                                // Analyse emas up to {time} minutes back in time.
                                // Look for shorter ema under longer ema with
                                // increasing distance, flag this. A watching
                                // function will wait for the turn, assess viability
                                // in relation to pricing situation and in case of
                                // positive assessment raise an event for OrderDirector

                                // if currentCandles[product]!=null
                                // if emas[product][period] != null
                                // calculate currentEmas[product]
                                // if(currentTrend[product]==null
                                // begin going back in time to find start 
                                // of latest trend
                                // if(currentTrend[product]!=null

                                if( results.ContainsKey( product ) )
                                {
                                    if( results[ product ] != null )
                                    {
                                        hasShortEmas = true;
                                        emaPeriods = new int[] { 6, 26 };

                                        if( shortProductCandles.ContainsKey( product ) && currentShortCandles.ContainsKey( product ) )
                                        {
                                            if( results[ product ].SellOff )
                                            {
                                                PreOrder pre = new PreOrder( product, DateTime.UtcNow, false );
                                                pre.Price = currentShortCandles[ product ].Close;
                                                pre.SellOff = true;
                                                PreOrderReadyEventArgs args = new PreOrderReadyEventArgs();
                                                args.PreliminaryOrder = new PreOrder( pre );
                                                OnPreOrderReady( args );
                                            }
                                            else if( results[ product ].BuyOk || results[ product ].SellOk )
                                            {
                                                if( shortProductCandles[ product ] != null && currentShortCandles[ product ] != null )
                                                {
                                                    foreach( int period in emaPeriods )
                                                    {
                                                        if( shortEmas[ product ][ period ] != null )
                                                        {
                                                            if( shortEmas[ product ][ period ].Count == 0 )
                                                            {
                                                                hasShortEmas = false;
                                                            }
                                                        }
                                                        else
                                                        {
                                                            hasShortEmas = false;
                                                        }
                                                    }

                                                    if( hasShortEmas )
                                                    {
                                                        currentEmas = new Dictionary<int, Ema>();
                                                        currentEmaSlopes = new Dictionary<int, Ema>();
                                                        foreach( int period in emaPeriods )
                                                        {
                                                            k = 2.0 / (period + 1);
                                                            shortEmas[ product ][ period ].TryPeek( out newestEma );
                                                            emaPrice = (currentShortCandles[ product ].Avg * k) + (newestEma.Price * (1 - k));
                                                            currentEmas[ period ] = new Ema( period,
                                                                emaPrice,
                                                                currentShortCandles[ product ].Time );
                                                            currentEmaSlopes[ period ] = new Ema( period,
                                                                                            emaPrice - newestEma.Price,
                                                                                            currentEmas[ period ].Time );
                                                        }
                                                        // 0.035, 0.14, 0.40, 0.8
                                                        //tStartP 0.8 --> 0.3
                                                        AnalyseProduct( product,
                                                                       0.010,
                                                                       0.003,
                                                                       0.70,
                                                                       0.97,
                                                                       0.01,
                                                                       emaPeriods.Max<int>(),
                                                                       emaPeriods.Min<int>(),
                                                                       currentEmas,
                                                                       currentEmaSlopes );

                                                    }
                                                }
                                            }
                                        }
                                    }
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



        private void AnalyseProductLongTerm( string product,
                                            double bTurnP,
                                            double tStartP,
                                            double sOffP,
                                            double sDiffP,
                                            double bTooLateP,
                                            int shortPeriod,
                                            int longPeriod,
                                            ref ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentStack<Ema>>> longEmas,
                                            ref Dictionary<int, Ema> currEmas,
                                            ref Dictionary<int, Ema> currEmaSlopes,
                                            ref Dictionary<string, Candle> currentLongCandlesCopy )
        {
            try
            {
                Dictionary<int, LimitedDateTimeList<Ema>> prevEmas = new Dictionary<int, LimitedDateTimeList<Ema>>();
                Dictionary<int, LimitedDateTimeList<Ema>> prevEmaSlopes = new Dictionary<int, LimitedDateTimeList<Ema>>();

                prevEmas[ shortPeriod ] = new LimitedDateTimeList<Ema>( longEmas[ product ][ shortPeriod ], 300 );
                prevEmas[ longPeriod ] = new LimitedDateTimeList<Ema>( longEmas[ product ][ longPeriod ], 300 );

                prevEmaSlopes[ shortPeriod ] = new LimitedDateTimeList<Ema>( longEmaSlopes[ product ][ shortPeriod ], 300 );
                prevEmaSlopes[ longPeriod ] = new LimitedDateTimeList<Ema>( longEmaSlopes[ product ][ longPeriod ], 300 );

                Ema newestShortEma = currEmas[ shortPeriod ];
                Ema newestLongEma = currEmas[ longPeriod ];
                Ema newestShortEmaSlope = currEmaSlopes[ shortPeriod ];
                Ema newestLongEmaSlope = currEmaSlopes[ longPeriod ];
                Ema currShortEma = currEmas[ shortPeriod ];
                Ema currLongEma = currEmas[ longPeriod ];
                Ema currShortEmaSlope = currEmaSlopes[ shortPeriod ];
                Ema currLongEmaSlope = currEmaSlopes[ longPeriod ];

                LongAnalysisResult result;
                double currDiff;

                // check for previous result
                if( results[ product ] == null )
                {
                    result = null;
                }
                else
                {
                    result = results[ product ];
                }

                if( result == null )
                {
                    // No previous result
                    // Determine current trend
                    for( int i = 0; i < longEmas[ product ][ longPeriod ].Count; i++ )
                    {
                        if( result == null )
                        {
                            currDiff = Math.Abs( currShortEma - currLongEma );
                            // Current result null, initialize and set to current ema-trend
                            if( currShortEma.Price < currLongEma.Price )
                            {
                                result = new LongAnalysisResult();
                                result.Trend = false;

                                result.PeakDiff = currDiff;
                                result.PeakTime = currShortEma.Time;
                            }
                            else if( currShortEma.Price > currLongEma.Price )
                            {
                                result = new LongAnalysisResult();
                                result.Trend = true;

                                result.PeakDiff = currDiff;
                                result.PeakTime = currShortEma.Time;
                            }

                            
                            

                            // Get next ema pair
                            currShortEma = prevEmas[ shortPeriod ].GetRemoveNewest();
                            currLongEma = prevEmas[ longPeriod ].GetRemoveNewest();

                            // Get next slope pair
                            currShortEmaSlope = prevEmaSlopes[ shortPeriod ].GetRemoveNewest();
                            currLongEmaSlope = prevEmaSlopes[ longPeriod ].GetRemoveNewest();

                        }
                        // Determine phase of trend
                        else
                        {
                            // Current difference in ema-pair
                            currDiff = Math.Abs( currShortEma - currLongEma );

                            // Short ema under long ema (not rising trend)
                            if( result.Trend == false)
                            {
                                // Find peak difference
                                if( result.PeakDiff < (currLongEma.Price - currShortEma.Price) )
                                {
                                    result.PeakDiff = currLongEma.Price - currShortEma.Price;
                                    result.PeakTime = currShortEma.Time;
                                }

                                // Find start of trend
                                if( currShortEma >= currLongEma )
                                {
                                    result.Time = currShortEma.Time;
                                    break;
                                }
                            }
                            // Short ema over long ema (rising trend)
                            else
                            {
                                // Find peak difference
                                if( result.PeakDiff < (currShortEma.Price - currLongEma.Price) )
                                {
                                    result.PeakDiff = currShortEma.Price - currLongEma.Price;
                                    result.PeakTime = currShortEma.Time;
                                }

                                // Find start of trend
                                if( currShortEma <= currLongEma )
                                {
                                    result.Time = currShortEma.Time;
                                    break;
                                }
                            }

                            // Check if current peak is most relevant
                            // PeakDiff always initialized to -1
                            if( result.PeakDiff != -1 )
                            {
                                // If current difference > tStart * peak difference, this is 
                                // taken to be start of trend
                                if( currDiff < tStartP * result.PeakDiff )
                                {
                                    if( result.Trend == false )
                                    {
                                        result.StartPrice = currShortEma.Price;
                                        result.Time = currShortEma.Time;
                                    }
                                    // Start price provided in od
                                    else
                                    {
                                        result.StartPrice = currLongEma.Price;
                                        result.Time = currLongEma.Time;
                                    }

                                    break;

                                }
                            }

                            currShortEma = prevEmas[ shortPeriod ].GetRemoveNewest();
                            currLongEma = prevEmas[ longPeriod ].GetRemoveNewest();
                        }
                    }
                }
                else
                {
                    // Check if trend still ongoing
                    if( result.Trend == false )
                    {
                        if( newestShortEma >= newestLongEma )
                        {
                            // Emas crossed
                            result = null;
                            results[ product ] = null;
                        }
                    }
                    else
                    {
                        if( newestShortEma <= newestLongEma )
                        {
                            // Emas crossed
                            result = null;
                            results[ product ] = null;
                        }
                    }
                }



                if( result != null )
                {
                    currDiff = Math.Abs(newestShortEma - newestLongEma);

                    // Update current result if at new peak
                    if( currDiff > result.PeakDiff )
                    {
                        result.PeakDiff = currDiff;
                        result.PeakTime = newestShortEma.Time;
                    }

                    bool sellingOff;

                    if( result.Trend == false)
                    {
                        // Too late-condition

                            // If difference decreased more than bTurnP + bTooLate %, wait for another peak
                        if( newestLongEmaSlope.Price < 0 )
                        {
                            if( Math.Abs( newestShortEmaSlope.Price ) > 0.0004 * newestShortEma.Price )
                            {
                                sellingOff = true;
                                result.SellOff = true;
                                result.Complete = true;

                                results[ product ] = result;
                            }
                        }
                        else
                        {
                            sellingOff = false;
                            result.SellOff = false;

                            if( result.PeakDiff - currDiff > (result.PeakDiff * (bTurnP + bTooLateP)) )
                            {

                            }
                            //else if( (newestShortEmaSlope.Price >= 0 && newestLongEmaSlope.Price >= 0)
                            //    && result.PeakDiff - currDiff > (result.PeakDiff * bTurnP) )
                            else if( (newestShortEmaSlope.Price >= 0 && newestLongEmaSlope.Price >= 0)
                                && result.PeakDiff - currDiff > (result.PeakDiff * bTurnP) )
                            {
                                // Suggested price 
                                result.Price = currentLongCandlesCopy[ product ].Avg;
                                //Console.WriteLine($"Buy {prel.ProductId} order at {prel.Price} ");
                                result.BuyOk = true;

                                result.Complete = true;

                                results[ product ] = result;

                                if( Math.Abs( currentShortCandles[ product ].Avg - currentLongCandlesCopy[ product ].Avg ) < 0.005 * currentLongCandlesCopy[ product ].Avg )
                                {
                                    
                                }

                                PreOrder pre = new PreOrder( product,
                                                             result.Time,
                                                             true );
                                pre.PeakTime = result.PeakTime;
                                pre.Price = currentShortCandles[ product ].Close;

                                PreliminaryComplete( pre, ref longProductCandles, ref currentLongCandles, 1.0075 );
                            }

                        }
                        
                        
                        //else
                        //{
                        //    // cancel any active buy order
                        //    foreach( var item in collection )
                        //    {

                        //    }
                        //}

                        //if( newestShortEmaSlope.Price >= 0 && (result.PeakDiff - currDiff > (result.PeakDiff * bTurnP)) )
                        //{
                        //    // Suggested price 
                        //    result.Price = currentLongCandlesCopy[ product ].Avg;
                        //    //Console.WriteLine($"Buy {prel.ProductId} order at {prel.Price} ");
                        //    result.BuyOk = true;

                        //    result.Complete = true;

                        //    results[ product ] = result;
                        //}
                        //else if( newestShortEmaSlope.Price >= 0 || newestLongEmaSlope.Price >= 0 )
                        //{
                        //    // Suggested price 
                        //    result.Price = currentLongCandlesCopy[ product ].Avg;
                        //    //Console.WriteLine($"Buy {prel.ProductId} order at {prel.Price} ");
                        //    result.BuyOk = true;

                        //    result.Complete = true;

                        //    results[ product ] = result;
                        //}
                        //else if( ( Math.Abs( newestShortEmaSlope.Price - newestLongEmaSlope.Price ) < (sDiffP * newestLongEmaSlope.Price  ) ) 
                        //            || newestShortEmaSlope.Price > newestLongEmaSlope.Price )
                        //{
                        //    // Suggested price 
                        //    result.Price = currentLongCandlesCopy[ product ].Avg;
                        //    //Console.WriteLine($"Buy {prel.ProductId} order at {prel.Price} ");
                        //    result.BuyOk = true;

                        //    result.Complete = true;

                        //    results[ product ] = result;
                        //}
                    }
                    else
                    {

                        // Too late-condition

                        //if( result.PeakDiff - currDiff > (result.PeakDiff * (0.003 + 0.4)) )
                        //{

                        //}
                        //If difference decreased by turnP % suggest order


                        // if short ema-slope is less than longer ema-slope
                        // or short ema-slope is within 10% of longer ema-slope
                        //
                        // abs(short-long) <-- abs difference between s l
                        // 0.1 * long <-- 10% of long
                        //

                        //if( newestShortEmaSlope.Price < 0 && newestLongEmaSlope.Price < 0 )

                        sellingOff = false;

                        if( newestLongEmaSlope.Price < 0 )
                        {
                            // 0.0018 --> 0.0012
                            if( Math.Abs(newestShortEmaSlope.Price) > 0.0012 * newestShortEma.Price )
                            {
                                sellingOff = true;
                                result.SellOff = true;
                                result.Complete = true;

                                results[ product ] = result;
                            }
                        }
                        else
                        {
                            bool sending = false;
                            result.SellOff = false;
                            sellingOff = false;

                            if( newestShortEmaSlope.Price >= newestLongEmaSlope.Price )
                            {
                                if( ((Math.Abs( newestShortEmaSlope.Price - newestLongEmaSlope.Price ) < (sDiffP * newestLongEmaSlope.Price)) ||
                                  (newestShortEmaSlope.Price < newestLongEmaSlope.Price)) )
                                {
                                    sending = true;
                                }
                            }
                            else
                            {
                                sending = true;
                            }

                            if( sending )
                            {
                                result.Price = currentLongCandlesCopy[ product ].Avg;

                                result.SellOk = true;

                                result.Complete = true;

                                results[ product ] = result;

                                PreOrder pre = new PreOrder( product,
                                                             result.Time,
                                                             false );
                                pre.PeakTime = result.PeakTime;
                                pre.Price = currentShortCandles[ product ].Avg;

                                PreliminaryComplete( pre, ref longProductCandles, ref currentLongCandles, 1.0075 );
                            }
                            // result.PeakDiff - currDiff > (result.PeakDiff * 0.006)

                        }


                        if( !sellingOff )
                        {

                            
                            
                        }
                    }

                    if( result.Complete )
                    {
                        //PreliminaryComplete( prel );
                        // prepare placing long order? 

                        if( result.BuyOk )
                        {
                            //writer.Write( $"{product} - buy ok {result.Time:HH-mm-ss}" );
                        }
                        else if( result.SellOk )
                        {
                            //writer.Write( $"{product} - sell ok {result.Time:HH-mm-ss}" );
                        } 
                    }
                    else
                    {
                        results[ product ] = result;
                    }
                }
            }
            catch( Exception e )
            {
                Console.WriteLine( e.StackTrace );
                Console.WriteLine( e.Message );
            }
        }
        private void AnalyseProduct( string product,
                                    double bTurnP,
                                    double sTurnP,
                                    double bTooLate,
                                    double sTooLate,
                                    double tStartP,
                                    int longPeriod,
                                    int shortPeriod,
                                    Dictionary<int, Ema> currEmas,
                                    Dictionary<int, Ema> currEmaSlopes )
        {
            try
            {
                Dictionary<int, LimitedDateTimeList<Ema>> prevEmas = new Dictionary<int, LimitedDateTimeList<Ema>>();
                Dictionary<int, LimitedDateTimeList<Ema>> prevEmaSlopes = new Dictionary<int, LimitedDateTimeList<Ema>>();

                prevEmas[ shortPeriod ] = new LimitedDateTimeList<Ema>( shortEmas[ product ][ shortPeriod ], 300 );
                prevEmas[ longPeriod ] = new LimitedDateTimeList<Ema>( shortEmas[ product ][ longPeriod ], 300 );

                prevEmaSlopes[ shortPeriod ] = new LimitedDateTimeList<Ema>( shortEmas[ product ][ shortPeriod ], 300 );
                prevEmaSlopes[ longPeriod ] = new LimitedDateTimeList<Ema>( shortEmas[ product ][ longPeriod ], 300 );

                Ema newestShortEma = currEmas[ shortPeriod ];
                Ema newestLongEma = currEmas[ longPeriod ];
                Ema currShortEma = currEmas[ shortPeriod ];
                Ema currLongEma = currEmas[ longPeriod ];
                Ema currShortSlope = currEmaSlopes[ shortPeriod ];
                Ema currLongSlope = currEmaSlopes[ longPeriod ];

                //if PrelOs[product]==null or irrelevant
                PreOrder pre;
                double currDiff;

                // check for previous preliminary order
                if( prelOs[ product ] == null )
                {
                    pre = null;
                }
                else
                {
                    pre = prelOs[ product ];
                }

                if( pre == null )
                {
                    for( int i = 0; i < shortEmas[ product ][ longPeriod ].Count; i++ )
                    {
                        // Determine current trend
                        if( pre == null )
                        {
                            if( currShortEma.Price < currLongEma.Price )
                            {
                                pre = new PreOrder( product, DateTime.MinValue, true );
                                pre.PeakDiff = currLongEma.Price - currShortEma.Price;
                                pre.PeakTime = currShortEma.Time;
                            }
                            else if( currShortEma.Price > currLongEma.Price )
                            {
                                pre = new PreOrder( product, DateTime.MinValue, false );
                                pre.PeakDiff = currShortEma.Price  - currLongEma.Price;
                                pre.PeakTime = currShortEma.Time;
                            }


                            currShortEma = prevEmas[ shortPeriod ].GetRemoveNewest();
                            currLongEma = prevEmas[ longPeriod ].GetRemoveNewest();
                        }
                        // Determine phase of trend
                        else
                        {

                            currDiff = Math.Abs( currShortEma - currLongEma );

                            if( pre.B == true )
                            {
                                // Find peak diff
                                if( pre.PeakDiff < (currLongEma.Price - currShortEma.Price) )
                                {
                                    pre.PeakDiff = currLongEma.Price - currShortEma.Price;
                                    pre.PeakTime = currShortEma.Time;
                                }

                                // Find start of b trend
                                if( currShortEma >= currLongEma )
                                {
                                    pre.Time = currShortEma.Time;
                                    break;
                                }
                            }
                            else
                            {
                                // Find peak diff
                                if( pre.PeakDiff < (currShortEma.Price - currLongEma.Price) )
                                {
                                    pre.PeakDiff = currShortEma.Price - currLongEma.Price;
                                    pre.PeakTime = currShortEma.Time;
                                }

                                // Find start of s trend
                                if( currShortEma <= currLongEma )
                                {
                                    pre.Time = currShortEma.Time;
                                    break;
                                }

                            }

                            // Check if current peak is most relevant
                            if( pre.PeakDiff != -1 )
                            {
                                // If current difference > tStart* peak difference, this is 
                                // taken to be start of trend, only relevant here for B=true
                                if( currDiff < tStartP * pre.PeakDiff )
                                {
                                    if( pre.B )
                                    {
                                        pre.Time = currShortEma.Time;
                                    }
                                    // Start price provided by order logs
                                    else
                                    {
                                        pre.Time = currLongEma.Time;
                                    }

                                    break;
                                }
                            }

                            currShortEma = prevEmas[ shortPeriod ].GetRemoveNewest();
                            currLongEma = prevEmas[ longPeriod ].GetRemoveNewest();
                        }


                    }
                }
                else
                {
                    // Check if trend still current
                    if( pre.B )
                    {
                        if( newestShortEma >= newestLongEma )
                        {
                            pre = null;
                            prelOs[ product ] = null;
                        }
                    }
                    else
                    {
                        if( newestShortEma <= newestLongEma )
                        {
                            pre = null;
                            prelOs[ product ] = null;
                        }
                    }
                }

                if( pre != null )
                {
                    // If previous PreOrder
                    // Update current difference
                    currDiff = Math.Abs( newestLongEma - newestShortEma );

                    if( currDiff > pre.PeakDiff )
                    {
                        pre.PeakDiff = currDiff;
                        pre.PeakTime = newestShortEma.Time;
                    }

                    // Check current emas

                    if( pre.B == true )
                    {

                        // If difference decreased more than turnP + tooLate %, wait for another peak
                        if( pre.PeakDiff - currDiff > (pre.PeakDiff * (bTurnP + bTooLate)) )
                        {

                        }
                        // If difference decreased by turnP % suggest order
                        else if( pre.PeakDiff - currDiff > (pre.PeakDiff * bTurnP) )
                        {
                            // long term analysis result
                            if( results[ product ].BuyOk )
                            {
                                // Suggested price 
                                pre.Price = currentShortCandles[ product ].Avg;
                                //Console.WriteLine($"Buy {prel.ProductId} order at {prel.Price} ");
                                //pre.Complete = true;

                            }
                        }
                    }
                    else
                    {

                        if( pre.PeakDiff - currDiff > (pre.PeakDiff * (sTurnP + sTooLate)) )
                        {

                        }
                        //If difference decreased by turnP % suggest order
                        else if( pre.PeakDiff - currDiff > (pre.PeakDiff * sTurnP) )
                        {
                            if( results[ product ].SellOk )
                            {
                                // Suggested price
                                pre.Price = currentShortCandles[ product ].Avg;
                                //Console.WriteLine($"Sell {prel.ProductId} order at {prel.Price} ");
                                pre.Complete = true;
                            }
                        }
                    }

                    if( pre.Complete )
                    {
                        PreliminaryComplete( pre, ref shortProductCandles, ref currentShortCandles, 1.0025 );
                    }
                    else
                    {
                        prelOs[ product ] = pre;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
        }

        private void PreliminaryComplete( PreOrder preOrder,
                                         ref Dictionary<string, Queue<Candle>> productCandles,
                                         ref ConcurrentDictionary<string, Candle> currentCandles,
                                         double startPeakBPercent )
        {
            try
            {
                LimitedDateTimeList<Candle> candles = new LimitedDateTimeList<Candle>( productCandles[ preOrder.ProductId ], 300 );
                Candle startCandle = null;
                Candle peakCandle = null;
                Candle currCandle = currentCandles[ preOrder.ProductId ];
                int count = candles.Count;

                for( int i = 0; i < candles.Count; i++ )
                {
                    if( startCandle == null )
                    {
                        if( (currCandle.Time.Day == preOrder.Time.Day) &&
                        (currCandle.Time.Hour == preOrder.Time.Hour) &&
                        ((currCandle.Time.Minute - preOrder.Time.Minute < 5) && (currCandle.Time.Minute - preOrder.Time.Minute >= 0)) )
                        {
                            startCandle = new Candle( currCandle );
                        }
                        else if( (currCandle.Time.Day - preOrder.Time.Day < 0) ||
                                 (currCandle.Time.Hour - preOrder.Time.Hour < 0) ||
                                 ((currCandle.Time.Hour == preOrder.Time.Hour) &&
                                   (currCandle.Time.Hour == preOrder.Time.Hour) &&
                                   (currCandle.Time.Minute - preOrder.Time.Minute < 0)) )
                        {
                            startCandle = new Candle( currCandle );
                        }
                    }

                    if( peakCandle == null )
                    {
                        if( (currCandle.Time.Day == preOrder.PeakTime.Hour) &&
                        (currCandle.Time.Hour == preOrder.PeakTime.Hour) &&
                        (currCandle.Time.Minute - preOrder.PeakTime.Minute < 5 && currCandle.Time.Minute - preOrder.PeakTime.Minute >= 0) )
                        {
                            peakCandle = new Candle( currCandle );
                            preOrder.peakPrice = peakCandle.Avg;
                        }
                        else if( (currCandle.Time.Day - preOrder.PeakTime.Day < 0) ||
                                 (currCandle.Time.Hour - preOrder.PeakTime.Hour < 0) ||
                                 ((currCandle.Time.Hour == preOrder.PeakTime.Hour) &&
                                   (currCandle.Time.Hour == preOrder.PeakTime.Hour) &&
                                   (currCandle.Time.Minute - preOrder.PeakTime.Minute < 0)) )
                        {
                            peakCandle = new Candle( currCandle );
                            preOrder.peakPrice = peakCandle.Avg;
                        }
                    }

                    if( candles.Newest != null  )
                    {
                        currCandle = candles.GetRemoveNewest();
                    }

                    if( startCandle != null && peakCandle != null )
                    {
                        break;
                    }
                }

                if( startCandle != null && peakCandle != null )
                {
                    if( preOrder.B )
                    {
                        preOrder.Price = Math.Round( preOrder.Price, productInfos[ preOrder.ProductId ].QuotePrecision );

                        if( startCandle.High > peakCandle.Avg )
                        {
                            
                        }

                        PreOrderReadyEventArgs args = new PreOrderReadyEventArgs();
                        args.PreliminaryOrder = new PreOrder( preOrder );
                        OnPreOrderReady( args );

                    }
                    else
                    {
                        preOrder.Price = Math.Round( preOrder.Price, productInfos[ preOrder.ProductId ].QuotePrecision );

                        PreOrderReadyEventArgs args = new PreOrderReadyEventArgs();
                        args.PreliminaryOrder = new PreOrder( preOrder );
                        OnPreOrderReady( args );
                    }
                }
                else
                {
                    throw new Exception( "Could not find start and peak" );
                }


            }
            catch( Exception e )
            {
                Console.WriteLine( e.Message );
                Console.WriteLine( e.StackTrace );
            }
        }

        public double CalculateProfit(string product, double buyP, double sellP, double size)
        {
            
            ProductInfo info = dataHandler.Fetcher.ProductInfos[product];
            double profit = size * (((199 * sellP) / 200) - ((201 * buyP) / 200));
            return Math.Round(profit, info.BasePrecision);
        }

        private void AddToLongCandle( string productId, Candle newShortCandle )
        {
            try
            {
                lock( analyserCurrentLongCandlesRoot )
                {

                    if( currentLongCandles[ productId ].Close != newShortCandle.Close )
                    {
                        currentLongCandles[ productId ].Close = newShortCandle.Close;
                    }

                    if( currentLongCandles[ productId ].High < newShortCandle.High )
                    {
                        currentLongCandles[ productId ].High = newShortCandle.High;
                    }

                    if( currentLongCandles[ productId ].Low > newShortCandle.Low )
                    {
                        currentLongCandles[ productId ].Low = newShortCandle.Low;
                    }

                }
            }
            catch( Exception e)
            {
                Console.WriteLine( e.Message );
                Console.WriteLine( e.StackTrace );
            }
        }

        // b sg if !in (max - (30% of (max-min)))
        // && if short < long
        // && if short-long<((short-longMAX)*0.95)
        // && sum of 50 last emaSlopes ! <0

        // s sg if !in Abs(min +(30% of (max-min)))
        private async void ShortCandleUpdateEvent(Object source, ShortCandleUpdateEventArgs e)
        {
            try
            {
                await Task.Run(() =>
                {
                    currentShortCandles[e.ProductId] = e.NewShortCandle;

                    //addToLongCandle( e.NewShortCandle );
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }

        private async void LongCandleUpdateEvent(object source, LongCandleUpdateEventArgs e)
        {
            try
            {
                await Task.Run( () =>
                {
                    lock( analyserCurrentLongCandlesRoot )
                    {
                        foreach( var product in e.NewLongCandles.Keys )
                        {
                            if( e.NewLongCandles[ product ] != null )
                            {
                                currentLongCandles[ product ] = e.NewLongCandles[ product ];
                            }
                        }
                    }
                });
            }
            catch( Exception ex )
            {
                Console.WriteLine( ex.Message );
                Console.WriteLine( ex.StackTrace );
            }
        }

        public event EventHandler<PreOrderReadyEventArgs> PreOrderReady;
        protected virtual void OnPreOrderReady(PreOrderReadyEventArgs e)
        {
            EventHandler<PreOrderReadyEventArgs> handler = PreOrderReady;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        public DataHandler DataHandler
        {
            get
            {
                return dataHandler;
            }
        }

        bool analysisRunning;

        private SynchronizedConsoleWriter writer;
        private DataHandler dataHandler;
        private Dictionary<string, Queue<Candle>> shortProductCandles;
        private Dictionary<string, Queue<Candle>> longProductCandles;
        private ConcurrentDictionary<string, ConcurrentStack<SlopeCandle>> shortSlopes;
        private ConcurrentDictionary<string, HighLow> shortMinMax24;
        private ConcurrentDictionary<string, HighLow> shortMinMax;
        private ConcurrentDictionary<string, HighLow> longMinMax;
        private ConcurrentDictionary<string, Candle> currentShortCandles;
        private ConcurrentDictionary<string, Candle> currentLongCandles;
        private ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentStack<Ema>>> shortEmas;
        private ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentStack<Ema>>> longEmas;
        private ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentStack<Ema>>> shortEmaSlopes;
        private ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentStack<Ema>>> longEmaSlopes;
        private ConcurrentDictionary<string, bool> calculatedShortEmas;
        private ConcurrentDictionary<string, bool> calculatedLongEmas;
        private ConcurrentDictionary<string, ProductInfo> productInfos;
        private ConcurrentDictionary<string, PreOrder> prelOs;
        private ConcurrentDictionary<string, LongAnalysisResult> results;
    }

    public class PreOrderReadyEventArgs
    {
        public PreOrder PreliminaryOrder { get; set; }
    }

    public class AnalyserConfiguration
    {
        public AnalyserConfiguration()
        {

        }
    }
}
