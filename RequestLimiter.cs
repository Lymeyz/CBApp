using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace CBApp1
{
    public class RequestLimiter
    {
        private readonly object requestRoot = new object();
        private readonly object endRequestRoot = new object();
        private static System.Timers.Timer requestTimer;
        private int requestCount;
        private bool requesting;
        private static HttpClient hClient = new HttpClient();

        public RequestLimiter()
        {
            requestCount = 0;
            SetTimer( 1000 );
            hClient.Timeout = TimeSpan.FromSeconds(2);

            requestTimer.Elapsed += this.OnTimedEvent;
        }

        public async Task<HttpResponseMessage> ExecuteRequest( HttpRequestMessage hReq )
        {
            try
            {
                HttpResponseMessage resp = null;
                if( requestCount < 30 )
                {
                    resp = hClient.SendAsync( hReq ).GetAwaiter().GetResult();
                    //resp = SendReq( hReq );
                    if( resp != null )
                    {
                        string respString = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    }
                }
                else
                {
                    Thread.Sleep( 1000 );
                    resp = hClient.SendAsync( hReq ).GetAwaiter().GetResult();
                    //resp = SendReq( hReq );
                    if( resp != null )
                    {
                        string respString = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    }
                    
                }

                return resp;
            }
            catch( Exception e )
            {
                Console.WriteLine( e.Message );
                Console.WriteLine( e.StackTrace );
                return null;
            }
        }

        private HttpResponseMessage SendReq( HttpRequestMessage req )
        {
            try
            {
                lock( requestRoot )
                {
                    return hClient.SendAsync( req ).GetAwaiter().GetResult();
                }
            }
            catch( Exception e )
            {
                Console.WriteLine( e.Message );
                Console.WriteLine( e.StackTrace );
                return null;
            }
        }

        private void OnTimedEvent( Object source, ElapsedEventArgs e )
        {
            requestCount = 0;
            //Console.WriteLine( "reset req count" );
        }

        private static void SetTimer( int ms )
        {
            requestTimer = new System.Timers.Timer( ms );
            requestTimer.AutoReset = true;
            requestTimer.Enabled = true;
        }
    }
}
