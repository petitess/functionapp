using System;
using System.IO;
using System.Net;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using Azure.Data.Tables;

namespace WeatherStatus
{
    public class Function1
    {

        [FunctionName("SendWeatherToStorage")]//"0 */1 * * *"
        public async Task Run([TimerTrigger("0 */1 * * *")] TimerInfo myTimer, ILogger log)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

            DateTime time = DateTime.Now;
            string timeStamp = time.AddHours(1).ToString("dd/MM/yyyy HH:mm");
            //URL to weatherstack
            string apiKey = "xxxx83e1415cee33de96d30fb";
            string cityGbg = "Gothenburg";
            string urlGbg = $"http://api.weatherstack.com/current?access_key={apiKey}&query={cityGbg}";
            var httpRequestGbg = (HttpWebRequest)WebRequest.Create(urlGbg);
            string cityWwa = "Warsaw";
            string urlWwa = $"http://api.weatherstack.com/current?access_key={apiKey}&query={cityWwa}";
            var httpRequestWwa = (HttpWebRequest)WebRequest.Create(urlWwa);

            try
            {
                //GET request to weatherstack
                httpRequestGbg.Accept = "application/json";
                var httpResponseGbg = (HttpWebResponse)httpRequestGbg.GetResponse();
                //Show json response from weatherstack
                var streamReaderGbg = new StreamReader(httpResponseGbg.GetResponseStream());
                var resultGbg = streamReaderGbg.ReadToEnd();
                //Get temperature from weatherstack
                dynamic weatherGbg = JObject.Parse(resultGbg);
                string temperatureGbg = weatherGbg.current.temperature;
                Console.WriteLine(weatherGbg);

                //GET request to weatherstack
                httpRequestWwa.Accept = "application/json";
                var httpResponseWwa = (HttpWebResponse)httpRequestWwa.GetResponse();
                //Show json response from weatherstack
                var streamReaderWwa = new StreamReader(httpResponseWwa.GetResponseStream());
                var resultWwa = streamReaderWwa.ReadToEnd();
                //Get temperature from weatherstack
                dynamic weatherWwa = JObject.Parse(resultWwa);
                string temperatureWwa = weatherWwa.current.temperature;
                Console.WriteLine(weatherWwa);

                var serviceClient = new TableServiceClient("DefaultEndpointsProtocol=https;AccountName=sttrustprod01;AccountKey=XXXXX/rHo3fkuP63bmtTmXDiV/MZQ6JpJP1oPWtdrMk+AStsyI52Q==;EndpointSuffix=core.windows.net");
                TableClient table = serviceClient.GetTableClient("weather");
                table.CreateIfNotExists();
                var tableGbg = new Azure.Data.Tables.TableEntity(cityGbg, Guid.NewGuid().ToString()) {
                    {
                        "temperature",
                        temperatureGbg
                    }, {
                        "Time",
                        timeStamp
                    }
                };
                table.AddEntity(tableGbg);
                Console.WriteLine("Added data for " + cityGbg);
                var tableWwa = new Azure.Data.Tables.TableEntity(cityWwa, Guid.NewGuid().ToString()) {
                    {
                        "temperature",
                        temperatureWwa
                    }, {
                        "Time",
                        timeStamp
                    }
                };
                table.AddEntity(tableWwa);
                Console.WriteLine("Added data for " + cityWwa);
                Console.ReadKey();
            }
            catch (Exception ex)
            {
            }
            finally { httpRequestGbg.Abort(); }
        }
    }
}
