﻿using Azure.Data.Tables;
using System;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.Azure.Functions.Worker.Http;
using System.ComponentModel;
using ScottPlot.Palettes;
using System.Net;
using System.Threading.Tasks;
using Azure;

namespace VedAstro.Library
{
    public static class ApiStatistic
    {
        /// <summary>
        /// sample holder type when doing interop
        /// </summary>
        public record GeoLocationRawAPI(dynamic MainRow, dynamic MetadataRow);

        private static readonly TableServiceClient ipAddressServiceClient;
        private static readonly TableServiceClient webPageServiceClient;
        private static readonly TableServiceClient requestUrlStatisticServiceClient;
        private static readonly TableServiceClient subscriberStatisticServiceClient;
        private static readonly TableServiceClient userAgentStatisticServiceClient;
        private static readonly TableServiceClient rawRequestStatisticServiceClient;

        private static readonly TableClient ipAddressStatisticTableClient;
        private static readonly TableClient webPageStatisticTableClient;
        private static readonly TableClient requestUrlStatisticTableClient;
        private static readonly TableClient subscriberStatisticTableClient;
        private static readonly TableClient userAgentStatisticTableClient;
        private static readonly TableClient rawRequestStatisticTableClient;




        //-------------------------------------


        /// <summary>
        /// Logs IP to for statistics
        /// </summary>

        public static void LogIpAddress(HttpRequestData incomingRequest)
        {
            // Step 1: Get the current month and year in the format "yyyy-MM"
            var todayRecord = DateTime.Now.ToString("yyyy-MM");

            // Step 2: Get the caller's IP address (or use "0.0.0.0" if not available)
            var ipAddress = incomingRequest?.GetCallerIp()?.ToString() ?? "0.0.0.0";

            // Step 3: Check if the IP address already exists in the table
            Expression<Func<IpAddressStatisticEntity, bool>> expression =
                call => call.PartitionKey == ipAddress && call.RowKey == todayRecord;
            var recordFound = ipAddressStatisticTableClient.Query(expression).FirstOrDefault();

            // If the IP address exists, update call statistics
            if (recordFound != null)
            {

                // Calculate calls per second
                if (recordFound.PerSecondTimestamp == null ||
                    ((DateTimeOffset.UtcNow - recordFound.PerSecondTimestamp.Value).TotalSeconds >= 60))
                {
                    recordFound.CallsPerSecond = 1;
                    recordFound.PerSecondTimestamp = DateTimeOffset.UtcNow;
                }
                else
                {
                    recordFound.CallsPerSecond++;
                }

                // Calculate calls per minute
                if (recordFound.PerMinuteTimestamp == null ||
                    ((DateTimeOffset.UtcNow - recordFound.PerMinuteTimestamp.Value).TotalMinutes >= 1))
                {
                    recordFound.CallsPerMinute = recordFound.CallsPerSecond;
                    recordFound.CallsPerSecond = 0;
                    recordFound.PerSecondTimestamp = null;
                    recordFound.PerMinuteTimestamp = DateTimeOffset.UtcNow;
                }
                else
                {
                    recordFound.CallsPerMinute += recordFound.CallsPerSecond;
                    recordFound.CallsPerSecond = 0;
                }

                // Calculate calls per hour
                if (recordFound.PerHourTimestamp == null ||
                    ((DateTimeOffset.UtcNow - recordFound.PerHourTimestamp.Value).TotalHours >= 1))
                {
                    recordFound.CallsPerHour = recordFound.CallsPerMinute;
                    recordFound.CallsPerMinute = 0;
                    recordFound.PerMinuteTimestamp = null;
                    recordFound.PerHourTimestamp = DateTimeOffset.UtcNow;
                }
                else
                {
                    recordFound.CallsPerHour += recordFound.CallsPerMinute;
                    recordFound.CallsPerMinute = 0;
                }

                // Calculate calls per day
                if (recordFound.PerDayTimestamp == null ||
                    ((DateTimeOffset.UtcNow - recordFound.PerDayTimestamp.Value).TotalDays >= 1))
                {
                    recordFound.CallsPerDay = recordFound.CallsPerHour;
                    recordFound.CallsPerHour = 0;
                    recordFound.PerHourTimestamp = null;
                    recordFound.PerDayTimestamp = DateTimeOffset.UtcNow;
                }
                else
                {
                    recordFound.CallsPerDay += recordFound.CallsPerHour;
                    recordFound.CallsPerHour = 0;
                }

                // Calculate calls per month
                if (recordFound.PerMonthTimestamp == null ||
                    ((DateTimeOffset.UtcNow - recordFound.PerMonthTimestamp.Value).TotalDays >= DateTime.DaysInMonth(DateTime.UtcNow.Year, DateTime.UtcNow.Month)))
                {
                    recordFound.CallsPerMonth = recordFound.CallsPerDay;
                    recordFound.CallsPerDay = 0;
                    recordFound.PerDayTimestamp = null;
                    recordFound.PerMonthTimestamp = DateTimeOffset.UtcNow;
                }
                else
                {
                    recordFound.CallsPerMonth += recordFound.CallsPerDay;
                    recordFound.CallsPerDay = 0;
                }

                // Update the entity in the table
                ipAddressStatisticTableClient.UpsertEntityAsync(recordFound);
            }
            else
            {
                //Create a new log entry for the IP address
                var newRow = new IpAddressStatisticEntity();
                newRow.PartitionKey = Tools.CleanAzureTableKey(ipAddress);
                newRow.RowKey = todayRecord;
                ipAddressStatisticTableClient.AddEntity(newRow);
            }

        }
        public static void LogWebPage(string webPage)
        {
            //get month and year in correct format 2019-10
            var todayRecord = DateTime.Now.ToString("yyyy-MM");

            //# get ip address out
            //var ipAddress = incomingRequest?.GetCallerIp()?.ToString() ?? "0.0.0.0";
            var cleanWebPageUrl = Tools.CleanAzureTableKey(webPage);
            //# check if already exist
            Expression<Func<WebPageStatisticEntity, bool>> expression = call => call.PartitionKey == cleanWebPageUrl && call.RowKey == todayRecord;

            //execute search
            var recordFound = webPageStatisticTableClient.Query(expression).FirstOrDefault();

            //# if existed, update call count
            var isExist = recordFound != null;
            if (isExist)
            {
                //update row
                recordFound.CallCount = ++recordFound.CallCount; //increment call count
                webPageStatisticTableClient.UpsertEntity(recordFound);
            }

            //# if not exist, make new log
            else
            {
                var newRow = new WebPageStatisticEntity();

                newRow.PartitionKey = cleanWebPageUrl;
                //get month and year in correct format 2019-10
                newRow.RowKey = todayRecord;
                newRow.CallCount = 1;
                webPageStatisticTableClient.AddEntity(newRow);
            }
        }

        public static void LogRequestUrl(HttpRequestData incomingRequest)
        {

            //# get request URL
            var requestUrl = incomingRequest?.Url.AbsolutePath ?? "no URL";

            //get month and year in correct format 2019-10
            var todayRecord = DateTime.Now.ToString("yyyy-MM");

            //# check if URL already exist
            //make a search for ip address stored under row key
            var cleanAzureTableKey = Tools.CleanAzureTableKey(requestUrl, "-").Truncate(100); //keep short as not overcrowd
            Expression<Func<RequestUrlStatisticEntity, bool>> expression = call => call.PartitionKey == cleanAzureTableKey && call.RowKey == todayRecord;

            //execute search
            var recordFound = requestUrlStatisticTableClient.Query(expression).FirstOrDefault();

            //# if existed, update call count
            var isExist = recordFound != null;
            if (isExist)
            {
                //update row
                recordFound.CallCount = ++recordFound.CallCount; //increment call count
                requestUrlStatisticTableClient.UpsertEntity(recordFound);
            }

            //# if not exist, make new log
            else
            {
                var newRow = new RequestUrlStatisticEntity();

                newRow.PartitionKey = cleanAzureTableKey;
                //get month and year in correct format 2019-10
                newRow.RowKey = todayRecord;
                newRow.CallCount = 1;
                requestUrlStatisticTableClient.AddEntity(newRow);
            }
        }

        public static void LogSubscriber(HttpRequestData incomingRequest)
        {
            //get host address as main ID of record
            var host = incomingRequest.ExtractHostAddress();

            //get date that this record would be in (Row Key)
            var currentDate = DateTime.Now.ToString("yyyy-MM");

            //# check if URL already exist
            //make a search for ip address stored under row key
            var cleanHostAddress = Tools.CleanAzureTableKey(host, "|");
            Expression<Func<RequestUrlStatisticEntity, bool>> expression = call =>
                    call.PartitionKey == cleanHostAddress &&
                    call.RowKey == currentDate;

            //execute search
            var recordFound = subscriberStatisticTableClient.Query(expression).FirstOrDefault();

            //# if existed, update call count
            var isExist = recordFound != null;
            if (isExist)
            {
                //update row
                recordFound.CallCount = ++recordFound.CallCount; //increment call count
                subscriberStatisticTableClient.UpsertEntity(recordFound);
            }

            //# if not exist, make new log
            else
            {
                var newRow = new SubscriberStatisticEntity();
                newRow.PartitionKey = cleanHostAddress;
                //get month and year in correct format 2019-10
                newRow.RowKey = currentDate;
                newRow.CallCount = 1; //start with 1
                //save to db
                subscriberStatisticTableClient.AddEntity(newRow);
            }
        }

        public static void LogUserAgent(HttpRequestData incomingRequest)
        {
            //get host address as main ID of record
            var requestHeaderList = incomingRequest.Headers.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
            requestHeaderList.TryGetValue("User-Agent", out var userAgentValues);
            var userAgent = userAgentValues?.FirstOrDefault() ?? "no User-Agent";

            //get date that this record would be in (Row Key)
            var currentDate = DateTime.Now.ToString("yyyy-MM");

            //# check if User-Agent already exist
            //make a search for ip address stored under row key
            var cleanUserAgent = Tools.CleanAzureTableKey(userAgent, "|");
            Expression<Func<UserAgentStatisticEntity, bool>> expression = call => call.PartitionKey == cleanUserAgent && call.RowKey == currentDate;

            //execute search
            var recordFound = userAgentStatisticTableClient.Query(expression).FirstOrDefault();

            //# if existed, update call count
            var isExist = recordFound != null;
            if (isExist)
            {
                //update row
                recordFound.CallCount = ++recordFound.CallCount; //increment call count
                userAgentStatisticTableClient.UpsertEntity(recordFound);
            }

            //# if not exist, make new log
            else
            {
                var newRow = new UserAgentStatisticEntity();
                newRow.PartitionKey = cleanUserAgent;
                //get month and year in correct format 2019-10
                newRow.RowKey = currentDate;
                newRow.CallCount = 1; //start with 1
                //save to db
                userAgentStatisticTableClient.AddEntity(newRow);
            }
        }

        /// <summary>
        /// Makes raw full header log of what ever that comes in
        /// NOTE: high cost carefully use
        /// </summary>
        public static void LogRawRequest(HttpRequestData incomingRequest)
        {
            //step 1: extract needed data from request
            var newRow = new RawRequestStatisticEntity();

            //convert to list
            var requestHeaderList = incomingRequest.Headers.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);

            for (int i = 0; i < requestHeaderList.Count; i++)
            {
                var currentHeader = requestHeaderList.ElementAt(i);
                var currentHeaderKey = currentHeader.Key;
                string currentValue = Tools.ListToString(currentHeader.Value.ToList());

                //debug print
                //Console.WriteLine($"{currentHeaderKey}:{currentValue}");

                //match with correct header based on attribute and fill in the value
                // Get all properties of the current instance
                var properties = newRow.GetType().GetProperties();
                foreach (var property in properties)
                {
                    var attribute = (DescriptionAttribute)property.GetCustomAttributes(typeof(DescriptionAttribute), false).FirstOrDefault();
                    if (attribute?.Description.Equals(currentHeaderKey, StringComparison.OrdinalIgnoreCase) ?? false)
                    {
                        property.SetValue(newRow, currentValue);
                        break;
                    }
                }
            }

            //step 2: generate hash to identify the data
            newRow.PartitionKey = incomingRequest?.GetCallerIp()?.ToString() ?? "no ip";
            //newRow.PartitionKey = newRow.CalculateCombinedHash();
            var url = incomingRequest.Url.ToString() ?? "no URL";
            newRow.RowKey = Tools.CleanAzureTableKey(url, "|"); //place url

            //step 3: add entry to database
            //TODO check if exist before overwrite
            rawRequestStatisticTableClient.UpsertEntity(newRow);
        }

        private static bool IsLoggingEnabled()
        {
            // Retrieve the environment variable
            var isEnabled = Environment.GetEnvironmentVariable("EnableLogging");

            // If the variable is not defined, default to false
            if (string.IsNullOrEmpty(isEnabled))
            {
                return false;
            }

            // If the variable is defined, try to parse it as a boolean
            if (bool.TryParse(isEnabled, out bool result))
            {
                return result;
            }

            // If the variable cannot be parsed as a boolean, default to false
            return false;
        }

        public static void LogCallInfo(HttpRequestData incomingRequest)
        {
            // Only continue if logging is enabled
            if (!IsLoggingEnabled())
            {
                return;
            }

            // Step 1: Extract needed data from request
            var callerIp = incomingRequest?.GetCallerIp()?.ToString() ?? "no ip";
            var userAgent = incomingRequest.Headers.FirstOrDefault(x => x.Key.Equals("User-Agent", StringComparison.OrdinalIgnoreCase)).Value.FirstOrDefault() ?? "no User-Agent";
            var requestUrl = incomingRequest?.Url.AbsolutePath ?? "no URL";

            // Step 3: Create a new log entry
            var callInfoEntity = new CallInfoStatisticEntity
            {
                PartitionKey = callerIp,
                RowKey = Guid.NewGuid().ToString(),
                UserAgent = userAgent,
                RequestUrl = requestUrl,
                Timestamp = DateTime.UtcNow
            };

            // Step 4: Add the log entry to the database
            AzureTable.CallInfoStatistic.AddEntity(callInfoEntity);
        }

        // Define the CallInfoStatisticEntity class
        public class CallInfoStatisticEntity : ITableEntity
        {
            public string UserAgent { get; set; }
            public string RequestUrl { get; set; }
            public string PartitionKey { get; set; }
            public string RowKey { get; set; }
            public DateTimeOffset? Timestamp { get; set; }
            public ETag ETag { get; set; }
        }

        public static void Log(HttpRequestData incomingRequest)
        {
            ApiStatistic.LogCallInfo(incomingRequest);
            //ApiStatistic.LogIpAddress(incomingRequest);
            //ApiStatistic.LogRequestUrl(incomingRequest);
            //ApiStatistic.LogRawRequest(incomingRequest);
            //ApiStatistic.LogSubscriber(incomingRequest);
            //ApiStatistic.LogUserAgent(incomingRequest);

        }
    }

}
