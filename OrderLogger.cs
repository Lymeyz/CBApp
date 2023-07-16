using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CBApp1
{


    public class OrderLogger
    {
        private readonly object logRoot = new object();
        public OrderLogger(int days)
        {
            fileUnMatchedBuyOrders = new Dictionary<string, List<FillInfo>>();
            fileAssociatedIds = new Dictionary<string, string>();
            fileFills = new Dictionary<string, List<FillInfo>>();
            fileActiveOrders = new Dictionary<string, List<OrderInfo>>();
            this.days = days;
            buyCount = new Dictionary<string, int>();

            //CheckLogFiles();
        }
        private void CheckLogFiles()
        {
            try
            {
                logDir = Directory.GetCurrentDirectory() + @"\Logs\";

                unMatchedBuysFileName = "LoggedOpenBuys" + ".JSON";
                unMatchedBuysFilePath = Path.Combine(logDir, unMatchedBuysFileName );

                associatedFileName = "AssociatedOrderIds" + ".JSON";
                associatedFilePath = Path.Combine( logDir, associatedFileName );

                allFillsFileName = DateTime.UtcNow.ToString( "yyyy-MM-dd" ) + ".JSON";
                allFillsFilePath = Path.Combine( logDir + allFillsFileName );

                loggedActiveOrdersFileName = "ActiveOrders" + ".JSON";
                loggedActiveOrdersFilePath = Path.Combine( logDir + loggedActiveOrdersFileName );
                
                

                if (Directory.Exists(logDir))
                {
                    //check for daily log file
                    //if doesnt exist create new log file
                    //else open relevant one
                    //List<string> fileNames = new List<string>();
                    // file exists, read content
                    if (FileExists(unMatchedBuysFileName))
                    {
                        ReadUnmatchedOrders();
                        //ThrowOldOrders();
                        CountUnmatchedBuys();
                    }
                    else
                    {
                        sw = new StreamWriter(unMatchedBuysFilePath);
                        sw.Close();
                    }

                    if( FileExists( associatedFileName ) )
                    {
                        ReadAssociated();
                    }
                    else
                    {
                        sw = new StreamWriter( associatedFilePath );
                        sw.Close();
                    }
                    if( FileExists( allFillsFileName ) )
                    {
                        ReadFills();
                    }
                    else
                    {
                        sw = new StreamWriter( allFillsFilePath );
                        sw.Close();
                    }

                    if( FileExists( loggedActiveOrdersFileName ) )
                    {
                        ReadActiveOrders();
                    }
                    else
                    {
                        sw = new StreamWriter(loggedActiveOrdersFilePath );
                        sw.Close();
                    }
                }
                else
                {
                    //create log directory, create new log file
                    Directory.CreateDirectory(logDir);

                    sw = new StreamWriter(unMatchedBuysFilePath);
                    sw.Close();

                    sw = new StreamWriter(associatedFilePath);
                    sw.Close();

                    sw = new StreamWriter( allFillsFilePath );
                    sw.Close();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
            
        }

        private bool FileExists(string fileName)
        {
            try
            {
                return Directory.EnumerateFiles( logDir, "*.JSON" ).Where( n => n == logDir + fileName ).Any();
            }
            catch( Exception e)
            {
                Console.WriteLine( e.Message );
                Console.WriteLine( e.StackTrace );
                return false;
            }
        }

        public void CountUnmatchedBuys()
        {
            try
            {
                buyCount = new Dictionary<string, int>();

                int count;
                foreach (var product in fileUnMatchedBuyOrders.Keys)
                {
                    buyCount[ product] = 0;
                    count = fileUnMatchedBuyOrders[product].Count;

                    for (int i = 0; i < count; i++)
                    {
                        if (fileUnMatchedBuyOrders[product][i].Side == "buy")
                        {
                            buyCount[ product]++;
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

        private void ReadActiveOrders()
        {
            try
            {
                Dictionary<string, List<OrderInfo>> fileActiveOrders;
                sr = new StreamReader( loggedActiveOrdersFilePath );
                fileContent = sr.ReadToEnd();
                sr.Close();
                if( fileContent.Length > 0 )
                {
                    fileActiveOrders = JsonConvert.DeserializeObject<Dictionary<string, List<OrderInfo>>>(fileContent);
                    foreach( var product in fileActiveOrders.Keys )
                    {
                        this.fileActiveOrders[ product ] = new List<OrderInfo>( fileActiveOrders[ product ] );
                        
                    }
                }
            }
            catch( Exception e)
            {
                Console.WriteLine( e.Message );
                Console.WriteLine( e.StackTrace );
            }
        }

        private void ReadAssociated()
        {
            try
            {
                Dictionary<string, string> associated;
                sr = new StreamReader( associatedFilePath );
                fileContent = sr.ReadToEnd();
                sr.Close();

                if( fileContent.Length > 0 )
                {
                    associated = JsonConvert.DeserializeObject<Dictionary<string, string>>( fileContent );
                    foreach( var id in associated.Keys )
                    {
                        fileAssociatedIds[ id ] = associated[ id ];
                    }
                }
            }
            catch( Exception e )
            {
                Console.WriteLine( e.Message );
                Console.WriteLine( e.StackTrace );
            }
        }

        private void ReadFills()
        {
            try
            {
                Dictionary<string, List<FillInfo>> fileFills;
                sr = new StreamReader( allFillsFilePath );
                fileContent = sr.ReadToEnd();
                sr.Close();

                if( fileContent.Length > 0 )
                {
                    fileFills = JsonConvert.DeserializeObject<Dictionary<string, List<FillInfo>>>( fileContent );
                    foreach( var product in fileFills.Keys )
                    {
                        if( this.fileFills.ContainsKey( product ) )
                        {
                            foreach( var fill in fileFills[ product ] )
                            {
                                this.fileFills[ product ].Add( fill );
                            }
                        }
                        else
                        {
                            this.fileFills[ product ] = new List<FillInfo>( fileFills[ product ] );
                        }
                    }
                }
            }
            catch( Exception e )
            {
                Console.WriteLine( e.Message );
                Console.WriteLine( e.StackTrace );
            }
        }

        private void ReadUnmatchedOrders()
        {
            try
            {
                Dictionary<string, List<FillInfo>> fileOrders;
                sr = new StreamReader( unMatchedBuysFilePath );
                fileContent = sr.ReadToEnd();
                sr.Close();

                if( fileContent.Length > 0 )
                {
                    fileOrders = JsonConvert.DeserializeObject<Dictionary<string, List<FillInfo>>>( fileContent );
                    foreach( var product in fileOrders.Keys )
                    {
                        fileUnMatchedBuyOrders[ product ] = new List<FillInfo>( fileOrders[ product ] );
                    }
                }
            }
            catch( Exception e )
            {
                Console.WriteLine( e.Message );
                Console.WriteLine( e.StackTrace );
            }
        }

        private void ThrowOldOrders()
        {
            try
            {
                int count;
                foreach( var product in fileUnMatchedBuyOrders.Keys )
                {
                    count = fileUnMatchedBuyOrders[ product ].Count;
                    for( int i = 0; i < count; i++ )
                    {
                        if( fileUnMatchedBuyOrders[ product ][ i ].Time < DateTime.UtcNow.AddDays( -days ) )
                        {
                            fileUnMatchedBuyOrders[ product ].RemoveAt( i );
                            count--;
                        }
                    }
                }
            }
            catch( Exception e )
            {
                Console.WriteLine( e.Message );
                Console.WriteLine( e.StackTrace );
            }
        }

        public void LogAssociated(Dictionary<string, string> associatedIds)
        {
            try
            {
                if( associatedIds.Count != 0 )
                {
                    sw = new StreamWriter( associatedFilePath );
                    sw.Write( JsonConvert.SerializeObject( associatedIds, Formatting.Indented ) );
                    sw.Close();
                }
                else
                {
                    sw = new StreamWriter( associatedFilePath );
                    sw.Close();
                }
            }
            catch( Exception e )
            {
                Console.WriteLine( e.Message );
                Console.WriteLine( e.StackTrace );
            }
        }

        public void LogFilledOrder(FillInfo order)
        {
            try
            {
                if( fileFills.ContainsKey( order.Product_Id ) )
                {
                    fileFills[ order.Product_Id ].Add( order );
                    sw = new StreamWriter( allFillsFilePath );
                    sw.Write( JsonConvert.SerializeObject( fileFills, Formatting.Indented ) );
                    sw.Close();
                }
                else
                {
                    fileFills[ order.Product_Id ] = new List<FillInfo>();
                    fileFills[ order.Product_Id ].Add( order );
                    sw = new StreamWriter( allFillsFilePath );
                    sw.Write( JsonConvert.SerializeObject( fileFills, Formatting.Indented ) );
                    sw.Close();
                }
            }
            catch( Exception e )
            {
                Console.WriteLine( e.Message );
                Console.WriteLine( e.StackTrace );
            }
        }

        public async Task UpdateLogs( string productId, Dictionary<string, List<OrderInfo>> currActive, 
            Dictionary<string, List<FillInfo>> unMatchedBuys, Dictionary<string, string> currAssociatedIds, List<FillInfo>[] filledOrders)
        {
            try
            {
                await Task.Run(() =>
                {
                    lock(logRoot)
                    {
                        LogAssociated( currAssociatedIds );

                        LogActive( currActive );

                        LogUnmatchedBuyOrders( productId, unMatchedBuys );

                        LogFills(productId, filledOrders );
                    }
                });
            }
            catch( Exception e)
            {
                Console.WriteLine( e.Message );
                Console.WriteLine( e.StackTrace );
            }
        }

        private void LogUnmatchedBuyOrders( string productId, Dictionary<string, List<FillInfo>> unMatchedBuys )
        {
            try
            {
                sw = new StreamWriter( unMatchedBuysFilePath );
                sw.Write( JsonConvert.SerializeObject( unMatchedBuys, Formatting.Indented ) );
                sw.Close();
            }
            catch( Exception e)
            {
                Console.WriteLine( e.StackTrace );
                Console.WriteLine( e.Message );
            }

        }

        private void LogFills( string productId, List<FillInfo>[] filledOrders )
        {
            try
            {
                if( allFillsFileName.Split( '.' )[0] != DateTime.UtcNow.ToString("yyyy-MM-dd") )
                {
                    allFillsFileName = DateTime.UtcNow.ToString( "yyyy-MM-dd" ) + ".JSON";
                    allFillsFilePath = Path.Combine( logDir + allFillsFileName );
                    fileFills = new Dictionary<string, List<FillInfo>>();
                }

                foreach( var list in filledOrders )
                {
                    if( list != null )
                    {
                        if( !fileFills.ContainsKey( productId ) )
                        {
                            fileFills[ productId ] = new List<FillInfo>();
                        }

                        foreach( var fill in list )
                        {
                            fileFills[ productId ].Add( fill );
                        }
                    }
                }

                sw = new StreamWriter( allFillsFilePath );
                sw.Write( JsonConvert.SerializeObject( fileFills, Formatting.Indented ) );
                sw.Close();


            }
            catch( Exception e)
            {
                Console.WriteLine( e.Message );
                Console.WriteLine( e.StackTrace );
            }
        }

        public void LogActive( Dictionary<string, List<OrderInfo>> currProductActive )
        {
            try
            {
                sw = new StreamWriter( loggedActiveOrdersFilePath );
                sw.Write( JsonConvert.SerializeObject( currProductActive, Formatting.Indented ) );
                sw.Close();
            }
            catch( Exception e)
            {
                Console.WriteLine( e.StackTrace );
                Console.WriteLine( e.Message );
            }
        }

        // Properties

        public Dictionary<string, List<FillInfo>> FileUnMatchedBuyOrders
        {
            get
            {
                lock( logRoot )
                {
                    return fileUnMatchedBuyOrders;
                }
            }
        }
        public Dictionary<string, string> FileAssociatedIds
        {
            get
            {
                lock( logRoot )
                {
                    return fileAssociatedIds;
                }
            }
        }

        public Dictionary<string, List<FillInfo>> FileFills
        {
            get
            {
                lock( logRoot )
                {
                    return fileFills;
                }
            }
        }

        public Dictionary<string, List<OrderInfo>> FileActiveOrders
        {
            get
            {
                lock( logRoot )
                {
                    return fileActiveOrders;
                }
            }
        }

        // Fields
        private int days;
        private string logDir;
        private string unMatchedBuysFileName;
        private string allFillsFileName;
        private string associatedFileName;
        private string unMatchedBuysFilePath;
        private string allFillsFilePath;
        private string associatedFilePath;
        private string fileContent;
        private string loggedActiveOrdersFileName;
        private string loggedActiveOrdersFilePath;
        private StreamReader sr;
        private StreamWriter sw;

        //  Collections

        private Dictionary<string, List<FillInfo>> fileUnMatchedBuyOrders;
        private Dictionary<string, string> fileAssociatedIds;
        private Dictionary<string, int> buyCount;
        private Dictionary<string, List<FillInfo>> fileFills;
        private Dictionary<string, List<OrderInfo>> fileActiveOrders;

    }
}
