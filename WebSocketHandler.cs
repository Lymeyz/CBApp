using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebSocketSharp;
using Newtonsoft.Json;
using RestSharp;

namespace CBApp1
{
    public class WebSocketHandler
    {
        private readonly object wsLock = new object();
        public WebSocketHandler(string url, Authenticator auth, string[] channelNames, string[] products)
        {
            this.url = url;
            this.auth = new Authenticator(auth);
            channels = channelNames;
            this.products = products;
            subscriptionStrings = new List<string>();
            MakeSubscriptionStrings( channelNames, products );

            MakeWebSocket();

            ws.OnClose += Ws_OnClose;
            ws.OnError += Ws_OnError;
        }

        public string Url
        {
            get
            {
                return url;
            }
        }
        public ref WebSocket Ws
        {
            get
            {
                return ref ws;
            }
        }

        private void MakeWebSocket()
        {
            try
            {
                ws = new WebSocket(url);
                ws.SslConfiguration.EnabledSslProtocols = System.Security.Authentication.SslProtocols.None;
            }
            catch (WebSocketException e)
            {
                Console.WriteLine("Error: " + e.Message);
            }
        }

        private string makeAuthSubMessage( string channelName, string[] products )
        {
            //auth.GenerateWsHeaders( );
            string[] headers = auth.GenerateWsHeaders( channelName, products );

            var subMessage = new
            {
                type = "subscribe",
                product_ids = products,
                channel = headers[ 0 ],
                api_key = headers[ 1 ],
                timestamp = headers[ 2 ],
                signature = headers[ 3 ]
            };

            //foreach( var product in products )
            //{
            //    subMessage.product_ids.Add( product );
            //}

            return JsonConvert.SerializeObject( subMessage );
        }

        //public bool TryConnectUserWebSocket()
        //{
        //    try
        //    {
        //        ws.Connect();

        //        ws.Send(makeAuthSubMessage( "user", new string[] { } ));

        //        return true;
        //    }
        //    catch( Exception e )
        //    {
        //        Console.WriteLine( e.StackTrace );
        //        Console.WriteLine( e.Message );
        //        return false;
        //    }
        //}

        public bool TryConnectWebSocket()
        {
            try
            {
                lock( wsLock )
                {
                    if( ws.ReadyState != WebSocketState.Open ||
                    ws.ReadyState != WebSocketState.Connecting )
                    {
                        ws.Connect();

                        MakeSubscriptionStrings( channels, products );

                        foreach( var subString in subscriptionStrings )
                        {
                            ws.Send( subString );
                        }

                        if( ws.ReadyState == WebSocketState.Open ||
                            ws.ReadyState == WebSocketState.Connecting )
                        {
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                    else
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
                return false;
            }
        }

        private void MakeSubscriptionStrings( string[] channelNames, string[] products )
        {
            subscriptionStrings = new List<string>();
            foreach( string channelName in channelNames )
            {
                subscriptionStrings.Add( makeAuthSubMessage( channelName, products ) );
            }
        }

        private void Ws_OnError(object sender, WebSocketSharp.ErrorEventArgs e)
        {
            try
            {
                lock( wsLock )
                {
                    int tryConnect = 0;
                    while( ws.ReadyState == WebSocketState.Closed ||
                            ws.ReadyState == WebSocketState.Closing )
                    {
                        Console.WriteLine( "Websocket Closed! " );
                        Console.WriteLine( "Trying reconnnect..." );

                        MakeSubscriptionStrings( channels, products );

                        if( TryConnectWebSocket() )
                        {
                            wsState = WebSocketState.Open;
                            break;
                        }
                        else
                        {
                            tryConnect++;
                            if( tryConnect > 500 )
                            {
                                throw new Exception( "Exceeding rate limits" );
                            }
                            continue;
                        }
                    }
                }
            }
            catch( Exception ex )
            {
                Console.WriteLine( ex.StackTrace );
                Console.WriteLine( ex.Message );
            }
        }
        private void Ws_OnClose(object sender, WebSocketSharp.CloseEventArgs e)
        {
            try
            {
                lock( wsLock )
                {
                    int tryConnect = 0;
                    while( ws.ReadyState == WebSocketState.Closed ||
                        ws.ReadyState == WebSocketState.Closing )
                    {
                        Console.WriteLine( "Websocket Closed! " + e.Reason );
                        Console.WriteLine( "Trying reconnnect..." );

                        MakeSubscriptionStrings( channels, products );

                        if( TryConnectWebSocket() )
                        {
                            wsState = WebSocketState.Open;
                            break;
                        }
                        else
                        {
                            tryConnect++;
                            if( tryConnect > 500 )
                            {
                                throw new Exception( "Exceeding rate limits" );
                            }
                            continue;
                        }
                    }
                }
            }
            catch( Exception ex)
            {
                Console.WriteLine( ex.StackTrace );
                Console.WriteLine( ex.Message );
            }
            
        }

        private string[] products;
        private string[] channels;
        private List<string> subscriptionStrings;
        private string url;
        private Authenticator auth;
        private WebSocket ws;
        private WebSocketState wsState;
    }
}
