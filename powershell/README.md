#### Repository content
Name | Description 
--|--
basic01 | http & timer function
opsgenie01 | dstny + ticketing system integration

#### Managed identity token (without Az module)
```pwsh
$resourceURI = "https://management.azure.com"
$tokenAuthURI = $env:IDENTITY_ENDPOINT + "?resource=$resourceURI&api-version=2019-08-01"
$tokenResponse = Invoke-RestMethod -Method Get -Headers @{"X-IDENTITY-HEADER"="$env:IDENTITY_HEADER"} -Uri $tokenAuthURI
$AzureToken = $tokenResponse.access_token
```
