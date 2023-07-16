using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CBApp1
{
    
    public class SynchronizedConsoleWriter
    {
        private readonly object printRoot = new object();
        public SynchronizedConsoleWriter()
        {
            logDir = Directory.GetCurrentDirectory() + @"\Logs\";
            consoleLogFileName = "ConsoleLog" +
                $"{DateTime.UtcNow.ToString( "yyyy-MM-dd" )}";
            consoleLogFilePath = Path.Combine( logDir + consoleLogFileName );

            if(  true )
            {

            }

        }

        public void Write(string text)
        {
            lock (printRoot)
            {
                Console.WriteLine(text);
            }
        }

        private void Log(string text)
        {
            if( consoleLogFileName == "ConsoleLog" +
                $"{DateTime.UtcNow.ToString( "yyyy-MM-dd" )}" )
            {
                
            }
            else
            {
                consoleLogFileName = "ConsoleLog" +
                $"{DateTime.UtcNow.ToString( "yyyy-MM-dd" )}";


            }
        }

        private bool FileExists(string fileName)
        {
            try
            {
                return true;
            }
            catch( Exception e)
            {
                Console.WriteLine( e.StackTrace );
                Console.WriteLine( e.Message );
                return false;
            }
        }

        private string logDir;
        private string consoleLogFileName;
        private string consoleLogFilePath;
    }
}
