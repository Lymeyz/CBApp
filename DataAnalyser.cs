﻿using System;
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
        public DataAnalyser( ref DataHandler dataHandler,
                             ref System.Timers.Timer aTimer,
                             ref SynchronizedConsoleWriter writer,
                             AnalyserConfiguration config )
        {
            this.writer = writer;
            this.config = config;
            this.dataHandler = dataHandler;
            this.dataHandler.UpdatedProductCandles += UpdatedProductCandles;
            dataHandler.Fetcher.ShortCandleUpdate += ShortCandleUpdateEvent;
            dataHandler.Fetcher.LongCandleUpdate += LongCandleUpdateEvent;
            aTimer.Elapsed += this.OnTimedEvent;

            fiveMinCandles = new Dictionary<string, Queue<Candle>>();
            hourCandles = new Dictionary<string, Queue<Candle>>();
            fiveMinEmas = new ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentStack<Ema>>>();
            hourEmas = new ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentStack<Ema>>>();
            fiveMinEmaSlopes = new ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentStack<Ema>>>();
            hourEmaSlopes = new ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentStack<Ema>>>();
            shortSlopes = new ConcurrentDictionary<string, ConcurrentStack<SlopeCandle>>();
            currentFiveMinCandles = new ConcurrentDictionary<string, Candle>();
            currentHourCandles = new ConcurrentDictionary<string, Candle>();
            calculatedFiveMinEmas = new ConcurrentDictionary<string, bool>();
            calculatedHourEmas = new ConcurrentDictionary<string, bool>();
            prelOs = new ConcurrentDictionary<string, PreOrder>();
            results = new ConcurrentDictionary<string, DoubleEmaAnalysisResult>();
            doubleEmaResults = new ConcurrentDictionary<string, ConcurrentDictionary<string, DoubleEmaAnalysisResult>>();

            // Double ema collections
            doubleEmaResults[ "fiveMin" ] = new ConcurrentDictionary<string, DoubleEmaAnalysisResult>();
            doubleEmaResults[ "hour" ] = new ConcurrentDictionary<string, DoubleEmaAnalysisResult>();

            productInfos = new ConcurrentDictionary<string, ProductInfo>(dataHandler.Fetcher.ProductInfos);

            foreach (var product in dataHandler.ShortProductCandles.Keys)
            {
                shortSlopes[ product ] = null;
                fiveMinEmas[product] = null;
                hourEmas[ product ] = null;
                fiveMinEmaSlopes[product] = null;
                hourEmaSlopes[ product ] = null;
                currentFiveMinCandles[ product ] = new Candle( DateTime.MinValue, 0, 0, 0, 0, 0 );
                calculatedFiveMinEmas[product] = false;
                calculatedHourEmas[ product ] = false;
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

                // check if there are previously calculated lastEma
                if (currEmas == null)
                {
                    // no old lastEma, initialize currEmas
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
                    // there are old lastEma, find latest lastEma
                    latestEmas = new Dictionary<int, Ema>();
                    foreach (int period in periods)
                    {
                        if (currEmas[period].Count != 0)
                        {
                            latestEmas[period] = currEmas[period].Newest;
                        }
                    }
                }

                // if no old lastEma, calculate SMA of each first length
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

                    // EMAs calculation based on last PrevEmas
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
                        fiveMinCandles = new Dictionary<string, Queue<Candle>>();
                        foreach( var product in dataHandler.ShortProductCandles.Keys )
                        {
                            currentQueue = dataHandler.ShortProductCandles[ product ];
                            if( currentQueue.Count > config.FiveMinCandleLowerLimit )
                            {
                                fiveMinCandles[ product ] = new Queue<Candle>( currentQueue );
                            }
                            results[ product ] = null;
                            prelOs[ product ] = null;
                        }
                    }

                    // lastEma
                    CalculateAllEmas( fiveMinCandles,
                                      ref fiveMinEmas,
                                      ref fiveMinEmaSlopes,
                                      ref calculatedFiveMinEmas,
                                      config.FiveMinDoubleEmaLengths[0],
                                      config.FiveMinDoubleEmaLengths[1] );

                    if( e.updatedLong )
                    {
                        lock( analyserLongCandlesRoot )
                        {
                            hourCandles = new Dictionary<string, Queue<Candle>>();
                            foreach( var product in dataHandler.LongProductCandles.Keys )
                            {
                                currentQueue = dataHandler.LongProductCandles[ product ];
                                if( currentQueue.Count > config.HourCandleLowerLimit )
                                {
                                    hourCandles[ product ] = new Queue<Candle>( currentQueue );
                                }
                            }
                        }

                        // hour ema lengths
                        List<int> hourLengths = new List<int>( config.HourDoubleEmaLengths );
                        hourLengths.Add( config.HourSingleEmaLength );
                        int[] hourLengthsArray = hourLengths.ToArray();

                        // Hour lastEma
                        CalculateAllEmas( hourCandles,
                                          ref hourEmas,
                                          ref hourEmaSlopes,
                                          ref calculatedHourEmas,
                                          hourLengthsArray
                                         );


                        
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }

        private void PrintEmasAndEmaSlopes(string product)
        {
            LimitedDateTimeList<Ema> currEmas;
            LimitedDateTimeList<Ema> currEmaSlopes;
            Ema currEma;
            Ema currSlope;
            //for each period, print 10 lastEma and 10 emaslopes next to eachother
            Console.WriteLine("");
            Console.WriteLine($"lastEma and emaslopes for {product}");
            foreach (int period in fiveMinEmas[product].Keys)
            {
                currEmas = new LimitedDateTimeList<Ema>(fiveMinEmas[product][period], 300);
                currEmaSlopes = new LimitedDateTimeList<Ema>(fiveMinEmaSlopes[product][period], 300);
                
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
                foreach (var product in fiveMinCandles.Keys)
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
                Queue<Candle> currCandles = new Queue<Candle>(fiveMinCandles[product]);
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
                if (fiveMinCandles.Values.Count != 0)
                {
                    if (!((e.SignalTime.Minute % 5 == 0) && (e.SignalTime.Second > 49 || e.SignalTime.Second < 10)))
                    {
                        if (!analysisRunning)
                        {
                            analysisRunning = true;
                            await Task.Run(() =>
                            {
                                //AnalyseData();
                                AnalyseDataNew();
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

        private void AnalyseDataNew()
        {
            try
            {
                // long

                // calculate long ema, double ema or both

                // calculate volatility between crosses or zero slopes

                // if volatility high enough and slope positive enough
                // analyse ema/lastEma for timing
                // input: volatility and long ema results + volatilitySettings + longEmaSettings + 

                // short

                // calculate long ema, or double ema or both

                // calculate volatility between crosses or zero slopes

                // if volatility high enough and slope positive enough
                // analyse ema/lastEma for timing


                // test double ema analysis
                DoubleEmaAnalysisResult fiveMinDoubleEmaResult;
                DoubleEmaAnalysisSettings fiveMinDoubleEmaSetting;
                foreach( var pair in fiveMinEmas.Where( p => p.Value != null ) )
                {
                    string product = pair.Key;
                    if( currentFiveMinCandles.ContainsKey( product ) )
                    {
                        Dictionary<int, Ema> currentFiveMinEmas = new Dictionary<int, Ema>();

                        CalculateNewestEma( product, ref currentFiveMinEmas, currentFiveMinCandles, fiveMinEmas );

                        fiveMinDoubleEmaSetting = new DoubleEmaAnalysisSettings( product,
                                                                                 false,
                                                                                 0.06,
                                                                                 0.01,
                                                                                 0.003,
                                                                                 0.6,
                                                                                 0.97,
                                                                                 true,
                                                                                 true,
                                                                                 config.FiveMinDoubleEmaLengths,
                                                                                 ref currentFiveMinCandles,
                                                                                 ref currentFiveMinEmas,
                                                                                 ref fiveMinEmas );

                        if( doubleEmaResults[ "fiveMin" ].ContainsKey( product ) )
                        {
                            fiveMinDoubleEmaResult = doubleEmaResults[ "fiveMin" ][ product ];
                        }
                        else
                        {
                            fiveMinDoubleEmaResult = null;
                        }

                        fiveMinDoubleEmaResult = DoubleEmaAnalyseProduct( fiveMinDoubleEmaSetting, fiveMinDoubleEmaResult );

                        if( fiveMinDoubleEmaResult != null )
                        {
                            doubleEmaResults[ "fiveMin" ][ product ] = fiveMinDoubleEmaResult;
                        }
                    }

                    if( product == "BTC-EUR" )
                    {
                        if( doubleEmaResults[ "fiveMin" ][ product ] != null )
                        {
                            VolatilityAnalysisSettings volSett = new VolatilityAnalysisSettings( product,
                                                                                                 5,
                                                                                                 false,
                                                                                                 config.FiveMinDoubleEmaLengths,
                                                                                                 currentFiveMinCandles,
                                                                                                 ref fiveMinCandles,
                                                                                                 fiveMinEmas,
                                                                                                 null,
                                                                                                 null );
                            VolatilityAnalysisResult volRes = VolatilityAnalysis( volSett );
                        }
                    }
                }

                //// Test double ema slope analysis
                //foreach( var pair in fiveMinEmaSlopes.Where( p => p.Value != null ) )
                //{
                //    string product = pair.Key;

                //    if( currentFiveMinCandles.ContainsKey( product ) )
                //    {
                //        Dictionary<int, Ema> currentFiveMinEmas = new Dictionary<int, Ema>();
                //        Dictionary<int, Ema> currentFiveMinEmaSlopes = new Dictionary<int, Ema>();

                //        CalculateNewestEmaAndSlope( product,
                //                                 ref currentFiveMinEmas,
                //                                 ref currentFiveMinEmaSlopes,
                //                                 ref currentFiveMinCandles,
                //                                 ref fiveMinEmas,
                //                                 ref fiveMinEmaSlopes );

                //        DoubleEmaAnalysisSettings fiveMinDoubleEmaSlopeSetting =
                //            new DoubleEmaAnalysisSettings( product,
                //                                          true,
                //                                          0.04,
                //                                          0.15,
                //                                          true,
                //                                          true,
                //                                          config.FiveMinDoubleEmaLengths,
                //                                          ref currentFiveMinCandles,
                //                                          ref currentFiveMinEmas,
                //                                          ref currentFiveMinEmaSlopes,
                //                                          ref fiveMinEmas );

                //        DoubleEmaAnalyseProduct( fiveMinDoubleEmaSlopeSetting, null );
                //    }
                //}

                // test single ema analysis
                SingleEmaAnalysisSettings hourSingleEmaSettings;
                SingleEmaAnalysisResult hourSingleEmaResult;
                VolatilityAnalysisSettings hourVolSettings;
                VolatilityAnalysisResult hourVolResult;

                foreach( var pair in hourEmas.Where( p => p.Value != null ) )
                {
                    string product = pair.Key;
                    if( currentHourCandles.ContainsKey( product ) && (product == "BTC-EUR" || product == "ETH-EUR"))
                    {
                        hourSingleEmaSettings = new SingleEmaAnalysisSettings( product,
                                                                               0.0024,
                                                                               0.0014,
                                                                               0.009,
                                                                               0.009,
                                                                               0.0012,
                                                                               0.008,
                                                                               true,
                                                                               0.004,
                                                                               0.006,
                                                                               0.0005,
                                                                               0.0075,
                                                                               false,
                                                                               0.004,
                                                                               0.004,
                                                                               true,
                                                                               true,
                                                                               true,
                                                                               0.25,
                                                                               config.HourSingleEmaLength,
                                                                               ref currentHourCandles,
                                                                               ref hourEmas,
                                                                               ref hourEmaSlopes );
                        hourSingleEmaResult = SingleEmaAnalyseProduct( hourSingleEmaSettings, null );

                        if( hourSingleEmaResult != null )
                        {
                            Ema latestEma;
                            hourEmas[ product ][ 45 ].TryPeek( out latestEma );
                            hourVolSettings = 
                                new VolatilityAnalysisSettings( product,
                                                               5,
                                                               true,
                                                               new int[] { 45 },
                                                               hourSingleEmaSettings.CurrentCandles,
                                                               ref hourCandles,
                                                               null,
                                                               hourEmaSlopes,
                                                               latestEma
                                                               );

                            hourVolResult = VolatilityAnalysis( hourVolSettings );
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

        private void CalculateNewestEmaAndSlope( string product,
                                                 ref Dictionary<int, Ema> currentEmas,
                                                 ref Dictionary<int, Ema> currentEmaSlopes,
                                                 ref ConcurrentDictionary<string, Candle> currentCandles,
                                                 ref ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentStack<Ema>>> prevEmas,
                                                 ref ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentStack<Ema>>> emaSlopes )
        {
            try
            {
                Ema newestEma = null;
                double emaPrice;

                foreach( int period in prevEmas[ product ].Keys )
                {
                    double k = 2.0 / (period + 1);
                    
                    prevEmas[ product ][ period ].TryPeek( out newestEma );

                    emaPrice = (currentCandles[ product ].Avg * k) + (newestEma.Price * (1 - k));

                    currentEmas[ period ] = new Ema( period,
                                                     emaPrice,
                                                     currentCandles[ product ].Time );

                    currentEmaSlopes[ period ] = new Ema( period,
                                                          emaPrice - newestEma.Price,
                                                          currentCandles[ product ].Time );
                }
            }
            catch( Exception e )
            {
                Console.WriteLine( e.StackTrace );
                Console.WriteLine( e.Message );
            }
        }

        /// <summary>
        /// Inputs current candles and previously calculated lastEma,
        /// returns current lastEma into currentEmas referenced in parameters
        /// </summary>
        /// <param name="product"></param>
        /// <param name="currentEmas">To input current lastEma</param>
        /// <param name="currentCandles"></param>
        /// <param name="prevEmas"></param>
        private void CalculateNewestEma( string product,
                                         ref Dictionary<int, Ema> currentEmas,
                                         ConcurrentDictionary<string, Candle> currentCandles,
                                         ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentStack<Ema>>> prevEmas )
        {
            try
            {
                Ema newestEma = null;
                foreach( int period in prevEmas[product].Keys )
                {
                    double k = 2.0 / (period + 1);
                    double emaPrice;
                    prevEmas[ product ][ period ].TryPeek( out newestEma );
                    emaPrice = (currentCandles[ product ].Avg * k) + (newestEma.Price * (1 - k));

                    currentEmas[ period ] = new Ema( period, emaPrice, currentCandles[ product ].Time );
                }
            }
            catch( Exception e )
            {
                Console.WriteLine( e.StackTrace );
                Console.WriteLine( e.Message );
            }
        }

        private Ema CalculateNewestEma( string product,
                                        ConcurrentDictionary<string, Candle> currentCandles,
                                        ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentStack<Ema>>> prevEmas )
        {
            try
            {
                Ema newestEma = null;
                foreach( int period in prevEmas[ product ].Keys )
                {
                    double k = 2.0 / (period + 1);
                    double emaPrice;
                    prevEmas[ product ][ period ].TryPeek( out newestEma );
                    emaPrice = (currentCandles[ product ].Avg * k) + (newestEma.Price * (1 - k));

                    newestEma = new Ema( period, emaPrice, currentCandles[ product ].Time );
                }

                return newestEma;
            }
            catch( Exception e )
            {
                Console.WriteLine( e.StackTrace );
                Console.WriteLine( e.Message );
                return null;
            }
        }

        private Ema CalculateNewestEma( Candle currentCandle,
                                        Ema prevEma )
        {
            try
            {
                Ema newestEma = prevEma;
                int length = newestEma.Length;

                double k = 2.0 / (length + 1);
                double emaPrice;

                emaPrice = (currentCandle.Avg * k) + (newestEma.Price * (1 - k));

                newestEma = new Ema( length, emaPrice, currentCandle.Time );

                return newestEma;
            }
            catch( Exception e )
            {
                Console.WriteLine( e.StackTrace );
                Console.WriteLine( e.Message );
                return null;
            }
        }

        private Ema CalculateNewestEmaSlope( string product,
                                             ConcurrentDictionary<string, Candle> currentCandles,
                                             ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentStack<Ema>>> prevEmas,
                                             ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentStack<Ema>>> emaSlopes )
        {
            try
            {
                Ema lastEma = null;
                Ema newEmaSlope = null;
                double emaPrice;

                foreach( int period in prevEmas[ product ].Keys )
                {

                    double k = 2.0 / (period + 1);

                    prevEmas[ product ][ period ].TryPeek( out lastEma );

                    emaPrice = (currentCandles[ product ].Avg * k) + (lastEma.Price * (1 - k));

                    newEmaSlope = new Ema( period,
                                           emaPrice - lastEma.Price,
                                           currentCandles[ product ].Time );
                }

                return newEmaSlope;
            }
            catch( Exception e )
            {
                Console.WriteLine( e.StackTrace );
                Console.WriteLine( e.Message );
                return null;
            }
        }

        private Ema CalculateNewestEmaSlope( Candle currentCandle,
                                             Ema lastEma )
        {
            try
            {
                Ema newEmaSlope;
                int length = lastEma.Length;
                double emaPrice;

                double k = 2.0 / (length + 1);

                emaPrice = ( currentCandle.Avg * k) + (lastEma.Price * (1 - k));

                newEmaSlope = new Ema( length,
                                       emaPrice - lastEma.Price,
                                       currentCandle.Time );

                return newEmaSlope;
            }
            catch( Exception e )
            {
                Console.WriteLine( e.StackTrace );
                Console.WriteLine( e.Message );
                return null;
            }
        }

        private Ema CalculateNewestEmaSlope( string product,
                                             ConcurrentDictionary<string, Candle> currentCandles,
                                             Ema lastEma,
                                             LimitedDateTimeList<Ema> emaSlopes )
        {
            try
            {
                Ema lastEmaSlope = emaSlopes.Newest;
                Ema newestEmaSlope;
                int length = lastEma.Length;
                double emaPrice;

                double k = 2.0 / (length + 1);

                emaPrice = (currentCandles[ product ].Avg * k) + (lastEma.Price * (1 - k));

                newestEmaSlope = new Ema( length,
                                       emaPrice - lastEma.Price,
                                       currentCandles[ product ].Time );

                return newestEmaSlope;
            }
            catch( Exception e )
            {
                Console.WriteLine( e.StackTrace );
                Console.WriteLine( e.Message );
                return null;
            }
        }

        private SingleEmaAnalysisResult SingleEmaAnalyseProduct( SingleEmaAnalysisSettings sSett, SingleEmaAnalysisResult inResult )
        {
            try
            {
                string product = sSett.Product;

                int length = sSett.EmaLength;

                //LimitedDateTimeList<Ema> prevEmaSlopes;

                SingleEmaAnalysisResult result = null;
                SingleEmaAnalysisResult outResult = null;

                if( inResult != null )
                {
                    result = inResult;
                }

                Ema newestEma = CalculateNewestEma( sSett.CurrentCandles[product], sSett.PrevEmas.Newest );
                Ema newestEmaSlope = CalculateNewestEmaSlope( sSett.CurrentCandles[product], sSett.PrevEmas.Newest );
                Ema currEmaSlope = newestEmaSlope;
                Ema prevEmaSlope = null;
                Ema currEma = newestEma;
                Ema prevEma = null;

                double newestSlopeRate = newestEmaSlope - sSett.PrevEmaSlopes.Newest;
                double currSlopeRate = newestSlopeRate;

                DateTime tStartTime = DateTime.MinValue;
                double peakEmaPrice = -1;

                if( result == null )
                {

                    int count = sSett.PrevEmaSlopes.Count;
                    for( int i = 0; i < count; i++ )
                    {
                        // go backwards, add rates of change, increase count, find zero, calculate average
                        if( result == null )
                        {
                            result = new SingleEmaAnalysisResult( sSett.SlopeRateAvgP );

                            if( currEmaSlope >= 0 )
                            {
                                result.Trend = true;
                            }
                            else
                            {
                                result.Trend = false;
                            }

                            result.LastUpdate = newestEmaSlope.Time;
                            result.SlopeRates.AddFirst( currSlopeRate );

                            prevEmaSlope = currEmaSlope;
                            currEmaSlope = sSett.PrevEmaSlopes.GetRemoveNewest();
                            prevEma = currEma;
                            currEma = sSett.PrevEmas.GetRemoveNewest();
                        }
                        else
                        {
                            // check if slope passed through zero, otherwise continue

                            if( result.Trend == false )
                            {
                                if( currEmaSlope >= 0 )
                                {
                                    peakEmaPrice = currEma.Price;
                                    tStartTime = currEmaSlope.Time;
                                    break;
                                }
                            }
                            else
                            {
                                if( currEmaSlope < 0 )
                                {
                                    peakEmaPrice = currEma.Price;
                                    tStartTime = currEmaSlope.Time;
                                    break;
                                }
                            }

                            currSlopeRate = prevEmaSlope - currEmaSlope;
                            result.SlopeRates.AddLast( currSlopeRate );

                            prevEmaSlope = currEmaSlope;
                            currEmaSlope = sSett.PrevEmaSlopes.GetRemoveNewest();

                            prevEma = currEma;
                            currEma = sSett.PrevEmas.GetRemoveNewest();
                        }
                    }

                    if( tStartTime != DateTime.MinValue &&
                        peakEmaPrice != -1)
                    {
                        result.StartPrice = peakEmaPrice;
                        result.Time = tStartTime;
                    }
                    else
                    {
                        throw new Exception( "No find start of trend?" );
                    }
                }
                else
                {
                    if( result.Trend == true )
                    {
                        if( newestEmaSlope < 0 )
                        {
                            result = null;
                            outResult = SingleEmaAnalyseProduct( sSett, null );
                        }

                    }
                    else
                    {
                        if( newestEmaSlope >= 0 )
                        {
                            result = null;
                            outResult = SingleEmaAnalyseProduct( sSett, null );
                        }

                    }
                }

                if( result != null )
                {
                    if( result.LastUpdate < newestEmaSlope.Time )
                    {
                        result.SlopeRates.AddFirst( currSlopeRate );
                    }
                    else if( result.SlopeRates.First.Value != newestSlopeRate )
                    {
                        result.UpdateSlopeRateAverage( newestSlopeRate );
                    }

                    result.SlopeRateAverageLength = Convert.ToInt32( Math.Round( sSett.SlopeRateAvgP * result.SlopeRates.Count, 0 ) );

                    if( result.SlopeRates.Count > result.SlopeRateAverageLength )
                    {
                        int difference = result.SlopeRates.Count - result.SlopeRateAverageLength;
                        for( int i = 0; i < difference; i++ )
                        {
                            result.SlopeRates.RemoveLast();
                        }
                    }


                    result.BuyOk = false;
                    result.SellOk = false;
                    result.SellOff = false;

                    // make decisions...
                    if( sSett.BTrigger )
                    {
                        // simple slope or override
                        if( (sSett.BS1 != -1 && (newestEmaSlope >= 0 ||
                            newestEmaSlope >= sSett.BS1 * newestEma) ) ||
                            sSett.BS2Override )
                        {
                            // slope rate 
                            double slopeAbs = Math.Abs( newestEmaSlope.Price );
                            if( sSett.BS2 != -1 &&
                                ( result.SlopeRateAverage > 0 && result.SlopeRateAverage > sSett.BS2 * slopeAbs ) )
                            {
                                // slope rate and peak return
                                if( sSett.BPeakRP != -1 &&
                                        ( newestEma.Price > result.StartPrice * sSett.BPeakRP ) )
                                {
                                    // peak return window
                                    if( sSett.BPeakWindow != -1 && ( newestEma.Price < ( result.StartPrice * ( sSett.BPeakRP + sSett.BPeakWindow ) ) ) )
                                    {
                                        result.BuyOk = true;
                                    }
                                    else
                                    {
                                        result.BuyOk = true;
                                    }
                                }
                                else
                                {
                                    result.BuyOk = true;
                                }
                            }
                            // simple slope
                            else if( !sSett.BS2Override )
                            {
                                // simple slope and peak return
                                if( sSett.BPeakWindow != -1 && ( newestEma.Price < ( result.StartPrice * ( sSett.BPeakRP + sSett.BPeakWindow) ) ) )
                                {
                                    result.BuyOk = true;
                                }
                                // only simple slope
                                else
                                {
                                    result.BuyOk = true;
                                }
                            }
                        }
                    }

                    if( sSett.STrigger )
                    {
                        // simple slope and slope rate or override
                        if( ( sSett.SS1 != -1 && 
                            
                            ( newestEmaSlope >= 0 || 
                            newestEmaSlope <= sSett.SS1 * newestEma ) ) || 
                            sSett.SS2Override)
                        {
                            // slope rate
                            if( sSett.SS2 != -1 && 
                                ( result.SlopeRateAverage <= sSett.SS2 * newestEmaSlope ) )
                            {
                                // peak return
                                if( sSett.SPeakRP != -1 && 
                                    ( newestEma.Price < result.StartPrice * sSett.SPeakRP ) )
                                {
                                    // peak return window
                                    if( sSett.SPeakWindow != -1 && 
                                        ( newestEma.Price > ( result.StartPrice * ( sSett.SPeakRP + sSett.SPeakWindow ) ) ) )
                                    {
                                        result.SellOk = true;
                                    }
                                    else
                                    {
                                        result.SellOk = true;
                                    }
                                }
                                else
                                {
                                    result.SellOk = true;
                                }
                            }
                            // simple slope
                            else
                            {
                                // peak return
                                if( sSett.SPeakRP != -1 && 
                                        ( newestEma.Price < result.StartPrice * sSett.SPeakRP ) )
                                {
                                    // peak return window
                                    if( sSett.SPeakWindow != -1 &&
                                        ( newestEma.Price > ( result.StartPrice * ( sSett.SPeakRP + sSett.SPeakWindow ) ) ) )
                                    {
                                        result.SellOk = true;
                                    }
                                    else
                                    {
                                        result.SellOk = true;
                                    }
                                }
                                else
                                {
                                    result.SellOk = true;
                                }
                            }
                        }
                    }

                    if( sSett.SOffTrigger )
                    {
                        // simple slope and slope rate or override
                        if( ( sSett.SOffSP != -1 && (newestEmaSlope < 0 ||
                            newestEmaSlope <= sSett.SOffSP * newestEma ) ) )
                        {
                            // slope rate
                            if( sSett.SOffSSP != -1 &&
                                ( result.SlopeRateAverage <= sSett.SOffSSP * newestEmaSlope ) )
                            {
                                // peak return
                                if( sSett.SOffPeakRP != -1 &&
                                    ( newestEma.Price < result.StartPrice * sSett.SOffPeakRP ) )
                                {
                                    // peak return window
                                    if( sSett.SOffPeakWindow != -1 &&
                                        ( newestEma.Price > ( result.StartPrice * ( sSett.SOffPeakRP + sSett.SOffPeakWindow ) ) ) )
                                    {
                                        result.SellOff = true;
                                    }
                                    else
                                    {
                                        result.SellOff = true;
                                    }
                                }
                                else
                                {
                                    result.SellOff = true;
                                }
                            }
                            // simple slope
                            else
                            {
                                // peak return
                                if( sSett.SOffPeakRP != -1 &&
                                        (newestEma.Price < result.StartPrice * sSett.SOffPeakRP) )
                                {
                                    // peak return window
                                    if( sSett.SOffPeakWindow != -1 &&
                                        (newestEma.Price > (result.StartPrice * (sSett.SOffPeakRP + sSett.SOffPeakWindow))) )
                                    {
                                        result.SellOk = true;
                                    }
                                    else
                                    {
                                        result.SellOk = true;
                                    }
                                }
                                else
                                {
                                    result.SellOk = true;
                                }
                            }
                        }

                    }
                }
                if( result != null )
                {
                    if( result.BuyOk || result.SellOk || result.SellOff )
                    {
                        outResult = result;
                    }
                }
                
                return outResult;
            }
            catch( Exception e )
            {
                Console.WriteLine( e.StackTrace );
                Console.WriteLine( e.Message );
                return null;
            }
        }

        // Find peaks based on analysis of lastEma and do a
        private VolatilityAnalysisResult VolatilityAnalysis( VolatilityAnalysisSettings volSett )
        {
            try
            {
                string product = volSett.Product;

                VolatilityAnalysisResult result = null;
                LinkedList<double> peaks = null;
                LinkedList<DateTime> peakTimes = null;
                LinkedList<DateTime> switchTimes = null;

                Candle newestCandle = null;
                Candle currentCandle = null;
                Ema newestShortEma = null;
                Ema newestLongEma = null;
                Ema currentShortEma = null;
                Ema currentLongEma = null;

                DateTime lastSwitch = DateTime.MinValue;

                if( !volSett.SlopeBased )
                {
                    int shortLength = volSett.Lengths[ 0 ];
                    int longLength = volSett.Lengths[ 1 ];

                    newestCandle = volSett.CurrentCandles[ product ];
                    currentCandle = newestCandle;

                    // calculate current lastEma
                    newestShortEma = CalculateNewestEma( newestCandle, volSett.Emas[ shortLength ].Newest );
                    newestLongEma = CalculateNewestEma( newestCandle, volSett.Emas[ longLength ].Newest );

                    currentShortEma = newestShortEma;
                    currentLongEma = newestLongEma;

                    // go through lastEma from newest to oldest
                    bool trend = false;
                    double peak = -1;
                    DateTime peakTime = DateTime.MinValue;

                    int count = volSett.Emas[ longLength ].Count;

                    for( int i = 0; i < count ; i++ )
                    {
                        if( peaks == null )
                        {
                            peaks = new LinkedList<double>();
                            peakTimes = new LinkedList<DateTime>();
                            switchTimes = new LinkedList<DateTime>();

                            if( currentShortEma < currentLongEma )
                            {
                                peakTime = currentCandle.Time;
                                peak = currentCandle.Avg;
                                trend = false;
                            }
                            else if( currentShortEma >= currentLongEma )
                            {
                                peakTime = currentCandle.Time;
                                peak = currentCandle.Avg;
                                trend = true;
                            }
                        }
                        else
                        {
                            if( trend == false )
                            {
                                if( currentShortEma > currentLongEma )
                                {

                                    trend = true;

                                    // trend switch, add peak, peaktime, switchtime
                                    switchTimes.AddLast( currentCandle.Time );
                                    peakTimes.AddLast( peakTime );
                                    peaks.AddLast( peak );

                                    // current peak to most recent candle
                                    peak = currentCandle.Avg;
                                    peakTime = DateTime.MinValue;

                                    if( currentCandle.Time > lastSwitch )
                                    {
                                        lastSwitch = currentCandle.Time;
                                    }
                                }
                                else
                                {
                                    if( currentCandle.Avg < peak )
                                    {
                                        peak = currentCandle.Avg;
                                        peakTime = currentCandle.Time;
                                    }
                                }
                            }
                            else
                            {
                                if( currentShortEma < currentLongEma )
                                {
                                    trend = false;

                                    peakTimes.AddLast( peakTime );
                                    switchTimes.AddLast( currentCandle.Time );
                                    peaks.AddLast( peak );

                                    peak = currentCandle.Avg;
                                    peakTime = DateTime.MinValue;

                                    if( currentCandle.Time > lastSwitch )
                                    {
                                        lastSwitch = currentCandle.Time;
                                    }
                                }
                                else
                                {
                                    if( currentCandle.Avg > peak )
                                    {
                                        peak = currentCandle.Avg;
                                        peakTime = currentCandle.Time;
                                    }
                                }
                            }
                        }

                        currentCandle = volSett.Candles.GetRemoveNewest();
                        currentShortEma = volSett.Emas[ shortLength ].GetRemoveNewest();
                        currentLongEma = volSett.Emas[ longLength ].GetRemoveNewest();
                    }
                }
                else
                {
                    newestCandle = volSett.CurrentCandles[ product ];
                    currentCandle = newestCandle;

                    // calculate current lastEma
                    Ema newestEmaSlope = 
                        CalculateNewestEmaSlope( product, volSett.CurrentCandles, volSett.LastEma, volSett.EmaSlopes);
                    Ema currentEmaSlope = newestEmaSlope;

                    // go through lastEma from newest to oldest
                    bool trend = false;
                    double peak = -1;
                    DateTime peakTime = DateTime.MinValue;
                    DateTime switchTime = DateTime.MinValue;
                    int count = volSett.EmaSlopes.Count;

                    for( int i = 0; i < count; i++ )
                    {
                        if( peaks == null )
                        {
                            peaks = new LinkedList<double>();
                            peakTimes = new LinkedList<DateTime>();
                            switchTimes = new LinkedList<DateTime>();

                            if( newestEmaSlope < 0 )
                            {
                                peakTime = currentCandle.Time;
                                peak = currentCandle.Avg;
                                trend = false;
                            }
                            else if( newestEmaSlope >= 0 )
                            {
                                peakTime = currentCandle.Time;
                                peak = currentCandle.Avg;
                                trend = true;
                            }
                        }
                        else
                        {
                            if( trend == false )
                            {
                                if( currentEmaSlope >= 0 )
                                {

                                    trend = true;

                                    // trend switch, add peak, peaktime, switchtime
                                    peakTimes.AddLast( peakTime );
                                    peaks.AddLast( peak );
                                    switchTimes.AddLast( currentCandle.Time );

                                    // set current peak to most recent candle
                                    peak = currentCandle.Avg;
                                    peakTime = currentCandle.Time;
                                    

                                    // most recent trend switch
                                    if( currentCandle.Time > lastSwitch )
                                    {
                                        lastSwitch = currentCandle.Time;
                                    }
                                }
                                else
                                {
                                    // if candle is lower than current peak, set current peak
                                    if( currentCandle.Avg < peak )
                                    {
                                        peak = currentCandle.Avg;
                                        peakTime = currentCandle.Time;
                                    }
                                }
                            }
                            else
                            {
                                if( currentEmaSlope < 0 )
                                {

                                    trend = false;

                                    // trend switch, add peak, peaktime, switchtime
                                    peakTimes.AddLast( peakTime );
                                    peaks.AddLast( peak );
                                    switchTimes.AddLast( currentCandle.Time );

                                    // set peak to current candle
                                    peak = currentCandle.Avg;
                                    peakTime = currentCandle.Time;
                                    
                                    // most recent trend switch
                                    if( currentCandle.Time > lastSwitch )
                                    {
                                        lastSwitch = currentCandle.Time;
                                    }
                                }
                                else
                                {
                                    // if candle is higher than current peak, set current peak
                                    if( currentCandle.Avg > peak )
                                    {
                                        peak = currentCandle.Avg;
                                        peakTime = currentCandle.Time;
                                    }
                                }
                            }
                        }

                        // Advance to next slope and candle
                        currentCandle = volSett.Candles.GetRemoveNewest();
                        currentEmaSlope = volSett.EmaSlopes.GetRemoveNewest();
                    }
                }

                if( peaks != null )
                {
                    // Calculate ema of difference between peaks if there has been enough switches
                    if( peaks.Count > volSett.VolatilityLength * 2 )
                    {
                        // Advance through list from oldest to newest, first two nodes
                        LinkedListNode<double> node1 = peaks.Last.Previous;
                        LinkedListNode<double> node2 = peaks.Last;

                        // Calculate absolute value of difference between first peaks
                        double peakDiff = Math.Abs( node1.Value - node2.Value );

                        // Initial values and k for EMA calculation
                        int count = 0;
                        double SMA = 0;
                        double currEma = -1;
                        double prevEma = -1;
                        double k = 2.0 / (volSett.VolatilityLength + 1);

                        LinkedList<double> volEmas = new LinkedList<double>();

                        do
                        {
                            count++;

                            // Add peakDiff to SMA for seeding 
                            if( count < volSett.VolatilityLength )
                            {
                                SMA += peakDiff;
                            }
                            // Calculate SMA
                            else if( count == volSett.VolatilityLength )
                            {
                                SMA += peakDiff;
                                SMA = SMA / count;
                            }
                            // Calculate first EMA from SMA, add to list
                            else if( count == volSett.VolatilityLength + 1 )
                            {
                                currEma = (peakDiff * k) + (SMA * (1 - k));
                                volEmas.AddFirst( currEma );
                                prevEma = currEma;
                            }
                            // Calculate remaining EMAs and add to list
                            else
                            {
                                currEma = (peakDiff * k) + (prevEma * (1 - k));
                                volEmas.AddFirst( currEma );
                                prevEma = currEma;
                            }

                            // Advance through list from oldest to newest
                            node2 = node1;
                            node1 = node1.Previous;

                            // Calculate absolute value of difference between peaks
                            peakDiff = Math.Abs( node1.Value - node2.Value );

                            if( node1.Previous == null )
                            {
                                currEma = (peakDiff * k) + (prevEma * (1 - k));
                                volEmas.AddFirst( currEma );
                                prevEma = currEma;
                            }

                        } while( node1.Previous != null );

                        double latestVolEma = -1;
                        if( volSett.CurrentCandles[ product ].Time > lastSwitch 
                            && volSett.CurrentCandles[ product ].Time > peakTimes.First.Value )
                        {
                            // Calculate current EMA as if it is a peak, using .Close
                            peakDiff = Math.Abs( volSett.CurrentCandles[product].Close - peaks.First.Value );
                            latestVolEma = (peakDiff * k) + (currEma * (1 - k));
                            //volEmas.AddFirst( latestVolEma );
                            prevEma = currEma;
                        }
                        else
                        {
                            latestVolEma = volEmas.First.Value;
                        }

                        if( latestVolEma != -1 )
                        {
                            result = new VolatilityAnalysisResult( volSett.Product, peaks, volEmas, latestVolEma, peakTimes, switchTimes );
                        }
                        else
                        {
                            result = new VolatilityAnalysisResult( volSett.Product, peaks, volEmas, volEmas.First.Value, peakTimes, switchTimes );
                        }
                        
                    }
                }

                return result;
            }
            catch( Exception e )
            {
                Console.WriteLine( e.Message );
                Console.WriteLine( e.StackTrace );
                return null;
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
                    currentLongCandlesCopy = new Dictionary<string, Candle>( currentHourCandles );
                }

                foreach( string product in hourCandles.Keys )
                {
                    hasLongEmas = true;
                    emaPeriods = new int[] { 6, 12 };

                    if( hourCandles.ContainsKey( product ) && currentLongCandlesCopy.ContainsKey( product ) )
                    {
                        if( hourCandles[ product ] != null && currentLongCandlesCopy[ product ] != null )
                        {
                            foreach( int period in emaPeriods )
                            {
                                if( hourEmas[ product ][ period ] != null )
                                {
                                    if( hourEmas[ product ][ period ].Count == 0 )
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
                                    hourEmas[ product ][ period ].TryPeek( out newestEma );
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
                                                        ref hourEmas,
                                                        ref currentLongEmas,
                                                        ref currentLongEmaSlopes,
                                                        ref currentLongCandlesCopy);
                                // SellOff event --> director



                                // do short analysis

                                // Analyse lastEma up to {time} minutes back in time.
                                // Look for shorter ema under longer ema with
                                // increasing distance, flag this. A watching
                                // function will wait for the turn, assess viability
                                // in relation to pricing situation and in case of
                                // positive assessment raise an event for OrderDirector

                                // if currentCandle[product]!=null
                                // if lastEma[product][period] != null
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

                                        if( fiveMinCandles.ContainsKey( product ) && currentFiveMinCandles.ContainsKey( product ) )
                                        {
                                            if( results[ product ].SellOff )
                                            {
                                                PreOrder pre = new PreOrder( product, DateTime.UtcNow, false );
                                                pre.Price = currentFiveMinCandles[ product ].Close;
                                                pre.SellOff = true;
                                                PreOrderReadyEventArgs args = new PreOrderReadyEventArgs();
                                                args.PreliminaryOrder = new PreOrder( pre );
                                                OnPreOrderReady( args );
                                            }
                                            else if( results[ product ].BuyOk || results[ product ].SellOk )
                                            {
                                                if( fiveMinCandles[ product ] != null && currentFiveMinCandles[ product ] != null )
                                                {
                                                    foreach( int period in emaPeriods )
                                                    {
                                                        if( fiveMinEmas[ product ][ period ] != null )
                                                        {
                                                            if( fiveMinEmas[ product ][ period ].Count == 0 )
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
                                                            fiveMinEmas[ product ][ period ].TryPeek( out newestEma );
                                                            emaPrice = (currentFiveMinCandles[ product ].Avg * k) + (newestEma.Price * (1 - k));
                                                            currentEmas[ period ] = new Ema( period,
                                                                emaPrice,
                                                                currentFiveMinCandles[ product ].Time );
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

        private DoubleEmaAnalysisResult DoubleEmaAnalyseProduct( DoubleEmaAnalysisSettings aSett, DoubleEmaAnalysisResult inResult )
        {
            try
            {
                string product = aSett.Product;

                int shortPeriod = aSett.Periods[ 0 ];
                int longPeriod = aSett.Periods[ 1 ];

                Dictionary<int, LimitedDateTimeList<Ema>> prevEmas = null;
                Dictionary<int, LimitedDateTimeList<Ema>> prevEmaSlopes = null;

                Ema newestShortEmaSlope = null;
                Ema newestLongEmaSlope = null;
                Ema currShortEmaSlope = null;
                Ema currLongEmaSlope = null;

                Ema newestShortEma = null;
                Ema newestLongEma = null;
                Ema currShortEma = null;
                Ema currLongEma = null;

                double currDiff;

                DoubleEmaAnalysisResult result = null;

                if( inResult != null )
                {
                    result = inResult;
                }
                

                // Saves the effort of running analysis during certain 
                // circumstances.
                bool noStart = false;

                if( aSett.Slopes )
                {
                    prevEmaSlopes = new Dictionary<int, LimitedDateTimeList<Ema>>();

                    prevEmaSlopes[ shortPeriod ] = new LimitedDateTimeList<Ema>( aSett.EmaSlopes[ product ][ shortPeriod ],
                        aSett.EmaSlopes[ product ][ shortPeriod ].Count );

                    prevEmaSlopes[ longPeriod ] = new LimitedDateTimeList<Ema>( aSett.EmaSlopes[ product ][ longPeriod ],
                        aSett.EmaSlopes[ product ][ shortPeriod ].Count );

                    newestShortEmaSlope = aSett.CurrEmaSlopes[ shortPeriod ];
                    newestLongEmaSlope = aSett.CurrEmaSlopes[ longPeriod ];
                    currShortEmaSlope = aSett.CurrEmaSlopes[ shortPeriod ];
                    currLongEmaSlope = aSett.CurrEmaSlopes[ longPeriod ];

                    newestShortEma = aSett.CurrEmas[ shortPeriod ];
                    newestLongEma = aSett.CurrEmas[ longPeriod ];
                    currShortEma = aSett.CurrEmas[ shortPeriod ];
                    currLongEma = aSett.CurrEmas[ longPeriod ];
                }
                else
                {
                    newestShortEma = aSett.CurrEmas[ shortPeriod ];
                    newestLongEma = aSett.CurrEmas[ longPeriod ];
                    currShortEma = aSett.CurrEmas[ shortPeriod ];
                    currLongEma = aSett.CurrEmas[ longPeriod ];

                    // noStart condition for normal double ema analysis
                    if( aSett.BTrigger == false && newestShortEma < newestLongEma )
                    {
                        noStart = true;
                    }
                    if( aSett.STrigger == false && newestShortEma >= newestLongEma )
                    {
                        noStart = true;
                    }

                    if( !noStart )
                    {
                        prevEmas = new Dictionary<int, LimitedDateTimeList<Ema>>();

                        prevEmas[ shortPeriod ] = new LimitedDateTimeList<Ema>( aSett.Emas[ product ][ shortPeriod ],
                            aSett.Emas[ product ][ shortPeriod ].Count );

                        prevEmas[ longPeriod ] = new LimitedDateTimeList<Ema>( aSett.Emas[ product ][ longPeriod ],
                            aSett.Emas[ product ][ longPeriod ].Count );
                    }
                }

                if( !noStart )
                {
                    // double ema slope analysis
                    if( aSett.Slopes )
                    {
                        

                        if( newestShortEma >= newestLongEma )
                        {
                            if( aSett.BTrigger )
                            {
                                result = new DoubleEmaAnalysisResult();

                                result.Trend = false;

                                if( newestLongEmaSlope >= 0 + (aSett.BDiffP * newestLongEma.Price) )
                                {
                                    // buy
                                    result.BuyOk = true;
                                    result.Price = currentFiveMinCandles[ product ].Avg;
                                    writer.Write( $"Buy {product} at {result.Price}" );
                                }
                            }
                        }
                        else
                        {
                            if( aSett.STrigger )
                            {
                                result = new DoubleEmaAnalysisResult();
                                result.Trend = true;

                                if( Math.Abs( newestShortEmaSlope.Price ) > aSett.SDiffP * newestLongEmaSlope )
                                {
                                    // sell
                                    result.SellOk = true;
                                    result.Price = currentFiveMinCandles[ product ].Avg;
                                    writer.Write( $"Sell {product} at {result.Price}" );
                                }
                            }
                        }
                    }
                    // non slope double ema analysis
                    else
                    {
                        if( result == null )
                        {
                            bool secondary = false;

                            for( int i = 0; i < prevEmas[ longPeriod ].Count + 1; i++ )
                            {
                                if( !secondary )
                                {
                                    if( result == null )
                                    {
                                        // Find current trend
                                        currDiff = Math.Abs( currShortEma - currLongEma );

                                        // Current result is null, initialize and set to current double ema trend.
                                        // Short under long is considered a sinking trend,
                                        // opposite for long under short.
                                        // Magnitude of difference between short and long is also taken into account,
                                        // in order to find peaks and troughs.
                                        if( currShortEma.Price < currLongEma.Price )
                                        {
                                            result = new DoubleEmaAnalysisResult();
                                            result.Trend = false;

                                            result.PeakPrice = currShortEma.Price;
                                            result.PeakDiff = currDiff;
                                            result.PeakTime = currShortEma.Time;
                                        }
                                        else if( currShortEma.Price >= currLongEma.Price )
                                        {
                                            result = new DoubleEmaAnalysisResult();
                                            result.Trend = true;

                                            result.PeakPrice = currShortEma.Price;
                                            result.PeakDiff = currDiff;
                                            result.PeakTime = currShortEma.Time;
                                        }

                                        // Get next ema pair
                                        currShortEma = prevEmas[ shortPeriod ].GetRemoveNewest();
                                        currLongEma = prevEmas[ longPeriod ].GetRemoveNewest();
                                    }
                                    // Determine phase of trend
                                    else
                                    {
                                        // Current difference in ema-pair
                                        currDiff = Math.Abs( currShortEma - currLongEma );

                                        // Find peak difference
                                        if( result.PeakDiff < currDiff )
                                        {
                                            result.PeakPrice = currShortEma.Price;
                                            result.PeakDiff = currDiff;
                                            result.PeakTime = currShortEma.Time;
                                        }

                                        // Trough trend
                                        if( result.Trend == false )
                                        {
                                            // Find start of trend
                                            if( currShortEma >= currLongEma )
                                            {
                                                result.Time = currShortEma.Time;

                                                // analyse previous trend
                                                secondary = true;
                                            }
                                        }
                                        // Peak trend
                                        else
                                        {
                                            // Find start of trend
                                            if( currShortEma <= currLongEma )
                                            {
                                                result.Time = currShortEma.Time;

                                                // analyse previous trend
                                                secondary = true;
                                            }
                                        }

                                        // Check if current peak is most relevant
                                        // PeakDiff always initialized to -1
                                        if( result.PeakDiff != -1 )
                                        {
                                            // If current difference > tStart * peak difference, this is 
                                            // taken to be start of trend
                                            if( currDiff < aSett.TStartP * result.PeakDiff )
                                            {
                                                secondary = true;
                                            }
                                        }

                                        currShortEma = prevEmas[ shortPeriod ].GetRemoveNewest();
                                        currLongEma = prevEmas[ longPeriod ].GetRemoveNewest();
                                    }
                                }
                                else
                                {
                                    // If trend is still same as current,
                                    // move backwards to last opposite trend and find
                                    // peak or trough.
                                    if( result.Trend == false )
                                    {
                                        if( currShortEma < currLongEma )
                                        {

                                            currShortEma = prevEmas[ shortPeriod ].GetRemoveNewest();
                                            currLongEma = prevEmas[ longPeriod ].GetRemoveNewest();

                                            if( result.PrevPeakDiff == -1 )
                                            {
                                                continue;
                                            }
                                            else
                                            {
                                                break;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if( currShortEma >= currLongEma )
                                        {

                                            currShortEma = prevEmas[ shortPeriod ].GetRemoveNewest();
                                            currLongEma = prevEmas[ longPeriod ].GetRemoveNewest();

                                            if( result.PrevPeakDiff == -1 )
                                            {
                                                continue;
                                            }
                                            else
                                            {
                                                break;
                                            }
                                        }
                                    }

                                    currDiff = Math.Abs( currShortEma - currLongEma );

                                    if( result.PrevPeakDiff == -1 )
                                    {
                                        result.PrevPeakPrice = currShortEma.Price;
                                        result.PrevPeakDiff = currDiff;
                                        result.PrevPeakTime = currShortEma.Time;
                                    }
                                    else
                                    {
                                        if( result.PrevPeakDiff < currDiff )
                                        {
                                            result.PrevPeakPrice = currShortEma.Price;
                                            result.PrevPeakDiff = currDiff;
                                            result.PrevPeakTime = currShortEma.Time;
                                        }

                                        if( currDiff < aSett.TStartP * result.PrevPeakDiff )
                                        {
                                            //if( result.Trend == true )
                                            //{
                                            //    result.StartPrice = currShortEma.Price;
                                            //    result.Time = currShortEma.Time;
                                            //}
                                            //// Start price provided in od
                                            //else
                                            //{
                                            //    result.StartPrice = currLongEma.Price;
                                            //    result.Time = currLongEma.Time;
                                            //}

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
                                    // PrevEmas crossed
                                    result = null;
                                    DoubleEmaAnalyseProduct( aSett, null );
                                }
                            }
                            else
                            {
                                if( newestShortEma <= newestLongEma )
                                {
                                    // PrevEmas crossed
                                    result = null;
                                    DoubleEmaAnalyseProduct( aSett, null );
                                }
                            }
                        }
                        

                        if( result != null )
                        {
                            currDiff = Math.Abs( newestShortEma - newestLongEma );

                            // Update current result if new peak
                            if( currDiff > result.PeakDiff )
                            {
                                result.PeakDiff = currDiff;
                                result.PeakTime = newestShortEma.Time;
                            }

                            if( result.Trend == false )
                            {
                                // If difference decreased more than turnP + tooLate %, wait for another peak.
                                if( result.PeakDiff - currDiff > (result.PeakDiff * (aSett.BTurnP + aSett.BTooLateP)) )
                                {

                                }
                                // If difference decreased by turnP % suggest order
                                else if( result.PeakDiff - currDiff > (result.PeakDiff * aSett.BTurnP) )
                                {

                                    result.Price = currentFiveMinCandles[ product ].Avg;
                                    // Delete perchance
                                    result.Complete = true;
                                    writer.Write( $"Buy {product} at {result.Price} at {DateTime.UtcNow}" );
                                }
                            }
                            else
                            {

                                if( result.PeakDiff - currDiff > (result.PeakDiff * (aSett.STurnP + aSett.STooLateP)) )
                                {

                                }
                                //If difference decreased by turnP % suggest order
                                else if( result.PeakDiff - currDiff > (result.PeakDiff * aSett.STurnP) )
                                {
                                    // Suggested price
                                    result.Price = currentFiveMinCandles[ product ].Avg;
                                    //Console.WriteLine($"Sell {prel.ProductId} order at {prel.Price} ");

                                    // Delete perchance
                                    result.Complete = true;
                                    writer.Write( $"Sell {product} at {result.Price} at {DateTime.UtcNow}" );
                                }
                            }
                        }
                    }
                }


                return result;


            }
            catch( Exception e )
            {
                Console.WriteLine( e.StackTrace );
                Console.WriteLine( e.Message );
                return null;
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

                prevEmaSlopes[ shortPeriod ] = new LimitedDateTimeList<Ema>( hourEmaSlopes[ product ][ shortPeriod ], 300 );
                prevEmaSlopes[ longPeriod ] = new LimitedDateTimeList<Ema>( hourEmaSlopes[ product ][ longPeriod ], 300 );

                Ema newestShortEma = currEmas[ shortPeriod ];
                Ema newestLongEma = currEmas[ longPeriod ];
                Ema newestShortEmaSlope = currEmaSlopes[ shortPeriod ];
                Ema newestLongEmaSlope = currEmaSlopes[ longPeriod ];
                Ema currShortEma = currEmas[ shortPeriod ];
                Ema currLongEma = currEmas[ longPeriod ];
                Ema currShortEmaSlope = currEmaSlopes[ shortPeriod ];
                Ema currLongEmaSlope = currEmaSlopes[ longPeriod ];

                DoubleEmaAnalysisResult result;
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
                                result = new DoubleEmaAnalysisResult();
                                result.Trend = false;

                                result.PeakDiff = currDiff;
                                result.PeakTime = currShortEma.Time;
                            }
                            else if( currShortEma.Price > currLongEma.Price )
                            {
                                result = new DoubleEmaAnalysisResult();
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
                            // PrevEmas crossed
                            result = null;
                            results[ product ] = null;
                        }
                    }
                    else
                    {
                        if( newestShortEma <= newestLongEma )
                        {
                            // PrevEmas crossed
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

                                if( Math.Abs( currentFiveMinCandles[ product ].Avg - currentLongCandlesCopy[ product ].Avg ) < 0.005 * currentLongCandlesCopy[ product ].Avg )
                                {
                                    
                                }

                                PreOrder pre = new PreOrder( product,
                                                             result.Time,
                                                             true );
                                pre.PeakTime = result.PeakTime;
                                pre.Price = currentFiveMinCandles[ product ].Close;

                                PreliminaryComplete( pre, ref hourCandles, ref currentHourCandles, 1.0075 );
                            }

                        }
                    }
                    else
                    {

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
                                pre.Price = currentFiveMinCandles[ product ].Avg;

                                PreliminaryComplete( pre, ref hourCandles, ref currentHourCandles, 1.0075 );
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

                prevEmas[ shortPeriod ] = new LimitedDateTimeList<Ema>( fiveMinEmas[ product ][ shortPeriod ], 300 );
                prevEmas[ longPeriod ] = new LimitedDateTimeList<Ema>( fiveMinEmas[ product ][ longPeriod ], 300 );

                prevEmaSlopes[ shortPeriod ] = new LimitedDateTimeList<Ema>( fiveMinEmas[ product ][ shortPeriod ], 300 );
                prevEmaSlopes[ longPeriod ] = new LimitedDateTimeList<Ema>( fiveMinEmas[ product ][ longPeriod ], 300 );

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
                    for( int i = 0; i < fiveMinEmas[ product ][ longPeriod ].Count; i++ )
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

                    // Check current lastEma

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
                                pre.Price = currentFiveMinCandles[ product ].Avg;
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
                                pre.Price = currentFiveMinCandles[ product ].Avg;
                                //Console.WriteLine($"Sell {prel.ProductId} order at {prel.Price} ");
                                pre.Complete = true;
                            }
                        }
                    }

                    if( pre.Complete )
                    {
                        PreliminaryComplete( pre, ref fiveMinCandles, ref currentFiveMinCandles, 1.0025 );
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

                    if( currentHourCandles[ productId ].Close != newShortCandle.Close )
                    {
                        currentHourCandles[ productId ].Close = newShortCandle.Close;
                    }

                    if( currentHourCandles[ productId ].High < newShortCandle.High )
                    {
                        currentHourCandles[ productId ].High = newShortCandle.High;
                    }

                    if( currentHourCandles[ productId ].Low > newShortCandle.Low )
                    {
                        currentHourCandles[ productId ].Low = newShortCandle.Low;
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
        // && sum of 50 last lastEmaSlope ! <0

        // s sg if !in Abs(min +(30% of (max-min)))
        private async void ShortCandleUpdateEvent(Object source, ShortCandleUpdateEventArgs e)
        {
            try
            {
                await Task.Run(() =>
                {
                    currentFiveMinCandles[e.ProductId] = e.NewShortCandle;

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
                                currentHourCandles[ product ] = e.NewLongCandles[ product ];
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
        private readonly AnalyserConfiguration config;
        private DataHandler dataHandler;
        private Dictionary<string, Queue<Candle>> fiveMinCandles;
        private Dictionary<string, Queue<Candle>> hourCandles;
        private ConcurrentDictionary<string, ConcurrentStack<SlopeCandle>> shortSlopes;
        private ConcurrentDictionary<string, Candle> currentFiveMinCandles;
        private ConcurrentDictionary<string, Candle> currentHourCandles;
        private ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentStack<Ema>>> fiveMinEmas;
        private ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentStack<Ema>>> hourEmas;
        private ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentStack<Ema>>> fiveMinEmaSlopes;
        private ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentStack<Ema>>> hourEmaSlopes;
        private ConcurrentDictionary<string, bool> calculatedFiveMinEmas;
        private ConcurrentDictionary<string, bool> calculatedHourEmas;
        private ConcurrentDictionary<string, ProductInfo> productInfos;
        private ConcurrentDictionary<string, PreOrder> prelOs;
        private ConcurrentDictionary<string, DoubleEmaAnalysisResult> results;
        private ConcurrentDictionary<string, ConcurrentDictionary<string, DoubleEmaAnalysisResult>> doubleEmaResults;
        private ConcurrentDictionary<string, ConcurrentDictionary<string, string>> singleEmaResults;
        private ConcurrentDictionary<string, ConcurrentDictionary<string, string>> volatilityResults;
    }

    public class PreOrderReadyEventArgs
    {
        public PreOrder PreliminaryOrder { get; set; }
    }

    public class AnalyserConfiguration
    {
        public AnalyserConfiguration( int[] fiveMinDoubleEmaLengths,
                                      int[] hourDoubleEmaLengths,
                                      int   fiveMinSingleEmaLength,
                                      int   hourSingleEmaLength,
                                      int   fiveMinCandleLowerLimit,
                                      int   hourCandleLowerLimit
                                      )
        {
            FiveMinDoubleEmaLengths = fiveMinDoubleEmaLengths;
            HourDoubleEmaLengths = hourDoubleEmaLengths;
            FiveMinSingleEmaLength = fiveMinSingleEmaLength;
            HourSingleEmaLength = hourSingleEmaLength;
            FiveMinCandleLowerLimit = fiveMinCandleLowerLimit;
            HourCandleLowerLimit = hourCandleLowerLimit;
        }

        public int[] FiveMinDoubleEmaLengths { get; }
        public int[] HourDoubleEmaLengths { get; }
        public int FiveMinSingleEmaLength { get; }
        public int HourSingleEmaLength { get; }
        public int FiveMinCandleLowerLimit { get; }
        public int HourCandleLowerLimit { get; }
    }
}
