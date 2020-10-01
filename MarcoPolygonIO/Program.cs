using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

// 3rd Party
using Serilog;
using Serilog.Sinks.SystemConsole;
using Serilog.Sinks.File;

namespace MarcoPolygonIO
{
    class Program
    {

        //          1.	You will need to install several packages using the NuGet package manager.
        //              Serilog
        //              Serilog.Sinks.File
        //              Serilog.Sinks.Console
        //              ServiceStack
        //          2.	You will need to set your output directory and log file path in the Constants file
        //          3.	You will need to create a ticker list that is in the getMasterTickerList() method.   
        //          4.	You will also need to put your ALPACA API Key in the private static string apiKey = "XXXXXXXX";


        public static async Task Main(string[] args)
        {
            // Record execution time
            var watch = new System.Diagnostics.Stopwatch();
            watch.Start();

            // Initialize Logger
            string logFilePath = myConstants.LogFilePath;

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                //.MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.File(logFilePath, rollingInterval: RollingInterval.Day)
                .CreateLogger();

            // Start Boom Trader
            Log.Information("Starting");

            // TEST !!
            args = new string[] { "-UpdateHistoricalData" };
            //args = new string[] { "-VerifyHistoricalData" };

            // Checks to make sure there is a command line parameter
            if (args.Length > 0)
            {
                // Choose path of operation based on command line arguments
                switch (args[0])
                {
                    case "-UpdateHistoricalData":
                        Log.Information("RUNNING : Updating Historical Data");
                        await PolygonAPIHistoricalTrades.BackfillHistoricalTrades();
                        break;
                    case "-VerifyHistoricalData":
                        break;
                    case "-Automation":

                        break;
                    default:
                        Log.Information("Invalid command line arguments");
                        break;
                }
            }
            else
            {
                Log.Error("ERROR: Must include a command parameter");
                return;
            }

            // Program execution time
            watch.Stop();
            Log.Information($"Execution Time: {watch.ElapsedMilliseconds} ms");

            // Finished running
            Log.Information("Finished");
            //Log.CloseAndFlush();

            // Wait to close
            //if (!System.Diagnostics.Debugger.IsAttached) Console.ReadLine();
            Console.ReadLine();

            return;

        }
    }
}
