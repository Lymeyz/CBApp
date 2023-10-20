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

namespace CBApp1
{
    public class RequestMaker
    {
        public RequestMaker(ref Authenticator authenticator, string apiEndpoint)
        {
            auth = authenticator;
            this.apiEndpoint = apiEndpoint;
        }

        private Authenticator auth;
        string apiEndpoint;
        public RestResponse SendAuthRequest(string reqPath, Method method, string body)
        {

            try
            {
                RestClient client;
                RestRequest request;
                RestResponse resp = null;
                string[] headers;

                client = new RestClient( apiEndpoint + reqPath );
                request = new RestRequest( "", method );

                int counter = 0;

                for( int i = 0; i < 25; i++ )
                {
                    headers = auth.GenerateHeaders( auth.GetUnixTime(),
                                                        method.ToString(),
                                                        client.Options.BaseUrl.AbsolutePath,
                                                        body );

                    request.AddHeader( "accept", "application/json" );
                    request.AddHeader( "CB-ACCESS-KEY", headers[ 0 ] );
                    request.AddHeader( "CB-ACCESS-SIGN", headers[ 1 ] );
                    request.AddHeader( "CB-ACCESS-TIMESTAMP", headers[ 2 ] );

                    if( body != "" )
                    {
                        request.AddParameter( "application/json", body, ParameterType.RequestBody );
                    }

                    resp = client.Execute( request );

                    if( resp != null &&
                        resp.IsSuccessful )
                    {
                        if( i > 0 )
                        {
                            //Console.WriteLine( $"success on {i+1}" );
                        }
                        else
                        {
                            //Console.WriteLine( $"success on {i + 1}" );
                        }
                        break;
                    }
                    else
                    {
                        //Console.WriteLine( "reqFail" );
                        //Console.WriteLine( $"{resp.StatusDescription}" );
                    }

                    if( i == 24 )
                    {
                        Console.WriteLine( "OVER 24 FAILS" );
                        Thread.Sleep( 1000 );
                        i = 0;
                        counter++;
                    }

                    if( counter > 4 )
                    {
                        throw new Exception( "Request failed, 100 failed requests" );
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

        public RestResponse SendAuthQueryRequest(string reqPath, string query, Method method, string body)
        {
            try
            {
                RestClient client;
                RestRequest request;
                RestResponse resp = null;
                string[] headers;


                client = new RestClient( apiEndpoint + reqPath + query );
                request = new RestRequest( "", method );

                int counter = 0;

                for( int i = 0; i < 25; i++ )
                {
                    headers = auth.GenerateHeaders( auth.GetUnixTime(),
                                                        method.ToString(),
                                                        reqPath,
                                                        body );

                    request.AddHeader( "accept", "application/json" );
                    request.AddHeader( "CB-ACCESS-KEY", headers[ 0 ] );
                    request.AddHeader( "CB-ACCESS-SIGN", headers[ 1 ] );
                    request.AddHeader( "CB-ACCESS-TIMESTAMP", headers[ 2 ] );

                    if( body != "" )
                    {
                        request.AddParameter( "application/json", body, ParameterType.RequestBody );
                    }

                    resp = client.Execute( request );

                    if( resp != null && 
                        resp.IsSuccessful &&
                        resp.StatusCode != System.Net.HttpStatusCode.BadGateway )
                    {
                        break;
                    }

                    if( i == 24 )
                    {
                        Thread.Sleep( 500 );
                        i = 0;
                    }


                    counter++;
                    if( counter > 4 )
                    {
                        throw new Exception( "Request failed" );
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

        public RestResponse SendOrderRequest(string reqBody)
        {
            return SendAuthRequest("/orders", Method.Post, reqBody);
        }

        public RestResponse SendCancelRequest(string orderId, string profile_id)
        {
            int sentCount = 0;
            bool canceled = false;
            RestResponse cResp = null;

            while (!canceled)
            {
                cResp = SendAuthRequest("/orders/" + orderId + $"?profile_id={profile_id}", Method.Delete, "");
                sentCount++;

                if (sentCount == 5)
                {
                    Thread.Sleep(1000);
                    sentCount = 0;
                }

                if (cResp.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    canceled = true;
                }
                else if( cResp.StatusCode == System.Net.HttpStatusCode.NotFound )
                {
                    canceled = true;
                }
                else
                {
                    sentCount++;
                }
            }
            return cResp;
        }

        public RestResponse GetOrdersRequest(string queryString)
        {
            return SendAuthRequest("/orders?" + queryString, Method.Get, "");
        }

        public RestResponse SendFillsRequest(string queryString)
        {
            return SendAuthRequest("/fills?" + queryString, Method.Get, "");
        }

        public RestResponse GetOrderRequest(string orderId)
        {
            return SendAuthRequest($"/orders/{orderId}", Method.Get, "");
        }

        public RestResponse GetProfilesRequest()
        {
            return SendAuthRequest($"/profiles?active=true", Method.Get, "");
        }
        public RestResponse GetAccountsRequest()
        {
            return SendAuthRequest("/accounts", Method.Get, "");
        }

        public RestResponse SendRequest(string reqPath, Method method)
        {
            var client = new RestClient(apiEndpoint + reqPath);
            var request = new RestRequest( "", method );
            
            request.AddHeader("Accept", "application/json");

            return client.Execute(request);
        }

        private int GetUnixTime()
        {
            return (int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        }
    }
}
