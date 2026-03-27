using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using System.Net;
using Azure.Identity;

namespace FunctionApi;

public class GetFunc
{
    private readonly ILogger<GetFunc> _logger;

    public GetFunc(ILogger<GetFunc> logger)
    {
        _logger = logger;
    }

    [Function("GetFunc")]
    [OpenApiOperation(operationId: "GetItems", tags: new[] { "Items" }, Summary = "Get items", Description = "Returns a list of items")]
    [OpenApiResponseWithBody(statusCode: System.Net.HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(IEnumerable<object>), Description = "The list of items")]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req)
    {
        _logger.LogInformation("Getting info...");
        var items = new List<object>
        {
            new { name = "item1", id = 1, date = DateTime.Now },
            new { name = "item2", id = 2, date = DateTime.Now },
            new { name = "item3", id = 3, date = DateTime.Now },
            new { name = "item4", id = 4, date = DateTime.Now },
            new { name = "item5", id = 5, date = DateTime.Now }
        };

        return new OkObjectResult(items);
    }

    [Function("UploadFile")]
    [OpenApiOperation(operationId: "run", tags: new[] { "multipartformdata" }, Summary = "Transfer file through multipart/formdata", Description = "This transfers a file through multipart/formdata.", Visibility = OpenApiVisibilityType.Advanced)]
    [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
    [OpenApiParameter(name: "container", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "Container Name")]
    [OpenApiRequestBody(contentType: "multipart/form-data", bodyType: typeof(MultiPartFormDataModel), Required = true, Description = "File data")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "multipart/form-data", bodyType: typeof(byte[]), Summary = "File data", Description = "This returns the file", Deprecated = false)]
    public static async Task<string> RunUpload(
                 [HttpTrigger(AuthorizationLevel.Anonymous, "POST", Route = "multipart")] HttpRequest req,
                 ILogger log)
    {
        var files = req.Form.Files;
        var file = files[0];

        string accountName = "stapimdev01";

        string containerName = req.Query["container"].FirstOrDefault("container01");
        //var credential = new DefaultAzureCredential().GetTokenAsync(new TokenRequestContext(new[] { "https://storage.azure.com/" }));
        var credential = new DefaultAzureCredential();

        var blobServiceClient = new BlobServiceClient(new Uri($"https://{accountName}.blob.core.windows.net"), credential);
        // Create a blob container if it doesn't exist
        var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        //await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

        // Delete the existing blob if it exists
        var blobClient = containerClient.GetBlobClient(file.FileName);
        await blobClient.DeleteIfExistsAsync();

        // Upload the new blob
        using var fileStream = file.OpenReadStream();
        await blobClient.UploadAsync(
            fileStream,
            new BlobHttpHeaders { ContentType = file.ContentType });

        // Return the URI of the uploaded blob
        return blobClient.Uri.ToString();
    }

    [Function("UploadContent")]
    [OpenApiOperation(operationId: "run", tags: new[] { "content" }, Summary = "Transfer content", Description = "This transfers content.", Visibility = OpenApiVisibilityType.Advanced)]
    [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
    public static async Task<string> RunUploadContent(
                 [HttpTrigger(AuthorizationLevel.Anonymous, "POST", Route = "content")] HttpRequest req,
                 ILogger log)
    {

        string accountName = "stapimdev01";

        string containerName = req.Query["container"].FirstOrDefault("container01");
        //var credential = new DefaultAzureCredential().GetTokenAsync(new TokenRequestContext(new[] { "https://storage.azure.com/" }));
        var credential = new DefaultAzureCredential();

        var blobServiceClient = new BlobServiceClient(new Uri($"https://{accountName}.blob.core.windows.net"), credential);
        // Create a blob container if it doesn't exist
        var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        //await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

        // Delete the existing blob if it exists
        var blobClient = containerClient.GetBlobClient("FileContent.txt");
        await blobClient.DeleteIfExistsAsync();

        // Upload the new blob
        var content = "Hello from ConsoleApp at " + DateTime.UtcNow;
        await blobClient.UploadAsync(
            BinaryData.FromString(content),
             overwrite: true);

        // Return the URI of the uploaded blob
        return blobClient.Uri.ToString();
    }

    public class MultiPartFormDataModel
    {
        public byte[]? FileUpload { get; set; }
    }
}
