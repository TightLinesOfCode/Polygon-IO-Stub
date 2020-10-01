using System;
using System.Collections.Generic;
//using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;


// 3rd Party
using Serilog;
using Serilog.Sinks.File;
using Serilog.Sinks.SystemConsole;
using ServiceStack;

namespace MarcoPolygonIO
{

    public class PolygonAPIHistoricalTrades
    {
        //  TODO:  This should run in automated mode every evening
        //  /v2/ticks/stocks/nbbo/{ticker}/{date}
        //  https://api.polygon.io/v2/ticks/stocks/trades/AAPL/2018-02-02?limit=10&apiKey=XXXXXXXXXXXXXXX

        // Parameters for throttling the API
        private static int numberOfThreadsPerConcurrentRequests = 64;   // ( This is the number of symbols that will happen at once )
        private static int actionsPerMinute = 60;   //   This is the delay for recursion and after each action and also after the throttler send the concurrent threads
        private static int delayBetweenConcurrentRequests = (60 / (actionsPerMinute) * 1000); // Milliseconds


        FIX THE CORRECT API KEY HERE

        // API call parameters
        private static string apiKey = "MARCO-ALPACA-APIKEY";    // ALPACA MARKETS API
        private static HttpClient Client = new HttpClient();
        private static string baseURL = "https://api.polygon.io/v2/ticks/stocks/trades/";   //      /v2/ticks/stocks/trades/{ticker}/{date}
        private static int resultsLimit = 25000;    // This can be adjusted lower to make things work nice

        // Main processing of the API, iterates through parameters and sends to the task for processing
        public static async Task BackfillHistoricalTrades()
        {
            ServicePointManager.DefaultConnectionLimit = 16;
            Console.WriteLine(ServicePointManager.DefaultConnectionLimit + " Connections allowed");

            Client.Timeout = TimeSpan.FromMinutes(5);   // TODO:  Maybe remove this code ?

            // List of symbols to get data from and create the action list
            var tickerlist = getMasterTickerList();

            for (int dayBehind = 1; dayBehind < 5; dayBehind++)                                   //TODO:   Abstract the for loop into variables for any API for any company ! ... Good idea !!!
            {
                // List of actions to be processed for each day
                List<Func<Task>> _events = new List<Func<Task>>();

                string date = DateTime.Today.AddDays(-dayBehind).ToString("yyyy-MM-dd");

                // Process the task for every symbol in the symbol list
                foreach (string symbol in tickerlist)
                {
                    // Add events to the action bag
                    _events.Add(async () => { await TheTask(symbol, date, ""); });

                }
                // Process one day at a time of all of the tasks that have been added to the list of tasks
                await ProcessActionsAsyncThrottled(_events, date);
            }
        }


        // The task to be processed by the Async Throttled Method
        public static async Task TheTask(string symbol, string date, string pagination)
        {

            try
            {
                //Log.Information("Requesting {0} : {1} : {2}", symbol, date, pagination);

                // Keep trying until we get a good response for this URL request
                bool symbolRetry = true;
                do
                {
                    // Create the endpoint
                    string url = baseURL + symbol + "/" + date + "?limit=" + resultsLimit + "&apiKey=" + apiKey + "&timestamp=" + pagination;
                    // Call the endpoint
                    HttpResponseMessage response = await Client.GetAsync(url);
                    //int responseCode = (int)response.StatusCode; //.ToString();
                    string responseBody = await response.Content.ReadAsStringAsync();

                    // Process the response
                    if ((int)response.StatusCode == 200)
                    {
                        // Don't retry this response
                        symbolRetry = false;
                        // Process the API call
                        String urlContents = await response.Content.ReadAsStringAsync();
                        // Parse the JSON response
                        var historicalTrades = urlContents.FromJson<List<HistoricalTrades>>();
                        var count = historicalTrades[0].results_count;   // .ResultsCount.ConvertTo<int>();

                        Log.Information((int)response.StatusCode + " " + response.StatusCode.ToString() + " : " + symbol + " : " + date + " : " + count + " : " + pagination);

                        if (count > 0)
                        {
                            // Verify the directory and Write The Data To the Disk
                            string csv = historicalTrades[0].results.ToCsv<List<Result>>();


                            string directoryPath = myConstants.OutputDirectory + date + "\\";


                            try
                            {
                                if (!Directory.Exists(directoryPath))
                                {
                                    // Try to create the directory.
                                    DirectoryInfo di = Directory.CreateDirectory(directoryPath);
                                }
                            }
                            catch (IOException ioex)
                            {
                                Console.WriteLine(ioex.Message);
                            }
                            string filePath = directoryPath + date + "_" + symbol + "_" + pagination + "_" + count;    // TODO:   Need robust error correction  ( liie check for folder exists )
                            File.WriteAllText(filePath, csv.ToString());


                            // Recurse if the limit was reached
                            if (count == resultsLimit)
                            {
                                // Wait here and run the next item using a delay
                                await Task.Delay(delayBetweenConcurrentRequests);
                                //Thread.Sleep(delayBetweenConcurrentRequests);
                                // Calculate pagination
                                string nextPagination = historicalTrades[0].results[count - 1].t.ToString();
                                // Recurse on the task in Async mode to avoid stack overflow
                                // await Task.Run(() => TheTask(symbol, date, nextPagination));
                                TheTask(symbol, date, nextPagination);
                            }
                        }
                    }
                    else
                    {
                        // Default is to not retry the API call
                        symbolRetry = false;

                        // TODO:   Look at possible error codes and make different decisions.    This can be abstracted into a character code class

                        // This is just going to too fast for rate limiting, so sleep and then retry
                        if ((int)response.StatusCode == 429)
                        {
                            //await Task.Delay(delayBetweenConcurrentRequests);
                            //Thread.Sleep(2000); // Sleep to let API catch up
                            await Task.Delay(delayBetweenConcurrentRequests);
                            symbolRetry = true;
                        }
                        // Internal server error
                        if ((int)response.StatusCode == 500)
                        {
                            //Thread.Sleep(2000); // Sleep to let API catch up
                            await Task.Delay(delayBetweenConcurrentRequests);
                            symbolRetry = true;
                        }

                        // TODO:    This might be some random error with weekends, so don't retry the API response.   Handle errors more elegantly
                        if ((int)response.StatusCode == 404)
                        {
                            //Thread.Sleep(5000);
                            symbolRetry = false;    /// What should we do on this one ???
                        }

                        //  Log the error
                        Log.Error("ERROR : " + (int)response.StatusCode + " " + response.StatusCode.ToString() + " : " + symbol + " : " + date + " : " + pagination);

                    }
                }
                while (symbolRetry == true);    // Retry the API call

            }
            catch (Exception ex)
            {
                if (ex.InnerException is TimeoutException)
                {
                    ex = ex.InnerException;
                }
                else if (ex is TaskCanceledException)
                {
                    if ((ex as TaskCanceledException).CancellationToken == null || (ex as TaskCanceledException).CancellationToken.IsCancellationRequested == false)
                    {
                        ex = new TimeoutException("Timeout occurred");
                        // Retry the task on the task in Async mode to avoid stack overflow
                        //await Task.Run(() => TheTask(symbol, date, pagination));
                    }
                }
                Log.Error(string.Format("Retrying because of Exception at calling {0} : {1} : {2} : {3}", symbol, date, pagination, ex.Message), ex);

                // Retry the task on the task in Async mode to avoid stack overflow
                await Task.Run(() => TheTask(symbol, date, pagination));

            }
        }

        // Processes the functions in the action list and runs at a throttled speed as defined by the class parameters
        public static async Task ProcessActionsAsyncThrottled(List<Func<Task>> actionList, string date)
        {
            var throttler = new SemaphoreSlim(numberOfThreadsPerConcurrentRequests);

            // Task list for async
            var tasks = new List<Task>();
            foreach (var action in actionList)
            {
                // let's wait here until we can pass from the Semaphore
                await throttler.WaitAsync();

                // add our task here
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                            // Action from the action list
                            await action();

                            // let's wait here for to honor the API's rate limit      
                            //await Task.Delay(delayBetweenConcurrentRequests);
                        }
                    finally
                    {
                            // here we release the throttler immediately
                            throttler.Release();
                    }
                }
                ));
            }
            // await for all the tasks to complete
            await Task.WhenAll(tasks.ToArray());

            // Finish
            Log.Information("Finished processing the async tasks : Tasks for {0} symbols : for date {1}", actionList.Count, date);
        }

        public static List<string> getMasterTickerList()
        {
            // Create a list of Tickers to return from the function
            List<string> tickerList = new List<string>();
            tickerList.Add("AAPL");
            tickerList.Add("AMD");
            tickerList.Add("NVDA");
            tickerList.Add("TSLA");
            tickerList.Add("MSFT");

            // Return the ticker list
            return tickerList;
        }



        //-----------------------------------------------
        // Json Class Definitions
        //-----------------------------------------------

        //TODO:   Fix this JSON schema issue and make it right !!


        public class Result
        {


            public object t { get; set; }
            public object y { get; set; }
            public object f { get; set; }
            public object q { get; set; }
            public object i { get; set; }
            public object x { get; set; }
            public object s { get; set; }
            public List<int> c { get; set; }
            public object p { get; set; }
            public object z { get; set; }
        }
        public class Z
        {
            public string name { get; set; }
            public string type { get; set; }
        }
        public class Y
        {
            public string name { get; set; }
            public string type { get; set; }
        }
        public class I
        {
            public string name { get; set; }
            public string type { get; set; }
        }
        public class P
        {
            public string name { get; set; }
            public string type { get; set; }
        }
        public class C
        {
            public string name { get; set; }
            public string type { get; set; }
        }
        public class I2
        {
            public string name { get; set; }
            public string type { get; set; }
        }
        public class E
        {
            public string name { get; set; }
            public string type { get; set; }
        }
        public class X
        {
            public string name { get; set; }
            public string type { get; set; }
        }
        public class R
        {
            public string name { get; set; }
            public string type { get; set; }
        }
        public class T
        {
            public string name { get; set; }
            public string type { get; set; }
        }
        public class F
        {
            public string name { get; set; }
            public string type { get; set; }
        }
        public class Q
        {
            public string name { get; set; }
            public string type { get; set; }
        }
        public class S
        {
            public string name { get; set; }
            public string type { get; set; }
        }
        public class Map
        {
            public Z z { get; set; }
            public Y y { get; set; }
            public I I { get; set; }
            public P p { get; set; }
            public C c { get; set; }
            public I2 i { get; set; }
            public E e { get; set; }
            public X x { get; set; }
            public R r { get; set; }
            public T t { get; set; }
            public F f { get; set; }
            public Q q { get; set; }
            public S s { get; set; }
        }
        public class HistoricalTrades
        {
            public List<Result> results { get; set; }
            public bool success { get; set; }
            public Map map { get; set; }
            public string ticker { get; set; }
            public int results_count { get; set; }
            public int db_latency { get; set; }
        }
    }


}

