using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RestSharp;
using RestSharp.Authenticators;
using System.Text.RegularExpressions;
using WebSocketSharp;
using System.IO;
using Newtonsoft.Json;
using System.Timers;

namespace CBApp1
{
    class Program
    {

        public static Timer aTimer;
        static void Main(string[] args)
        {
            try
            {

                SynchronizedConsoleWriter writer = new SynchronizedConsoleWriter();

                //,"ETH-EUR", "ADA-EUR", "DOT-EUR", "XLM-EUR", "SOL-EUR"
                //string[] products = { "ETH-EUR" };
                string[] products =
                {
                    "ETH-USD",
                    "ADA-USD",
                    "LTC-USD",
                    "BTC-USD",
                    "SOL-USD",
                    "XLM-USD"
                };

                Authenticator auth = new Authenticator( "UskLbUTH3fKU6lKl",
                                                  "0faf3uv6rzi",
                                                  "6qzqQXei1fFZgE9w37tndH8Qi0MSwmTd" );

                RequestMaker reqMaker = new RequestMaker( ref auth, @"https://api.coinbase.com/" );

                DataHandler dataHandler = new DataHandler(ref aTimer, ref writer, ref auth, ref reqMaker, products);

                AnalyserConfiguration analyserConfig = new AnalyserConfiguration( new int[] { 6, 26 },
                                                                                  new int[] { 6, 12 },
                                                                                  46,
                                                                                  52,
                                                                                  200,
                                                                                  72,
                                                                                  new int[] { 8, 20 },
                                                                                  new int[] { 8, 24 },
                                                                                  4,
                                                                                  4
                                                                                  );

                DataAnalyser analyser = new DataAnalyser( ref dataHandler, ref aTimer, ref writer, analyserConfig);

                Dictionary<string, string> aliases = new Dictionary<string, string>() 
                {
                    { "ETH-USD", "ETH-USDC" },
                    { "ADA-USD", "ADA-USDC" },
                    { "BTC-USD", "BTC-USDC" },
                    { "SOL-USD", "SOL-USDC" },
                    { "XLM-USD", "XLM-USDC" },
                    { "LTC-USD", "LTC-USDC" }
                };

                OrderDirector director = new OrderDirector( ref analyser, ref writer, ref reqMaker, 30, 3 , ref aTimer, 0.015, 1.01010, products, aliases);

                //string input = "";
              
                //while( input != "z" )
                //{
                //    Console.Write( "Type order: " );
                //    input = Console.ReadLine();
                //    director.TestSendOrder2( input );
                //}

                Console.ReadKey();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
        }

        //private static void Ws_OnMessage(object sender, MessageEventArgs e)
        //{
        //    Console.WriteLine("Recieved: " + e.Data + "\n");
        //}

        private static void SetTimer()
        {
            aTimer = new Timer(1000);
            aTimer.AutoReset = true;
            aTimer.Enabled = true;
        }
    }
}
