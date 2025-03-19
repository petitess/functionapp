### Logs

```pwsh
Get-Content C:\home\LogFiles\Application\Functions\Host\2024-01-01T10-12-13Z-63b2546981.log | Select-Object -Last 50
```

### Login using FTP
- Go to function > Overview > Get Publish Profile
- Find FTP credentials
    - Server: waws-prod-xxx-xxx.ftp.azurewebsites.windows.net
    - User: func-xxx-prod-01\$func-xxx-prod-01
- Login with WinSCP
    - FTP
    - SSL/TLS Implicit
    - Port 990  
### Devops agent
##### Download
```pwsh
(New-Object System.Net.WebClient).DownloadFile("https://vstsagentpackage.azureedge.net/agent/3.232.0/vsts-agent-win-x64-3.232.0.zip", "agent.zip")
```
##### Unzip
```pwsh
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::ExtractToDirectory("C:\home\agent.zip", "C:\home\devops_agent")
```
##### Install
```pwsh
.\config.cmd --unattended --url  "https://dev.azure.com/xxx" --auth "pat" --token "xxxgtrzr5zgnvcdx47rudliearurfedxa" --pool "home_pc" --agent "func-cons-prod-01" --work "_work" 
.\config.cmd remove --auth "pat" --token "xxxx5c7xgtrzr5zgnvcdx47rudliearurfedxa"
.\run.cmd
```
### Add swagger 
1. Install `Microsoft.Azure.Functions.Worker.Extensions.OpenApi`
2. In Program.cs add `var host = new HostBuilder().ConfigureOpenApi()`
3. In Function1.cs add
```cs
[Function("Function1")]
[OpenApiOperation(operationId: "Run", tags: new[] { "name" })]
[OpenApiParameter(name: "name", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "The **Name** parameter")]
[OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "The OK response")]
public HttpResponseData Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req)
{
    _logger.LogInformation("C# HTTP trigger function processed a request.");
    string? name = req.Query["name"];
    var response = req.CreateResponse(HttpStatusCode.OK);
    response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
    response.WriteString($"Welcome to Azure Functions, {name}!");
    return response;
}
```
```cs
var host = new HostBuilder().ConfigureOpenApi()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
    })
    .Build();
```

