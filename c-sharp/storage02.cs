using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs;
using System.Linq;
using System.Net;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.OpenApi.Models;

namespace UploadFile3
{
    public static class Function1
    {
        [FunctionName("UploadFile")]
        [OpenApiOperation(operationId: "run", tags: new[] { "multipartformdata" }, Summary = "Transfer file through multipart/formdata", Description = "This transfers a file through multipart/formdata.", Visibility = OpenApiVisibilityType.Advanced)]
        //[OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
        [OpenApiParameter(name: "container", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "Container Name")]
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

            var connectionString = "DefaultEndpointsProtocol=https;AccountName=sttrust01;AccountKey=ypfFnbb63bmtTmXDiV/MZQ6JpJP1oPWtdrMk+AStsyI52Q==;EndpointSuffix=core.windows.net";

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
    <DockerFastModeProjectMountDirectory>/home/site/wwwroot</DockerFastModeProjectMountDirectory>
    <DockerDefaultTargetOS>Linux</DockerDefaultTargetOS>
  </PropertyGroup>
  <ItemGroup>
	<PackageReference Include="Aspire.Azure.Storage.Blobs" Version="8.1.0" />
	<PackageReference Include="Microsoft.Azure.WebJobs.Extensions.OpenApi" Version="1.5.1" />
    <PackageReference Include="Microsoft.NET.Sdk.Functions" Version="4.4.0" />
    <PackageReference Include="Microsoft.VisualStudio.Azure.Containers.Tools.Targets" Version="1.21.0" />
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
