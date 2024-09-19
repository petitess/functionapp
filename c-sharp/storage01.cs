using System.IO;
using System.Net;
using System.Threading.Tasks;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using System.Linq;

namespace UploadFile
{
    public class Function1
    {
        private readonly ILogger<Function1> _logger;

        public Function1(ILogger<Function1> log)
        {
            _logger = log;
        }

        [FunctionName("UploadFile")]
        [OpenApiOperation(operationId: "run", tags: new[] { "multipartformdata" }, Summary = "Transfer file through multipart/formdata", Description = "This transfers a file through multipart/formdata.", Visibility = OpenApiVisibilityType.Advanced)]
        //[OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
        [OpenApiParameter(name: "container", In = ParameterLocation.Query , Required = false, Type = typeof(string), Description = "Container Name")]
        [OpenApiRequestBody(contentType: "multipart/form-data", bodyType: typeof(MultiPartFormDataModel), Required = true, Description = "File data")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "multipart/form-data", bodyType: typeof(byte[]), Summary = "File data", Description = "This returns the file", Deprecated = false)]
        public static async Task<string> Run(
                 [HttpTrigger(AuthorizationLevel.Anonymous, "POST", Route = "multipart")] HttpRequest req,
                 ILogger log)
        {
            var files = req.Form.Files;
            var file = files[0];

            var content = default(byte[]);
            using (var ms = new MemoryStream())
            {
                await file.CopyToAsync(ms).ConfigureAwait(false);
                content = ms.ToArray();
            }

            var result = new FileContentResult(content, "multipart/form-data");

            var connectionString = "DefaultEndpointsProtocol=https;AccountName=sttrust01;AccountKey=ypfFnbbBP1oPWtdrMk+AStsyI52Q==;EndpointSuffix=core.windows.net";

            string containerName = req.Query["container"].FirstOrDefault("container01");
            var blobServiceClient = new BlobServiceClient(connectionString);
            // Create a blob container if it doesn't exist
            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

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
    }
    public class MultiPartFormDataModel
    {
        public byte[] FileUpload { get; set; }
    }
}

/*
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <AzureFunctionsVersion>v4</AzureFunctionsVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Aspire.Azure.Storage.Blobs" Version="8.1.0" />
    <PackageReference Include="Microsoft.Azure.WebJobs.Extensions.OpenApi" Version="1.5.1" />
    <PackageReference Include="Microsoft.NET.Sdk.Functions" Version="4.4.1" />
  </ItemGroup>
  <ItemGroup>
    <None Update="host.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
    <None Update="local.settings.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
      <CopyToPublishDirectory>Never</CopyToPublishDirectory>
    </None>
  </ItemGroup>
</Project> 
*/

