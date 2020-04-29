using System;
using System.IO;
using System.Net;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;

namespace AzureFunctionApp
{
    public  class FunctionInsights
    {
        string instrumentationKey = "0bf129fc-c9c1-4d72-b200-c21fe882828f";
        string logApiKey = "013o4tpCxcfY9vGHmp4VRyHbWX-b9ebbBFSntE-oq4";
        string logApiUrl = "https://yourorg.oktapreview.com/api/v1/logs";


        private string GetLogs(string logUrl, string apiKey)
        {
            string responseText = "";
            try
            {
                var webRequest = WebRequest.Create(logUrl) as HttpWebRequest;
                if (webRequest != null)
                {
                    webRequest.Method = "GET";
                    webRequest.Headers.Add("Authorization", "SSWS " + apiKey);
                    webRequest.Accept = "application/json";
                    webRequest.ContentType = "application/json";
                    WebResponse response = webRequest.GetResponse();
                    using (var reader = new StreamReader(response.GetResponseStream()))
                    {
                        string jsonString = reader.ReadToEnd();
                        dynamic jsonObject = JsonConvert.DeserializeObject(jsonString);
                        JToken parsedJson = JToken.Parse(jsonObject.ToString());
                        responseText = parsedJson.ToString(Formatting.None);
                    }
                }
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
            return responseText;
        }

        [FunctionName("FunctionInsights")]
        public void Run([TimerTrigger("0 * * * * *")]TimerInfo myTimer, ILogger log)
        {
            log.LogInformation($"Insights function executed at: {DateTime.Now}");
            try
            {
                DateTime nowMinusOne = DateTime.UtcNow.AddMinutes(-1);
                string fullLogApiUrl = logApiUrl + "?since=" + nowMinusOne.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'")
                    + "&sortOrder=DESCENDING";

                string jsonString = GetLogs(fullLogApiUrl, logApiKey);

                if (jsonString + "" != "")
                {
                    string filteredString = "[" + jsonString.TrimEnd(jsonString[jsonString.Length - 1]) + "]";
                    dynamic jsonObject = JsonConvert.DeserializeObject(filteredString);
                    int i = 0;
                    foreach (dynamic item in jsonObject)
                    {
                        string tempStr = item.ToString();
                        string eventStr = "";
                        if (tempStr.Contains("FAIL"))
                        {
                            eventStr = item.displayMessage;
                            var config = new TelemetryConfiguration();
                            config.InstrumentationKey = instrumentationKey;
                            TelemetryClient client = new TelemetryClient(config);
                            client.Context.User.Id = item.actor.alternateId;
                            client.Context.User.AccountId = item.actor.displayName;
                            client.Context.User.AuthenticatedUserId = item.client.ipAddress;
                            client.Context.User.UserAgent = item.client.userAgent.rawUserAgent;
                            client.Context.Session.Id = item.client.geographicalContext.country + ", " + item.client.geographicalContext.State + ", " +
                                item.client.geographicalContext.city + ", " + item.client.geographicalContext.postalCode;
                            client.TrackEvent(eventStr);
                            log.LogInformation("Event sent at " + DateTime.Now.ToLocalTime().ToString());
                        }
                        else
                        {
                            i++;
                            continue;
                        }
                    }
                }
                else
                {
                    log.LogInformation("Nothing sent at " + DateTime.Now.ToLocalTime().ToString());
                }
            }
            catch (Exception ex)
            {
                log.LogInformation(ex.Message);
            }
        }
    }
}
