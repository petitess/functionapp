param($Timer)

$Timestamp = Get-Date -Format "yyyy-MM-dd"

$User = 'x1111'
$Pass = $Env:PASS_ADMIN_DSTNY
$Domain = "xxx.se"
$Permissions = "CONTACT:DISTRIBUTION_GROUP"

$String1 = "$($User):$($Domain):$($Pass)"
$String1MD5 = ([System.BitConverter]::ToString((New-Object -TypeName System.Security.Cryptography.MD5CryptoServiceProvider).ComputeHash((New-Object -TypeName System.Text.UTF8Encoding).GetBytes($String1)))).Replace("-", "")

$String2 = "D$([Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($Domain)))"
$String2Encode64 = $String2.Replace("=", "")

$String3 = $User + ":" + $Permissions + ":" + $String1MD5.ToLower()
$String3MD5 = ([System.BitConverter]::ToString((New-Object -TypeName System.Security.Cryptography.MD5CryptoServiceProvider).ComputeHash((New-Object -TypeName System.Text.UTF8Encoding).GetBytes($String3)))).Replace("-", "")

$String4 = "P:" + $String3MD5.ToLower() + ":" + $user + ":" + $Permissions
$String4Encode = "$([Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($String4)))"
$String4Encode64 = $String4Encode.Replace("=", "")

$PassToken = $String2Encode64 + "." + $String4Encode64

$AlloweApis = "api=CONTACT&api=DISTRIBUTION_GROUP"
$TokenName = "func-xxx-prod-01_$Timestamp"
$headers = @{
    "Accept" = "application/json"
}

try {
    $I = (Invoke-RestMethod -Method POST `
            -URI "https://bc.dstny.se/api/tickets/$Domain/$($User)?platform=other&$AlloweApis&name=$TokenName&t=$PassToken" `
            -Headers $headers)
}
catch {
    throw "{0} {1}" -f $_.Exception.Response.StatusCode.value__, $_.Exception.Response.ReasonPhrase
}

"Updated: $($I.name)"
"Expires: $($I.expires)"
"Allowed APIs: $($I.allowedApis)"

$resourceURI = "https://management.azure.com"
$tokenAuthURI = $env:IDENTITY_ENDPOINT + "?resource=$resourceURI&api-version=2019-08-01"
$tokenResponse = Invoke-RestMethod -Method Get -Headers @{"X-IDENTITY-HEADER" = "$env:IDENTITY_HEADER" } -Uri $tokenAuthURI
$AzureToken = $tokenResponse.access_token

$subscriptionId = "ec89d8fd-fe48-421d-ba3f-6181fdd00b69"
$RessourceGroupName = "rg-func-xxx-prod-01"
$KvName = "kv-xxx-prod-01"
$SecretName = "api-key-dstny"
$ApiVersion = "2023-07-01"
$URL = "https://management.azure.com/subscriptions/$subscriptionId/resourceGroups/$RessourceGroupName/providers/Microsoft.KeyVault/vaults/$KvName/secrets/$($SecretName)?api-version=$ApiVersion"
$headers = @{
    "Authorization" = "Bearer $AzureToken"
    "Content-type"  = "application/json"
}
$Body = ConvertTo-Json @{
    properties = @{
        value       = $I.token
        contentType = "$User updated $(Get-Date -Format "dd-MM-yyy HH:mm")"
    }
}
try {
    Invoke-RestMethod -Method PUT -URI $URL -Headers $headers -Body $Body
    $I.properties.secretUri
}
catch {
    throw "{0} {1}" -f $_.Exception.Response.StatusCode.value__, $_.Exception.Response.ReasonPhrase
}


