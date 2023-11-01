using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RestSharp;
using RestSharp.Authenticators;
using System.Text.RegularExpressions;
using System.Runtime.Serialization;
using System.Threading;
using Newtonsoft.Json;
using System.Runtime.CompilerServices;
using System.Net.Http;
using System.Web;

namespace CBApp1
{
    public class RequestMaker
    {
        public RequestMaker(ref Authenticator authenticator, RequestLimiter limiter, string apiEndpoint)
        {
            auth = authenticator;
            this.limiter = limiter;
            this.apiEndpoint = apiEndpoint;
        }

        private Authenticator auth;
        private RequestLimiter limiter;
        string apiEndpoint;
        public async Task<string> SendAuthRequest(string reqPath, string queryParams, HttpMethod method, string body)
        {

            try
            {
                HttpResponseMessage resp = null;
                string returnString = null;

                string[] headers;
                //Console.WriteLine( "Sending request" );

                for( int i = 0; i < 100; i++ )
                {
                    headers = auth.GenerateHeaders( auth.GetUnixTime(),
                                                    method.ToString(),
                                                    '/' + reqPath,
                                                    body                );

                    HttpRequestMessage reqMessage = new HttpRequestMessage( method, apiEndpoint + reqPath + queryParams );
                    reqMessage.Headers.Add( "accept", "application/json"        );
                    reqMessage.Headers.Add( "CB-ACCESS-KEY", headers[ 0 ]       );
                    reqMessage.Headers.Add( "CB-ACCESS-SIGN", headers[ 1 ]      );
                    reqMessage.Headers.Add( "CB-ACCESS-TIMESTAMP", headers[ 2 ] );

                    if( body != "" )
                    {
                        reqMessage.Content = new StringContent( body, Encoding.UTF8, "application/json" );
                    }

                    resp = await limiter.ExecuteRequest( reqMessage );

                    if( resp != null &&
                        resp.IsSuccessStatusCode )
                    {
                        returnString = await resp.Content.ReadAsStringAsync();

                        break;
                    }
                    else
                    {
                        if( resp != null )
                        {
                            string respContent = await resp.Content.ReadAsStringAsync();
                            //Console.WriteLine( "reqFail" );
                        }
                        else
                        {
                            //Console.WriteLine( "reqFail" );
                        }
                        
                        //Console.WriteLine( $"{resp.StatusCode}" );
                    }

                    //if( counter > 30 )
                    //{
                    //    throw new Exception( "Request failed, 100 failed requests" );
                    //}
                }

                return returnString;
            }
            catch( Exception e )
            {
                Console.WriteLine( e.Message );
                Console.WriteLine( e.StackTrace );
                return null;
            }
        }

        private int GetUnixTime()
        {
            return (int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        }
    }
}
