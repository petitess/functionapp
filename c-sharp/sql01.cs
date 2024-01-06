using System;
using System.IO;
using System.Net;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using System.Data.SqlClient;

namespace WeatherStatus
{
    public class Function1
    {

        [FunctionName("SendWeatherToAzureSql")]//"0 5,11,14 * * *"
        public async Task Run([TimerTrigger("0 5,11,14 * * *")] TimerInfo myTimer, ILogger log)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

            DateTime time = DateTime.Now;
            string timeStamp = time.AddHours(1).ToString("dd/MM/yyyy HH:mm");
            //URL to weatherstack
            string apiKey = "xxx";

            string cityGbg = "Gothenburg";
            string urlGbg = $"http://api.weatherstack.com/current?access_key={apiKey}&query={cityGbg}";
            var httpRequestGbg = (HttpWebRequest)WebRequest.Create(urlGbg);

            string cityWwa = "Warsaw";
            string urlWwa = $"http://api.weatherstack.com/current?access_key={apiKey}&query={cityWwa}";
            var httpRequestWwa = (HttpWebRequest)WebRequest.Create(urlWwa);

            //Azure SQL
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();
            builder.DataSource = "sql-xxx-prod-01.database.windows.net";
            builder.UserID = "azadmin";
            builder.Password = "xxx";
            builder.InitialCatalog = "sqldb-weather-01";

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
                int temperatureGbg = weatherGbg.current.temperature;
                string descriptionGbg = weatherGbg.current["weather_descriptions"][0];
                string timeGbg = weatherGbg.location["localtime"];
                string timezoneGbg = weatherGbg.location["timezone_id"];
                Console.WriteLine(weatherGbg);

                //GET request to weatherstack
                httpRequestWwa.Accept = "application/json";
                var httpResponseWwa = (HttpWebResponse)httpRequestWwa.GetResponse();
                //Show json response from weatherstack
                var streamReaderWwa = new StreamReader(httpResponseWwa.GetResponseStream());
                var resultWwa = streamReaderWwa.ReadToEnd();
                //Get temperature from weatherstack
                dynamic weatherWwa = JObject.Parse(resultWwa);
                int temperatureWwa = weatherWwa.current.temperature;
                string descriptionWwa = weatherWwa.current["weather_descriptions"][0];
                string timeWwa = weatherGbg.location["localtime"];
                string timezoneWwa = weatherWwa.location["timezone_id"];
                Console.WriteLine(weatherWwa);

                //Azure SQL
                using (SqlConnection connection = new SqlConnection(builder.ConnectionString))
                {
                    String sqlGbg = @$"USE [sqldb-weather-01]
                                    INSERT WeatherInfo 
                                (
                                    [City],
                                    [Temperature],
                                    [Description],
                                    [Localtime],
                                    [TimeZone]
                                ) VALUES
                                (
                                '{cityGbg}',
                                {temperatureGbg},
                                '{descriptionGbg}',
                                '{timeStamp}',
                                '{timezoneGbg}'
                                )";
                    String sqlWwa = @$"USE [sqldb-weather-01]
                                    INSERT WeatherInfo 
                                (
                                    [City],
                                    [Temperature],
                                    [Description],
                                    [Localtime],
                                    [TimeZone]
                                ) VALUES
                                (
                                '{cityWwa}',
                                {temperatureWwa},
                                '{descriptionWwa}',
                                '{timeStamp}',
                                '{timezoneWwa}'
                                )";

                    using (SqlCommand command = new SqlCommand(sqlGbg + sqlWwa, connection))
                    {
                        connection.Open();

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                Console.WriteLine(reader[0] + " " + reader[1] + " " + reader[2]);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
            }
            finally { httpRequestGbg.Abort(); }
        }
    }
}
