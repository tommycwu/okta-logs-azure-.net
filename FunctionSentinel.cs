using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace AzureFunctionApp
{
    public class FunctionSentinel
    {
        string logApiKey = "013o4tpCxcfY9vGHmp4VRyHbWX-b9ebbBFSntE-oq4";
        string logApiUrl = "https://yourorg.oktapreview.com/api/v1/logs";
        string workspaceId = "af9ddddd-c440-48fd-b1e4-25b6d2be9419";
        string pk = "8auE/3CAFw/K5BY+/wZn14bXEiueWmd01pV9O10zWmNkur2mwPXBGvatXKBq1Y4ctqnBSqFneADet1e6qXhSqA==";
        string sentinelLogName = "Test";

        private static string BuildSignature(string message, string secret)
        {
            var encoding = new System.Text.ASCIIEncoding();
            byte[] keyByte = Convert.FromBase64String(secret);
            byte[] messageBytes = encoding.GetBytes(message);
            using (var hmacsha256 = new HMACSHA256(keyByte))
            {
                byte[] hash = hmacsha256.ComputeHash(messageBytes);
                return Convert.ToBase64String(hash);
            }
        }

        private string PostData(string postUrl, string logType, string authSignature, string xmsDate, string json)
        {
            try
            {
                var client = new System.Net.Http.HttpClient();
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.DefaultRequestHeaders.Add("Log-Type", logType);
                client.DefaultRequestHeaders.Add("Authorization", authSignature);
                client.DefaultRequestHeaders.Add("x-ms-date", xmsDate);
                client.DefaultRequestHeaders.Add("time-generated-field", "");

                var httpContent = new StringContent(json, Encoding.UTF8);
                httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                Task<System.Net.Http.HttpResponseMessage> response = client.PostAsync(postUrl, httpContent);

                System.Net.Http.HttpContent responseContent = response.Result.Content;
                string result = responseContent.ReadAsStringAsync().Result + "";
                if (result == "")
                {
                    return "Return Result: Success - " + DateTime.Now.ToLocalTime().ToString(); ;
                }
                else
                {
                    return "Return Result: " + result;
                };
            }
            catch (Exception ex)
            {
                return "API Post Exception: " + ex.Message;
            }
        }

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

        [FunctionName("FunctionSentinel")]
        public void Run([TimerTrigger("0 * * * * *")]TimerInfo myTimer, ILogger log)
        {
            log.LogInformation("Polling began at: " + DateTime.Now.ToString());
            try
            {
                DateTime nowMinusOne = DateTime.UtcNow.AddMinutes(-1);
                string fullLogApiUrl = logApiUrl + "?since=" + nowMinusOne.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'")
                    + "&sortOrder=DESCENDING";
                
                string jsonString = GetLogs(fullLogApiUrl, logApiKey);


                if (jsonString + "" != "")
                {
                    dynamic jsonObject = JsonConvert.DeserializeObject("[" + jsonString.TrimEnd(jsonString[jsonString.Length - 1]) + "]");

                    string dateString = DateTime.UtcNow.ToString("r");
                    string sentinelUrl = "https://" + workspaceId + ".ods.opinsights.azure.com/api/logs?api-version=2016-04-01";

                    foreach (dynamic jsonItem in jsonObject)
                    {
                        byte[] jsonBytes = Encoding.UTF8.GetBytes(jsonItem.ToString());
                        string stringToHash = "POST\n" + jsonBytes.Length + "\napplication/json\n" + "x-ms-date:" + dateString + "\n/api/logs";
                        string hashedString = BuildSignature(stringToHash, pk);
                        string signature = "SharedKey " + workspaceId + ":" + hashedString;
                        string itemString = jsonItem.ToString();
                        string resultString = PostData(sentinelUrl, sentinelLogName, signature, dateString, itemString);
                        log.LogInformation(resultString);
                    }
                }
                else
                {
                    log.LogInformation("Nothing sent at " + DateTime.Now.ToString());
                }
            }
            catch (Exception ex)
            {
                log.LogInformation(ex.Message);
            }

        }
    }
}
