using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CBApp1
{
    public class AsyncOrderLogger
    {
        private readonly object writerRoot = new object();
        private readonly object readerRoot = new object();
        public AsyncOrderLogger()
        {
            CheckLogFiles();

            // active orders test
            //ConcurrentDictionary<string, ConcurrentDictionary<string, OrderInfo>> testActiveOrders =
            //    new ConcurrentDictionary<string, ConcurrentDictionary<string, OrderInfo>>();
            //testActiveOrders[ "ETH-EUR" ] = new ConcurrentDictionary<string, OrderInfo>();
            //testActiveOrders[ "ETH-EUR" ][ "aaaa" ] = new OrderInfo( "aaaa",
            //                                                        "aaaa",
            //                                                        new OrderConfiguration( new LimitGtc( "0.01", "0.01", true ) ),
            //                                                        "BUY",
            //                                                        "OPEN",
            //                                                        DateTime.Now.ToString(),
            //                                                        "0.01",
            //                                                        "0.01",
            //                                                        "aaaa" );

            //LogActiveOrders( testActiveOrders );
            //ReadActiveOrders();

            // pending orders test
            //ConcurrentDictionary<string, OrderInfo> pendings = new ConcurrentDictionary<string, OrderInfo>();
            //pendings[ "OOOO" ] = new OrderInfo( "aaaa",
            //                                                        "aaaa",
            //                                                        new OrderConfiguration( new LimitGtc( "0.01", "0.01", true ) ),
            //                                                        "BUY",
            //                                                        "OPEN",
            //                                                        DateTime.Now.ToString(),
            //                                                        "0.01",
            //                                                        "0.01",
            //                                                        "aaaa" );
            //pendings[ "OOOO" ].AssociatedId = "EEEEE";
            //LogPendingOrders( pendings );
            //ReadPendingOrders();
            // unmatched orders test
            //ConcurrentDictionary<string, ConcurrentDictionary<string, OrderInfo>> testunMatchedOrders =
            //    new ConcurrentDictionary<string, ConcurrentDictionary<string, OrderInfo>>();
            //testunMatchedOrders[ "ETH-EUR" ] = new ConcurrentDictionary<string, OrderInfo>();
            //testunMatchedOrders[ "ETH-EUR" ][ "aaaa" ] = new OrderInfo( "aaaa",
            //                                                        "aaaa",
            //                                                        new OrderConfiguration( new LimitGtc( "0.01", "0.01", true ) ),
            //                                                        "BUY",
            //                                                        "OPEN",
            //                                                        DateTime.Now.ToString(),
            //                                                        "0.01",
            //                                                        "0.01",
            //                                                        "aaaa" );

            //LogUnMatchedOrders( testunMatchedOrders );
            //ReadUnMatchedOrders();
            // filled orders test
            //OrderInfo order1 = new OrderInfo( "aaaa",
            //                    "ADA-EUR",
            //                    new OrderConfiguration( new LimitGtc( "0.01", "0.01", true ) ),
            //                    "BUY",
            //                    "OPEN",
            //                    DateTime.Now.ToString(),
            //                    "0.01",
            //                    "0.01",
            //                    "aaaa" );

            //OrderInfo order2 = new OrderInfo( "bbbb",
            //                    "ETH-EUR",
            //                    new OrderConfiguration( new LimitGtc( "0.01", "0.01", true ) ),
            //                    "BUY",
            //                    "OPEN",
            //                    DateTime.Now.ToString(),
            //                    "0.01",
            //                    "0.01",
            //                    "aaaa" );

            //LogFilledOrder( order1 );
            //LogFilledOrder( order2 );

        }

        private void CheckLogFiles()
        {
            try
            {
                logDir = Directory.GetCurrentDirectory() + @"\Logs\";

                activeOrdersFileName = "LoggedActive" + ".JSON";
                ActiveOrdersFilePath = Path.Combine( logDir, activeOrdersFileName );

                pendingOrdersFileName = "LoggedPending" + ".JSON";
                pendingOrdersFilePath = Path.Combine( logDir, pendingOrdersFileName );

                unMatchedOrdersFileName = "LoggedUnMatched" + ".JSON";
                unMatchedOrdersFilePath = Path.Combine( logDir, unMatchedOrdersFileName );

                filledOrdersDay = DateTime.UtcNow.ToString( "yyyy-MM-dd" );
                filledOrdersFileName = $"Filled{filledOrdersDay}.JSON";
                filledOrdersFilePath = Path.Combine( logDir, filledOrdersFileName );

                prevDaysFilledOrdersDay = DateTime.UtcNow.AddDays( -1 ).ToString( "yyyy-MM-dd" );
                prevDaysFilledOrdersFileName = $"Filled{prevDaysFilledOrdersDay}.JSON";
                prevDaysFilledOrdersFilePath = Path.Combine( logDir, prevDaysFilledOrdersFileName );

                associatedOrdersFileName = "LoggedAssociated" + ".JSON";
                associatedOrdersFilePath = Path.Combine( logDir, associatedOrdersFileName );

                trackerOutputFileName = "TrackerOutput" + ".JSON";
                trackerOutputFilePath = Path.Combine( logDir, trackerOutputFileName );

                pendingAssociatedFileName = "PendingAssociated" + ".JSON";
                pendingAssociatedFilePath = Path.Combine( logDir, pendingAssociatedFileName);


                if( Directory.Exists( logDir ) )
                {

                    // active orders
                    if( File.Exists( ActiveOrdersFilePath )  )
                    {
                        ReadActiveOrders();
                    }
                    else
                    {
                        sw = new StreamWriter( ActiveOrdersFilePath );
                        sw.Close();
                    }

                    // pending orders
                    if( File.Exists( pendingOrdersFilePath ) )
                    {
                        ReadPendingOrders();
                    }
                    else
                    {
                        sw = new StreamWriter( pendingOrdersFilePath );
                        sw.Close();
                    }

                    // unmatched orders
                    if( File.Exists( unMatchedOrdersFilePath ) )
                    {
                        ReadUnMatchedOrders();
                    }
                    else
                    {
                        sw = new StreamWriter( unMatchedOrdersFilePath );
                        sw.Close();
                    }

                    // filled orders
                    if( File.Exists( filledOrdersFilePath ) )
                    {
                        ReadFilledOrders();
                    }
                    else
                    {
                        sw = new StreamWriter( filledOrdersFilePath );
                        sw.Close();
                    }

                    // ydays filled orders
                    if( File.Exists( prevDaysFilledOrdersFilePath ) )
                    {
                        ReadPrevDaysFilledOrders();
                    }
                    else
                    {
                        for( int i = 0; i < 10; i++ )
                        {
                            prevDaysFilledOrdersDay = DateTime.UtcNow.AddDays( ( -i ) - 1 ).ToString( "yyyy-MM-dd" );
                            prevDaysFilledOrdersFileName = $"Filled{prevDaysFilledOrdersDay}.JSON";
                            prevDaysFilledOrdersFilePath = Path.Combine( logDir, prevDaysFilledOrdersFileName );

                            if( File.Exists( prevDaysFilledOrdersFilePath ) )
                            {
                                ReadPrevDaysFilledOrders();
                                break;
                            }
                        }
                    }

                    // associated orders
                    if( File.Exists ( associatedOrdersFilePath ) )
                    {
                        ReadAssociatedOrders();
                    }
                    else
                    {
                        sw = new StreamWriter( associatedOrdersFilePath );
                        sw.Close();
                    }

                    sw = new StreamWriter( trackerOutputFilePath );
                    sw.Close();

                    if( File.Exists ( pendingAssociatedFilePath ) )
                    {
                        ReadPendingAssociated();
                    }
                    else
                    {
                        sw = new StreamWriter( pendingAssociatedFilePath );
                        sw.Close();
                    }

                }
                else
                {
                    Directory.CreateDirectory( logDir );

                    sw = new StreamWriter( ActiveOrdersFilePath );
                    sw.Close();

                    sw = new StreamWriter( pendingOrdersFilePath );
                    sw.Close();

                    sw = new StreamWriter( unMatchedOrdersFilePath );
                    sw.Close();

                    sw = new StreamWriter( filledOrdersFilePath );
                    sw.Close();

                    sw = new StreamWriter( associatedOrdersFilePath );
                    sw.Close();

                    sw = new StreamWriter( trackerOutputFilePath );
                    sw.Close();

                    sw = new StreamWriter( pendingAssociatedFilePath );
                    sw.Close();
                }


            }
            catch( Exception e )
            {
                Console.WriteLine( e.StackTrace );
                Console.WriteLine( e.Message );
            }
        }

        public async Task LogPendingAssociated( ConcurrentDictionary<string, string> pendingAssociated)
        {
            try
            {
                lock( writerRoot )
                {
                    sw = new StreamWriter( pendingAssociatedFilePath );
                    sw.Write( JsonConvert.SerializeObject( pendingAssociated, Formatting.Indented ) );
                    sw.Close();
                }
            }
            catch( Exception e )
            {
                Console.WriteLine( e.StackTrace );
                Console.WriteLine( e.Message );
            }
        }
        private void ReadPendingAssociated()
        {
            try
            {
                sr = new StreamReader( pendingAssociatedFilePath );
                string fileContent = sr.ReadToEnd();
                sr.Close();

                if( fileContent.Length > 0 )
                {
                    pendingAssociated = JsonConvert.DeserializeObject<ConcurrentDictionary<string, string>>( fileContent );
                }
            }
            catch( Exception e )
            {
                Console.WriteLine( e.StackTrace );
                Console.WriteLine( e.Message );
            }
        }

        public async Task LogTrackerOutput(string output)
        {
            if( trackerOutput == null )
            {
                trackerOutput = new ConcurrentQueue<string>();
                trackerOutput.Enqueue( $"{DateTime.UtcNow.ToString( "HH-mm-ss" )} {output}" );

                lock( writerRoot )
                {
                    sw = new StreamWriter( trackerOutputFilePath );
                    sw.Write( JsonConvert.SerializeObject( trackerOutput, Formatting.Indented ) );
                    sw.Close();
                }
            }
            else
            {
                string throwAway;

                while( trackerOutput.Count > 1500 )
                {
                    trackerOutput.TryDequeue( out throwAway );
                }

                trackerOutput.Enqueue( $"{DateTime.UtcNow.ToString( "HH-mm-ss" )} {output}" );

                lock( writerRoot )
                {
                    sw = new StreamWriter( trackerOutputFilePath );
                    sw.Write( JsonConvert.SerializeObject( trackerOutput, Formatting.Indented ) );
                    sw.Close();
                }
            }
        }

        public async Task LogAssociatedOrders(ConcurrentDictionary<string, string> associatedOrders)
        {
            try
            {
                lock( writerRoot )
                {
                    sw = new StreamWriter( associatedOrdersFilePath );
                    sw.Write( JsonConvert.SerializeObject( associatedOrders, Formatting.Indented ) );
                    sw.Close();
                }
            }
            catch( Exception e )
            {
                Console.WriteLine( e.StackTrace );
                Console.WriteLine( e.Message );
            }
        }

        private void ReadAssociatedOrders()
        {
            try
            {
                sr = new StreamReader( associatedOrdersFilePath );
                string fileContent = sr.ReadToEnd();
                sr.Close();

                if( fileContent.Length > 0 )
                {
                    ConcurrentDictionary<string, string> fileAssociated =
                        JsonConvert.DeserializeObject<ConcurrentDictionary<string, string>>( fileContent );

                    fileAssociatedOrders = new ConcurrentDictionary<string, string>();

                    foreach( var pair in fileAssociated )
                    {
                        fileAssociatedOrders[ pair.Key ] = pair.Value;
                    }
                }
            }
            catch( Exception e )
            {
                Console.WriteLine( e.StackTrace );
                Console.WriteLine( e.Message );
            }
        }

        private void ReadFilledOrders()
        {
            try
            {
                sr = new StreamReader( filledOrdersFilePath );
                string fileContent = sr.ReadToEnd();
                sr.Close();

                fileFilledOrders = new ConcurrentDictionary<string, ConcurrentQueue<OrderInfo>>();

                if( fileContent.Length > 0 )
                {
                    ConcurrentDictionary<string, ConcurrentQueue<FileOrderInfo>> fileFilled =
                        JsonConvert.DeserializeObject<ConcurrentDictionary<string, ConcurrentQueue<FileOrderInfo>>> (fileContent);

                    foreach( var pair in fileFilled )
                    {
                        fileFilledOrders[ pair.Key ] = new ConcurrentQueue<OrderInfo>();
                        foreach( var order in pair.Value )
                        {
                            OrderInfo fileOrder = new OrderInfo( order );
                            if( order.FillTradeIds != null )
                            {
                                fileOrder.FillTradeIds = new List<string>( order.FillTradeIds );
                            }

                            fileFilledOrders[ pair.Key ].Enqueue( fileOrder );
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

        public async Task LogFilledOrder(OrderInfo filledOrder)
        {
            try
            {
                lock( writerRoot )
                {
                    if( DateTime.UtcNow.ToString( "yyyy-MM-dd" ) != filledOrdersDay )
                    {
                        filledOrdersDay = DateTime.UtcNow.ToString( "yyyy-MM-dd" );
                        filledOrdersFileName = $"Filled{DateTime.UtcNow.ToString( "yyyy-MM-dd" )}.Json";
                        filledOrdersFilePath = Path.Combine( logDir + filledOrdersFileName );

                        prevDaysFilledOrdersDay = DateTime.UtcNow.AddDays( -1 ).ToString( "yyyy-MM-dd" );
                        prevDaysFilledOrdersFileName = $"Filled{prevDaysFilledOrdersDay}.JSON";
                        prevDaysFilledOrdersFilePath = Path.Combine( logDir, prevDaysFilledOrdersFileName );



                        sw = new StreamWriter( filledOrdersFilePath );
                        sw.Close();

                        prevDaysFilledOrders = new ConcurrentDictionary<string, ConcurrentQueue<OrderInfo>>();
                        fileFilledOrders = new ConcurrentDictionary<string, ConcurrentQueue<OrderInfo>>();
                        
                    }

                    if( fileFilledOrders == null )
                    {
                        fileFilledOrders = new ConcurrentDictionary<string, ConcurrentQueue<OrderInfo>>();
                    }

                    if( !fileFilledOrders.ContainsKey( filledOrder.ProductId ) )
                    {
                        fileFilledOrders[ filledOrder.ProductId ] = new ConcurrentQueue<OrderInfo>();
                    }

                    fileFilledOrders[ filledOrder.ProductId ].Enqueue( filledOrder );

                    sw = new StreamWriter( filledOrdersFilePath );
                    sw.Write( JsonConvert.SerializeObject( fileFilledOrders, Formatting.Indented ) );
                    sw.Close();
                }
            }
            catch( Exception e )
            {
                Console.WriteLine( e.StackTrace );
                Console.WriteLine( e.Message );
            }
        }

        private void ReadPrevDaysFilledOrders()
        {
            try
            {
                sr = new StreamReader( prevDaysFilledOrdersFilePath );
                string fileContent = sr.ReadToEnd();
                sr.Close();

                prevDaysFilledOrders = new ConcurrentDictionary<string, ConcurrentQueue<OrderInfo>>();

                if( fileContent.Length > 0 )
                {
                    ConcurrentDictionary<string, ConcurrentQueue<FileOrderInfo>> filePrevDaysFilled =
                        JsonConvert.DeserializeObject<ConcurrentDictionary<string, ConcurrentQueue<FileOrderInfo>>>( fileContent );

                    foreach( var pair in filePrevDaysFilled )
                    {
                        prevDaysFilledOrders[ pair.Key ] = new ConcurrentQueue<OrderInfo>();
                        foreach( var order in pair.Value )
                        {
                            OrderInfo fileOrder = new OrderInfo( order );
                            if( order.FillTradeIds != null )
                            {
                                fileOrder.FillTradeIds = new List<string>(order.FillTradeIds);
                            }

                            prevDaysFilledOrders[ pair.Key ].Enqueue( fileOrder );
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

        private void ReadUnMatchedOrders()
        {
            try
            {
                sr = new StreamReader( unMatchedOrdersFilePath );
                string fileContent = sr.ReadToEnd();
                sr.Close();

                if( fileContent.Length > 0 )
                {
                    ConcurrentDictionary<string, ConcurrentDictionary<string, FileOrderInfo>> fileUnMatched =
                        JsonConvert.DeserializeObject<ConcurrentDictionary<string, ConcurrentDictionary<string, FileOrderInfo>>>( fileContent );

                    fileUnMatchedOrders = new ConcurrentDictionary<string, ConcurrentDictionary<string, OrderInfo>>();

                    foreach( var pair in fileUnMatched )
                    {
                        fileUnMatchedOrders[ pair.Key ] = new ConcurrentDictionary<string, OrderInfo>();
                        foreach( var innerPair in pair.Value )
                        {
                            fileUnMatchedOrders[ pair.Key ][ innerPair.Key ] = new OrderInfo( innerPair.Value );
                        }
                    }
                }
            }
            catch( Exception e)
            {
                Console.WriteLine( e.StackTrace );
                Console.WriteLine( e.Message );
            }
        }

        public async Task LogUnMatchedOrders( ConcurrentDictionary<string, ConcurrentDictionary<string, OrderInfo>> unMatchedOrders )
        {
            try
            {
                lock( writerRoot )
                {
                    sw = new StreamWriter( unMatchedOrdersFilePath );
                    sw.Write( JsonConvert.SerializeObject( unMatchedOrders, Formatting.Indented ) );
                    sw.Close();
                }
            }
            catch( Exception e)
            {
                Console.WriteLine( e.StackTrace );
                Console.WriteLine( e.Message );
            }
        }

        private void ReadPendingOrders()
        {
            try
            {
                sr = new StreamReader( pendingOrdersFilePath );
                string fileContent = sr.ReadToEnd();
                sr.Close();

                if( fileContent.Length > 0 )
                {
                    ConcurrentDictionary<string, FileOrderInfo> filePendings =
                        JsonConvert.DeserializeObject <ConcurrentDictionary<string, FileOrderInfo>>( fileContent );
                    filePendingOrders = new ConcurrentDictionary<string, OrderInfo>();

                    foreach( var pair in filePendings )
                    {
                        filePendingOrders[ pair.Key ] = new OrderInfo(pair.Value);
                    }
                }
            }
            catch( Exception e )
            {
                Console.WriteLine( e.StackTrace );
                Console.WriteLine( e.Message );
            }
        }

        public async Task LogPendingOrders(ConcurrentDictionary<string, OrderInfo> pendingOrders)
        {
            try
            {
                lock( writerRoot )
                {
                    sw = new StreamWriter( pendingOrdersFilePath );
                    sw.Write( JsonConvert.SerializeObject( pendingOrders, Formatting.Indented ) );
                    sw.Close();
                }
            }
            catch( Exception e )
            {
                Console.WriteLine( e.StackTrace );
                Console.WriteLine( e.Message );
            }
        }

        private void ReadActiveOrders()
        {
            try
            {
                sr = new StreamReader( ActiveOrdersFilePath );
                string fileContent = sr.ReadToEnd();
                sr.Close();

                if( fileContent.Length > 0 )
                {
                    ConcurrentDictionary<string, ConcurrentDictionary<string, FileOrderInfo>> fileActives = 
                        JsonConvert.DeserializeObject<ConcurrentDictionary<string, ConcurrentDictionary<string, FileOrderInfo>>>(fileContent);

                    fileActiveOrders = new ConcurrentDictionary<string, ConcurrentDictionary<string, OrderInfo>>();

                    foreach( var pair in fileActives )
                    {
                        fileActiveOrders[ pair.Key ] = new ConcurrentDictionary<string, OrderInfo>();
                        foreach( var innerpair in pair.Value )
                        {
                            fileActiveOrders[ pair.Key ][ innerpair.Key ] = new OrderInfo( innerpair.Value );
                        }
                    }
                }
            }
            catch( Exception e)
            {
                Console.WriteLine( e.StackTrace );
                Console.WriteLine( e.Message );
            }
        }

        public async Task LogActiveOrders(ConcurrentDictionary<string, ConcurrentDictionary<string, OrderInfo>> activeOrders )
        {
            try
            {
                lock( writerRoot )
                {
                    sw = new StreamWriter( ActiveOrdersFilePath );
                    sw.Write( JsonConvert.SerializeObject( activeOrders, Formatting.Indented ) );
                    sw.Close();
                }
            }
            catch( Exception e )
            {
                Console.WriteLine( e.StackTrace );
                Console.WriteLine( e.Message );
            }
        }

        StreamWriter sw;
        StreamReader sr;
        string logDir;


        // Active orders
        public ConcurrentDictionary<string, ConcurrentDictionary<string, OrderInfo>> FileActiveOrders
        {
            get
            {
                return fileActiveOrders;
            }
        }
        private ConcurrentDictionary<string, ConcurrentDictionary<string, OrderInfo>> fileActiveOrders;
        private string activeOrdersFileName;
        private string ActiveOrdersFilePath;

        // Pending orders
        public ConcurrentDictionary<string, OrderInfo> FilePendingOrders
        {
            get
            {
                return filePendingOrders;
            }
        }
        private ConcurrentDictionary<string, OrderInfo> filePendingOrders;
        private string pendingOrdersFileName;
        private string pendingOrdersFilePath;

        // Unmatched orders
        public ConcurrentDictionary<string, ConcurrentDictionary<string, OrderInfo>> FileUnMatchedOrders
        {
            get
            {
                return fileUnMatchedOrders;
            }
        }
        private ConcurrentDictionary<string, ConcurrentDictionary<string, OrderInfo>> fileUnMatchedOrders;
        private string unMatchedOrdersFileName;
        private string unMatchedOrdersFilePath;

        // Associated orders
        public ConcurrentDictionary<string, string> FileAssociatedOrders
        {
            get
            {
                return fileAssociatedOrders;
            }
        }
        private ConcurrentDictionary<string, string> fileAssociatedOrders;
        private string associatedOrdersFileName;
        private string associatedOrdersFilePath;

        // Filled orders
        public ConcurrentDictionary<string, ConcurrentQueue<OrderInfo>> FileFilledOrders
        {
            get
            {
                return fileFilledOrders;
            }
        }
        private ConcurrentDictionary<string, ConcurrentQueue<OrderInfo>> fileFilledOrders;
        private string filledOrdersFileName;
        private string filledOrdersFilePath;
        private string filledOrdersDay;

        public ConcurrentDictionary<string, ConcurrentQueue<OrderInfo>> YesterdaysFileFilledOrders
        {
            get
            {
                return prevDaysFilledOrders;
            }
        }
        private ConcurrentDictionary<string, ConcurrentQueue<OrderInfo>> prevDaysFilledOrders;
        private string prevDaysFilledOrdersFileName;
        private string prevDaysFilledOrdersFilePath;
        private string prevDaysFilledOrdersDay;

        private ConcurrentQueue<string> trackerOutput;
        private string trackerOutputFileName;
        private string trackerOutputFilePath;

        public ConcurrentDictionary<string, string> FilePendingAssociated
        {
            get
            {
                return pendingAssociated;
            }
        }
        private ConcurrentDictionary<string, string> pendingAssociated;
        private string pendingAssociatedFileName;
        private string pendingAssociatedFilePath;
    }
}
