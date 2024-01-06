using System;
using System.IO;
using System.Net;
using System.Text;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Azure.Core;
using Azure.Identity;
using System.Threading.Tasks;

namespace WeatherStatus
{
    public class Function1
    {
        [FunctionName("Function1")]
        public async Task Run([TimerTrigger("0 3-15/4 * * *")] TimerInfo myTimer, ILogger log)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

            //Date
            DateTime time = DateTime.Now;
            string timeStamp = time.AddHours(1).ToString("dd/MM/yyyy HH:mm");
            //URL to weatherstack
            string apiKey = "XXXXXX6b83e1415cee33de96d30fb";
            string city = "Gothenburg";
            string urlW = $"http://api.weatherstack.com/current?access_key={apiKey}&query={city}";
            var httpRequestW = (HttpWebRequest)WebRequest.Create(urlW);
            //Token to azure from managed identity. Must be contributor.
            AccessToken token =
                await new DefaultAzureCredential()
                .GetTokenAsync(
                    new TokenRequestContext(
            new[] { "https://management.azure.com/.default" }
            ));
            //Specify subscription
            string urlAzure = "https://management.azure.com/subscriptions/2d9f44ea-e3df-4ea1-b956-8c7a43b119a0/resourceGroups/rg-owner?api-version=2022-09-01";
            var httpRequestAzure = (HttpWebRequest)WebRequest.Create(urlAzure);

            try
            {
                //GET request to weatherstack
                httpRequestW.Accept = "application/json";
                var httpResponse = (HttpWebResponse)httpRequestW.GetResponse();
                //Show json response from weatherstack
                var streamReader = new StreamReader(httpResponse.GetResponseStream());
                var result = streamReader.ReadToEnd();
                //Get temperature from weatherstack
                dynamic weather = JObject.Parse(result);
                string temperature = weather.current.temperature;

                //Authnticate to azure
                httpRequestAzure.Accept = "application/json";
                httpRequestAzure.Headers["Authorization"] = "Bearer " + token.Token;
                //GET request
                var httpResponseAzure = (HttpWebResponse)httpRequestAzure.GetResponse();
                //Console.WriteLine("StatusCode: " + httpResponseAzure.StatusCode + " Method " + httpResponseAzure.Method);
                var streamReaderAzure = new StreamReader(httpResponseAzure.GetResponseStream());
                var resultAzure = streamReaderAzure.ReadToEnd();
                //Adjust JSON
                dynamic jsonAzure = JsonConvert.DeserializeObject(resultAzure);
                jsonAzure["tags"]["TEMPERATURE"] = temperature;
                jsonAzure["tags"]["SYNC"] = timeStamp;
                jsonAzure["properties"].Remove("provisioningState");
                string jsonAzureString = jsonAzure.ToString();
                //PATCH
                var requestAzure = (HttpWebRequest)WebRequest.Create(Uri.EscapeUriString(urlAzure));
                if (requestAzure == null)
                    throw new ApplicationException(string.Format("Could not create the httprequest from the url:{0}", urlAzure));

                requestAzure.Method = "PATCH";
                requestAzure.Accept = "application/json";
                requestAzure.Headers["Authorization"] = "Bearer " + token.Token;
                UTF8Encoding encodingx = new UTF8Encoding();
                var byteArrayx = Encoding.ASCII.GetBytes(jsonAzureString);
                requestAzure.ContentLength = byteArrayx.Length;
                requestAzure.ContentType = "application/json";
                Stream dataStreamx = requestAzure.GetRequestStream();
                dataStreamx.Write(byteArrayx, 0, byteArrayx.Length);
                dataStreamx.Close();
                try
                {
                    var responseAzure = (HttpWebResponse)requestAzure.GetResponse();
                }
                catch (WebException ex)
                {
                    HttpWebResponse errorResponseAzure = (HttpWebResponse)ex.Response;
                }
            }
            catch (Exception ex)
            {
            }finally { httpRequestW.Abort(); }
        }
    }
}
