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
            requestTimer.Elapsed += this.OnTimedEvent;
        }

        public async Task<HttpResponseMessage> ExecuteRequest( HttpRequestMessage hReq )
        {
            try
            {
                CancellationTokenSource cancelSource = new CancellationTokenSource( 3000 );
                CancellationToken cToken;
                HttpResponseMessage resp = null;
                int statusCheckCount = 0;
                if( requestCount < 29 )
                {
                    requestCount++;
                    //Console.WriteLine( $"Executing request {requestCount}" );

                    lock( requestRoot )
                    {
                        cToken = cancelSource.Token;

                        Task<HttpResponseMessage> task = hClient.SendAsync( hReq, cToken );

                        //HttpContent content = hTask.Content;
                        //var readAsString = content.ReadAsStringAsync();

                        //Task<RestResponse> task = client.ExecuteAsync( request, cToken );


                        while( task.Status == TaskStatus.Running ||
                               task.Status == TaskStatus.WaitingToRun ||
                               task.Status == TaskStatus.WaitingForActivation )
                        {
                            Thread.Sleep( 25 );
                            statusCheckCount++;

                            if( statusCheckCount > 80 )
                            {
                                //Console.WriteLine( "Request stuck" );
                                break;
                            }
                        }

                        if( task.IsCompleted && !task.IsFaulted )
                        {
                            resp = task.Result;
                        }
                        else
                        {
                            resp = null;
                        }
                    }
                }
                else
                {
                    Thread.Sleep( 1000 );
                    requestCount++;
                    //Console.WriteLine( $"Sending request {requestCount}" );
                    lock( requestRoot )
                    {
                        cToken = cancelSource.Token;
                        Task<HttpResponseMessage> task = hClient.SendAsync( hReq, cToken );

                        while( task.Status == TaskStatus.Running ||
                               task.Status == TaskStatus.WaitingToRun ||
                               task.Status == TaskStatus.WaitingForActivation )
                        {
                            Thread.Sleep( 25 );
                            statusCheckCount++;

                            if( statusCheckCount > 80 )
                            {
                                //Console.WriteLine( "Request stuck" );
                                break;
                            }
                        }

                        if( task.IsCompleted && !task.IsFaulted )
                        {
                            resp = task.Result;
                        }
                        else
                        {
                            resp = null;
                        }
                    }
                }


                if( cToken.IsCancellationRequested )
                {
                    Console.WriteLine( $"Request was cancelled" );
                }
                else
                {
                    //Console.WriteLine( $"Recieved response" );
                }

                requesting = false;
                return resp;
            }
            catch( Exception e )
            {
                Console.WriteLine( e.Message );
                Console.WriteLine( e.StackTrace );
                requesting = false;
                return null;
            }
        }

        private void OnTimedEvent( Object source, ElapsedEventArgs e )
        {
            requestCount = 0;
        }

        private static void SetTimer( int ms )
        {
            requestTimer = new System.Timers.Timer( ms );
            requestTimer.AutoReset = true;
            requestTimer.Enabled = true;
        }
    }
}
