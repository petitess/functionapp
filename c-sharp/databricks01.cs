using Azure.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text;
using Azure.Identity;
using Newtonsoft.Json;
using System.Net.Http.Headers;

namespace Funcdatabricks
{
    public class Function1
    {
        private readonly ILogger<Function1> _logger;

        public Function1(ILogger<Function1> logger)
        {
            _logger = logger;
        }

        [Function("Function1")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req)
        {
            AccessToken token =
                 await new DefaultAzureCredential()
                .GetTokenAsync(
                    new TokenRequestContext(
            new[] { "2ff814a6-3304-4ab8-85cb-cd0e6f879c1d/.default" } //"https://management.azure.com/.default"
            ));

            var accessToken = token.Token; 

            Console.WriteLine("Azure Token: " + accessToken.Substring(0,25));

            //GET request - databricks clusters
            string databricksClusterUrl = $"https://adb-123456789.4.azuredatabricks.net/api/2.0/clusters/list";
            string databricksAzureToken = accessToken;
            Console.WriteLine("URL: " + databricksClusterUrl);
            System.Net.Http.HttpClient databricksClusterHttpClient = new System.Net.Http.HttpClient();
            databricksClusterHttpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            databricksClusterHttpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            databricksClusterHttpClient.DefaultRequestHeaders.Add("Access-Control-Allow-Headers", "Authorization, X-Databricks-Azure-Workspace-Resource-Id, X-Databricks-Org-Id, Content-Type");
            databricksClusterHttpClient.DefaultRequestHeaders.Add("Access-Control-Allow-Origin", "*");
            databricksClusterHttpClient.DefaultRequestHeaders.Add("Cache-Control", "no-store, must-revalidate, no-cache");
            databricksClusterHttpClient.DefaultRequestHeaders.Add("Pragma", "no-cache");
            databricksClusterHttpClient.DefaultRequestHeaders.Add("x-databricks-org-id", "123456789");
            databricksClusterHttpClient.DefaultRequestHeaders.Add("Vary", "Accept-Encoding");
            databricksClusterHttpClient.DefaultRequestHeaders.Add("Strict-Transport-Security", "max-age=31536000; includeSubDomains; preload");
            databricksClusterHttpClient.DefaultRequestHeaders.Add("X-Content-Type-Options", "nosniff");
            databricksClusterHttpClient.DefaultRequestHeaders.Add("Server", "databricks");
            databricksClusterHttpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + databricksAzureToken);
            var databricksClusterResponse = await databricksClusterHttpClient.GetAsync(databricksClusterUrl);
            Console.WriteLine("IsSuccessStatusCode_Cluster_API: " + databricksClusterResponse.IsSuccessStatusCode);

            var databricksClusterJson = await databricksClusterResponse.Content.ReadAsStringAsync();
            //dynamic? clusters = JsonConvert.DeserializeObject(databricksClusterJson);

            /*            if (clusters?.clusters != null)
                        {
                            foreach (dynamic? c in clusters.clusters)
                            {
                                Console.WriteLine($"cluster_name: {c.cluster_name}, id: {c.cluster_id}, version: {c.spark_version}");
                            }
                        }*/

            //POST request - create dir
            string databricksDirUrl = $"https://adb-123456789.4.azuredatabricks.net/api/2.0/workspace/mkdirs";
            databricksAzureToken = accessToken;
            Console.WriteLine("URL: " + databricksDirUrl);
            System.Net.Http.HttpClient databricksDirHttpClient = new System.Net.Http.HttpClient();
            databricksDirHttpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            databricksDirHttpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + databricksAzureToken);

            var jsonContentDir = new StringContent(JsonConvert.SerializeObject(new
            {

                path = "/FolderC"


            }), Encoding.UTF8, "application/json");

            var databricksDirResponse = await databricksDirHttpClient.PostAsync(databricksDirUrl, jsonContentDir);
            Console.WriteLine("IsSuccessStatusCode: " + databricksDirResponse.IsSuccessStatusCode);

            var databricksDirJson = await databricksDirResponse.Content.ReadAsStringAsync();
            dynamic? dir = JsonConvert.DeserializeObject(databricksDirJson);

            Console.WriteLine("dir: " + dir);


            //POST request - databricks repo
            string databricksRepoUrl = $"https://adb-123456789.4.azuredatabricks.net/api/2.0/repos";
            databricksAzureToken = accessToken;
            Console.WriteLine("URL: " + databricksRepoUrl);
            System.Net.Http.HttpClient databricksRepoHttpClient = new System.Net.Http.HttpClient();
            databricksRepoHttpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            databricksRepoHttpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + databricksAzureToken);

            string GitHubToken = "ghp_xZviZR0AMD74";
            string GithubUrlRepo = $"https://{GitHubToken}@github.com/X-APPLICATIONS/x-notebooks.git";
            string DatabricksMainFolder = "/NotebooksC";
            var jsonContent = new StringContent(JsonConvert.SerializeObject(new
            {

                url = GithubUrlRepo,
                path = DatabricksMainFolder,
                provider = "gitHub"


            }), Encoding.UTF8, "application/json");

            var databricksRepoResponse = await databricksRepoHttpClient.PostAsync(databricksRepoUrl, jsonContent);
            Console.WriteLine("IsSuccessStatusCode: " + databricksRepoResponse.IsSuccessStatusCode);

            var databricksRepoJson = await databricksRepoResponse.Content.ReadAsStringAsync();
            dynamic? repo = JsonConvert.DeserializeObject(databricksRepoJson);

            Console.WriteLine("repo_id: " + repo?.id);

            _logger.LogInformation("C# HTTP trigger function processed a request.");
            return new OkObjectResult($"AzToken: {accessToken.Substring(0, 25)} " + "\n" + databricksClusterJson + "dir: " + dir + "repo_id: " + repo?.id);
        }
    }
}
