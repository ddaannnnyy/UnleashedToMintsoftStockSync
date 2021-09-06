using System; 
using System.Net;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Newtonsoft.Json;
using System.Diagnostics;
/// <summary>
/// This is a program that StockSyncs Unleashed and Mintsoft. With Unleashed being the "master" system.
/// It product calls to Unleashed, creates a list of Product Codes and SOH Quantities, and then pushes that list to Mintsoft.
/// Written by Dan Hebdon - dannhebdon@gmail.com
/// 14/07/2021
/// </summary>
namespace UnleashedToMintsoftStockSync
{
    class Program
    {
        #region Variable Declarations
        //Mintsoft authentication values
        public static string USERNAME = string.Empty;
        public static string PASSWORD = string.Empty;
        public static string APIKEY = string.Empty;

        //Unleashed authentication values
        public static string UNLEASHEDID = string.Empty;
        public static string UNLEASHEDSECRET = string.Empty;

        //Variable Declarations
        public static string URL = "https://api.unleashedsoftware.com/StockOnHand";
        public static string QUERY = "format=json&IsAssembled=true&pageSize=1000"; //hardcoded query, not ideal but this program is a set and forget
        public static string ApiHost = URL + "?" + QUERY;

        public static List<StockOnHand> itemList = new List<StockOnHand>(); //new list for items received from Unleashed
        public static List<APIResult> errorList = new List<APIResult>();
        #endregion

        static void Main(string[] args)
        {
            Program p = new Program();
            
            //Here we are reading in the key values from keys.json.
            //If you are having problems reading values you can set them as hardcodes in the LoadHardCore() method.
            //Only enable one of these options
            LoadJson(); //loads API variables from the keys.json file
            //LoadHardCode(); //This method exists if you're having a problem loading the key values from the keys.json file, the values can be hardcoded in the method below.
            
            
            DateTime startTime = DateTime.Now; //start timer for process, allows the total runtime to be collected and displayed/logged.
            Console.WriteLine("=======");
            Console.WriteLine("MINTSOFT API KEY: " + APIKEY); //displays Mintsoft key being used for mintsoft calls
            Console.WriteLine("UNLEASHED API KEY: " + UNLEASHEDID); //displays Unleashed key being used for unleashed calls
            Console.WriteLine("Start time: " + startTime.ToShortTimeString());
            Console.WriteLine("=======");

            updateSOHToMintsoftWithList(p, QUERY, UNLEASHEDSECRET);

            Console.WriteLine("\r\n\r\n==== PROCESS COMPLETED ====");
            
            DateTime endTime = DateTime.Now; //end timer for process, used with startTime to collect and display time elapsed.
            Console.WriteLine("End time: " + endTime.ToShortTimeString());
            TimeSpan span = endTime.Subtract(startTime); //time elapsed for process.
            Console.WriteLine("Time taken (in minutes): " + span.TotalMinutes); //minutes elapsed from start, to end of process and printed in minutes

            Console.WriteLine("Writing log");
            string fileName = "log " + DateTime.Today.ToShortDateString().Replace('/', '-') +" "+ DateTime.Now.ToShortTimeString().Replace(':', ' ');
            writeLog(itemList, fileName, span.TotalMinutes);

        }

        ///<summary>
        ///This is a fallback method which you can enable in the Main() method. Values can be added here as hardcodes if you cannot use the keys.json reading LoadJson() for any reason. Remember to only have one of these options enabled in Main()
        ///</summary>
        public static void LoadHardCode()
        {
            Console.WriteLine("keys set to hardcode");
            USERNAME = "mintsoft account email";
            PASSWORD = "mintsoft account password"; 
            APIKEY = "mintsoft API Key"; //mintsoft API key, you have to make a call for this which is not done here. You should also change this to static in the mintsoft user settings.
            UNLEASHEDID = "unleashed API ID"; //unleashed API Id
            UNLEASHEDSECRET = "unleashed API Key"; //unleashed API Key
        }
        /// <summary>
        /// This fetches data such as API Keys, and Usernames / Passwords from the keys.json file. If you are having a problem reading from this file see LoadHardCode() which will let you hardcode values as a fallback. Only enable one of these methods in the Main()
        /// </summary>
        public static void LoadJson()
        {
            Console.WriteLine("Now Reading User Details from keys.json file");
            try
            {
                using (StreamReader r = File.OpenText("keys/keys.json")) //opens keys/keys.json file
                {
                    string json = r.ReadToEnd();
                    List<Keys> keys = JsonConvert.DeserializeObject<List<Keys>>(json);

                    foreach (var key in keys)
                    {
                        USERNAME = key.MintsoftUsername; //mintsoft account email
                        PASSWORD = key.MintsoftPassword; //mintsoft account password
                        APIKEY = key.MintsoftAPIKey; //mintsoft API key, you have to make a call for this which is not done here. You should also change this to static in the mintsoft user settings.
                        UNLEASHEDID = key.UnleashedAPIId; //unleashed API Id
                        UNLEASHEDSECRET = key.UnleashedAPIKey; //unleashed API Key
                    }
                }
            }
            catch (Exception)
            {
                Console.WriteLine("\r\nAn Error has occured, please make sure the keys file is correct. \r\nPress y if you could like to open the file location now\r\nPress r if you could like to try again");
                ConsoleKeyInfo answer = Console.ReadKey(); //gets answer from console.
                if (answer.KeyChar == 'y') //open file where keys.json should be
                {
                    var path = Directory.GetCurrentDirectory() + "\\keys";
                    Process.Start("explorer.exe", path);
                }
                if (answer.KeyChar == 'r') //retries the loader
                {
                    LoadJson();
                }
            }
        }

        /// <summary>
        /// Calls Unleashed for all products stock on hand, iterates through the all pages received from unleashed and creates a list of object StockOnHand. Then pushes that list to Mintsoft to update all SOH levels.
        /// </summary>
        /// <param name="program">Program instance to run methods from</param>
        /// <param name="query">Query for Unleashed product API call, used to generate a signature for unleashed calls. i.e. everything after and not including the ? in the call URL</param>
        /// <param name="UNLEASHEDSECRET">Unleashed API key, used to authenticate and generate a signature for unleashed calls</param>
        public static void updateSOHToMintsoftWithList(Program program, string query, string UNLEASHEDSECRET)
        { //This is not very good loose coupling and high cohesion, I'm sorry.        
            try
            {
                
                var StockLevels = program.SetItemsUnleashed(query, UNLEASHEDSECRET); //call once to get the number of pages
                var splitResult = StockLevels.Split("},"); //splits products received from the api response. this could be done cleaner but I couldn't get the jsondeserialisation working :( could also be regex.

                int pages = getPages(splitResult); //extracts the number of pages reported from unleashed

                Console.WriteLine(pages.ToString() + " Pages of products");

                for (int i = 1; i <= pages; i++) // runs unleashed call for each page, changing the page requested until all pages reported have been called.
                {
                    Console.WriteLine("Starting Page: {0}", i.ToString());
                    StockLevels = program.SetItemsUnleashed(query, UNLEASHEDSECRET, "https://api.unleashedsoftware.com/StockOnHand" + "/" + i.ToString()); //call again to be able to cycle through pages
                    splitResult = StockLevels.Split("},"); //splits products received from the api response.

                    foreach (var item in splitResult) //splits product into each key and variable from unleashed in order to extract productcode and sohquantity.
                    {
                        try
                        {
                            string[] splitItem = item.Split(",");

                            string[] splitProductCode = splitItem[0].Split(":");
                            string productCode = string.Empty;
                            if (splitProductCode[1].Contains("Product")) //the first "product" is split into an array of 3, [Items:,Product:,USEFUL CODE], this moves the searcher down one on this first record.
                            {
                                productCode = splitProductCode[2].Replace("\"", ""); //cleans product code string from the split above, again probably shuld at least be regex.
                            }
                            else
                            {
                                productCode = splitProductCode[1].Replace("\"", "");
                            }


                            string[] splitSOHValue = splitItem[11].Split(":"); //does the same as above but captures the SOH value.
                            string SOHValue = splitSOHValue[1];
                            if (Convert.ToInt32(Math.Floor(Convert.ToDouble(SOHValue))) < 0) //mintsoft doesn't accept negative SOH values, so if the stock has negative available it is changed to 0.
                            {
                                SOHValue = "0";
                            }
                            if (Convert.ToInt32(Math.Floor(Convert.ToDouble(SOHValue))) >= 100) //ebay has listing limits, this prevents items from being over inventoried on ebay (e.g. 14000 units available but a realisic ceiling is 100)
                            {
                                SOHValue = "100";
                            }


                            setStockUpdateList(productCode, SOHValue, itemList); //passes productcode, soh value, and the list for the item to be added.
                        }
                        catch (IndexOutOfRangeException) { } //this triggers on the pagination as it doesn't split correctly, it can be disregarded as we've already got page count and it's not a product
                        catch (Exception ex)
                        {
                            Console.WriteLine("\r\nAn error occured: " + ex.ToString());
                        }
                    }                   
                }

                try
                {
                    var results = program.SetItemsWithList(APIKEY, itemList); //makes a call to mintsoft and passes the entire list of items and stock on hand levels

                    foreach (var result in results) //reports errors if the result returned from mintsoft contains failed calls.
                    {
                        if (result.Success == false)
                        {
                            Console.WriteLine("===Error occured===");
                            Console.WriteLine(result.ID);
                            Console.WriteLine(result.Message);
                            Console.WriteLine(result.WarningMessage);
                            Console.WriteLine("===================\r\n");

                            errorList.Add(result); //adds result to errorList to be written to log
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("\r\nAn error occured: " + ex.ToString());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("\r\nAn error occured: " + ex.ToString()); ;
            }
        }

        /// <summary>
        /// Splits the response from the Unleashed call and reads the number of pages reported by unleashed
        /// </summary>
        /// <param name="splitResult">the response from Unleashed to read</param>
        /// <returns>int of pages reported by unleashed.</returns>
        public static int getPages(string[] splitResult)
        {
            string[] getPageCount = splitResult[0].Split("NumberOfPages");
            string extractPageCount = getPageCount[1];

            string[] pageCount = extractPageCount.Split(":");
            int pageCountInt = int.Parse(pageCount[1]);

            return pageCountInt;
        }

        /// <summary>
        /// This attaches headers and makes the call to the Unleashed API
        /// </summary>
        /// <param name="query">query for call, every after but not including the ? in the URL, used for filters.</param>
        /// <param name="key">Unleashed API Key</param>
        /// <param name="url">The base URL and Endpoint used for the call. DEFAULT = "https://api.unleashedsoftware.com/StockOnHand"</param>
        /// <returns>Returns response from Unleashed in json format.</returns>
        public string SetItemsUnleashed(string query, string key, string url = "https://api.unleashedsoftware.com/StockOnHand")
        {

            var webClient = new WebClient(); //creates webClient for Unleashed API call

            string signature = GetSignature(query, key); //creates signature from query and api key. Needed for unleashed call.

            webClient.Headers.Add("api-auth-id", UNLEASHEDID); //adds API ID header
            webClient.Headers.Add("api-auth-signature", signature); //adds API signature header
            webClient.Headers.Add("Accept", "application/json"); //adds Accept header
            webClient.Headers.Add("Content-Type", "application/json"); //adds Content-Type header
            webClient.Headers.Add("Client-Type", "Marine Warehouse Pty Ltd/UnleashedToMintsoftStockSync");

            string apiCall = url + "?" + query; //combines url and query for the API call

            var ResultJson = webClient.DownloadString(apiCall); //API call to unleashed is done here

            return ResultJson.ToString();
        }

        /// <summary>
        /// Sends List of StockOnHand to Mintsoft through a BulkStockOnHandUpdate API POST
        /// </summary>
        /// <param name="apiKey">Mintsoft API key, if this isn't set to Static in the Mintsoft User Settings, then you'll have to regenerate this.</param>
        /// <param name="stockList">List of StockOnHand to be sent</param>
        /// <returns>Returns deserialised json of response</returns>
        public List<APIResult> SetItemsWithList(string apiKey, List<StockOnHand> stockList)
        {
            var OrderJson = Newtonsoft.Json.JsonConvert.SerializeObject(stockList); //serialises SOH list

            var webClient = new WebClient(); //creates webclient for Mintsoft API call.
            webClient.Headers.Add(HttpRequestHeader.Accept, "application/json"); //adds Accept header
            webClient.Headers.Add(HttpRequestHeader.ContentType, "application/json"); //adds Content-Type header

            var ResultJson = webClient.UploadString("https://api.mintsoft.co.uk/api/Product/BulkOnHandStockUpdate?APIKey=" + apiKey + "&ClientId=3&api_key=breakdown", "POST", "" + OrderJson + "");
            return Newtonsoft.Json.JsonConvert.DeserializeObject<List<APIResult>>(ResultJson);
        }

        /// <summary>
        /// Creates hmac64 sigature for Unleashed API calls, this is generated with the query of the API call and with the Unleashed API Key
        /// </summary>
        /// <param name="args">query for Unleashed API call</param>
        /// <param name="privatekey">Unleashed API Key</param>
        /// <returns>Returns hmac64 signature</returns>
        private static string GetSignature(string args, string privatekey)
        {
            //this is a snippet from Unleashed's API Documentation
            var encoding = new System.Text.UTF8Encoding();
            byte[] key = encoding.GetBytes(privatekey);
            var myhmacsha256 = new HMACSHA256(key);
            byte[] hashValue = myhmacsha256.ComputeHash(encoding.GetBytes(args));
            string hmac64 = Convert.ToBase64String(hashValue);
            myhmacsha256.Clear();
            return hmac64;
        }

        /// <summary>
        /// Adds productcode and SOH value to a List of StockOnHand, which can then be passed to mintsoft as part of the POST
        /// </summary>
        /// <param name="productCode">Product Code / SKU</param>
        /// <param name="SOHQuantity">SOH Value, this is a string but it will be converted to a rounded down int32.</param>
        /// <param name="existingProductList">An existing list of StockOnHand, defaults to null. If null then a new one is created.</param>
        /// <returns>Returns list of StockOnHand with the ProductCode and SOH added</returns>
        public static List<StockOnHand> setStockUpdateList(string productCode, string SOHQuantity, List<StockOnHand> existingProductList = null)
        {
            List<StockOnHand> newproductList = new List<StockOnHand>(); //creates a new list of StockOnHand

            if (existingProductList != null)
            {
                newproductList = existingProductList; //if a list is provided then the new list of StockOnHand is made to match it.
            }

            try
            {
                var stockUpdate = new StockOnHand
                {
                    SKU = productCode,
                    Quantity = Convert.ToInt32(Math.Floor(Convert.ToDouble(SOHQuantity))), //Mintsoft only accepts positive Int32 values for SOH, this will take negative values but they are set to zero later.
                    WarehouseId = 3
                };

                newproductList.Add(stockUpdate);
            }
            catch (Exception)
            {
                var stockUpdate = new StockOnHand //in case of a problem the product details are set to 0
                {
                    SKU = productCode,
                    Quantity = 0,
                    WarehouseId = 3
                };

                newproductList.Add(stockUpdate);
            }

            return newproductList;
        }

        /// <summary>
        /// A log writer that records SOH changes as well as any errors and failed responses from the Mintsoft call.
        /// </summary>
        /// <param name="itemList">Products and SOH sent to mintsoft</param>
        /// <param name="fileName">Filename to write log to.</param>
        /// <param name="timeTaken">Time taken is recorded for debugging purposes.</param>
        public static void writeLog(List<StockOnHand> itemList, string fileName, double timeTaken)
        {
            try
            {
                string filePath = "logs\\uploads\\";
                if (errorList.Count > 0)
                {
                    filePath = "logs\\uploadswitherrors\\";
                    fileName = fileName + " HASERRORS"; //appends a flag to file name if the error list contains anything.
                }
                using (StreamWriter outputFile = new StreamWriter(filePath + fileName + ".csv"))
                {
                    outputFile.WriteLine("SOH push performed " + DateTime.Now);
                    outputFile.WriteLine("Time taken: " + timeTaken.ToString() + "minutes" );
                    outputFile.WriteLine("Errors found: " + errorList.Count.ToString());

                    if (errorList.Count > 0) //only shows this direction if errors exist
                    {
                        outputFile.WriteLine("Errors are written at the end of this file");
                    }

                    outputFile.WriteLine("ProductCode,SOH");

                    foreach (var item in itemList)
                    {
                        outputFile.WriteLine("{0},{1}",item.SKU, item.Quantity);
                    }

                    if (errorList.Count > 0)
                    {
                        outputFile.WriteLine("\r\nSuccess,Product Code,ErrorMessage");
                        foreach (var item in errorList)
                        {
                            outputFile.WriteLine(item.Success + "," + item.ID + "," + item.Message.Replace(',', '-'));
                        }
                    }
                }
            }
            catch
            {
                Console.WriteLine("an error occurred writing log to file");
            }
        }
    }
}
