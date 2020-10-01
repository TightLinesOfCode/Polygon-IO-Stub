using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarcoPolygonIO
{
    public static class myConstants
    {
        // Local TimeScale Database Credentials
        public static string SQLDatabaseUserName = "";
        public static string SQLDatabasePassword = "";
        public static string SQLDatabaseName = "";

        // Global File Path
        public static string LogFilePath = @"c:\Users\John\Desktop\Kaboom-Logs\KaboomLog.txt";    // Please change this to your preferred file location
        public static string OutputDirectory = "D:\\kaboom\\pg_historical_trades_intraday_prices\\";

        // Thread Safe API Counter
        public static int _value = 0;

    }
}
