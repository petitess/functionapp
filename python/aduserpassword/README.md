
#### Install the Azure Functions Core Tools

https://learn.microsoft.com/en-us/azure/azure-functions/functions-core-tools-reference?tabs=v2

#### Azure Functions triggers and bindings

https://learn.microsoft.com/en-us/azure/azure-functions/functions-triggers-bindings?tabs=isolated-process%2Cnode-v4%2Cpython-v2&pivots=programming-language-python

#### Create a new function
```py
func templates list
func new --template "Timer trigger" --name http_trigger --authlevel anonymous
pip install -r requirements.txt
func start
func azure functionapp publish func-aduser-prod-01 --force
```

#### NSG
```yaml
Function app (out) > Storage Account (Pep)
Function app (out) > Resource management private link (Pep, management.azure.com)
Storage Account (Pep) > Function app (out)
```
#### RBAC for function app
```yaml
Virtual Machine Contributor on rg-vmmgmtprod01
Storage Table Data Contributor on Storage account
Monitoring Metrics Publisher on Application Insights
```
#### Graph API permission for function app
```pwsh
##As Global Admin
$TenantId = "abcd"
Connect-MgGraph -TenantId $TenantId -Scopes 'Application.Read.All', 'AppRoleAssignment.ReadWrite.All'

$serverApplicationName = "func-abcd-prod-01"
$serverServicePrincipal = (Get-MgServicePrincipal -Filter "DisplayName eq '$serverApplicationName'")
$serverServicePrincipalObjectId = $serverServicePrincipal.Id

$appRoleId = ((Get-MgServicePrincipal -Filter "appId eq '00000003-0000-0000-c000-000000000000'").AppRoles | Where-Object { $_.Value -eq "Mail.Send" }).Id
$mGraphId = (Get-MgServicePrincipal -Filter "appId eq '00000003-0000-0000-c000-000000000000'").Id

New-MgServicePrincipalAppRoleAssignment `
    -ServicePrincipalId $serverServicePrincipalObjectId `
    -PrincipalId $serverServicePrincipalObjectId `
    -ResourceId $mGraphId `
    -AppRoleId $appRoleId
```
#### Script on the virtual machine
```pwsh
#Prerequisites:
#Assign Storage Table Data Contributor for VM's system assigned identity
#Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
#Install-Module -Name Az -Scope AllUsers -Repository PSGallery -Force

$C = Connect-AzAccount -Identity -Subscription "sub-abcd-prod-01"
$C.Context.Subscription.Name
$storageAccountName = 'storageprodabcd01' 
$tableName = 'userpasswordexpiration'
$partitionKey = 'passwordexpiration'
$users = @()

#Clear table
$Token = (Get-AzAccessToken -ResourceUrl 'https://storage.azure.com/').Token
$URL = "https://$storageAccountName.table.core.windows.net/$tableName"
$Date = Get-Date (Get-Date).ToUniversalTime() -Format 'R'
$headers = @{
    "Authorization" = "Bearer $Token"
    "x-ms-date"     = $Date
    "x-ms-version"  = "2020-04-08"
    "Accept"        = "application/json;odata=fullmetadata"
}
$I = Invoke-RestMethod -Method GET -URI $URL -Headers $headers

$I.value.'odata.id' | ForEach-Object {
    $URL = $_
    $Date = Get-Date (Get-Date).ToUniversalTime() -Format 'R'
    $headers = @{
        "Authorization" = "Bearer $Token"
        "x-ms-date"     = $Date
        "Content-type"  = "application/json"
        "x-ms-version"  = "2020-04-08"
        "Accept"        = "application/json;odata=fullmetadata"
        "If-Match"      = "*"
    }
    if ($null -ne $URL) {
        $I = Invoke-RestMethod -Method Delete -URI $URL -Headers $headers #-Body $Body
    }
}

#get the data 
$users = Get-ADUser -filter { Enabled -eq $True -and PasswordNeverExpires -eq $False } -Properties UserPrincipalName, msDS-UserPasswordExpiryTimeComputed, mail, name | `
    Where-Object { $_."msDS-UserPasswordExpiryTimeComputed" -notmatch "92233720" } | `
    Where-Object { [datetime]::FromFileTime($_."msDS-UserPasswordExpiryTimeComputed") -lt (Get-Date).AddDays(7) -and [datetime]::FromFileTime($_."msDS-UserPasswordExpiryTimeComputed") -ge (Get-Date) -and $null -ne $_.mail } | `
    Select-Object -Property "Name", "UserPrincipalName", "mail", @{Name = "ExpiryDate"; Expression = { [datetime]::FromFileTime($_."msDS-UserPasswordExpiryTimeComputed") } }

foreach ($user in $users) {
    #Create user row
    $URL = "https://$storageAccountName.table.core.windows.net/$tableName"
    $Date = Get-Date (Get-Date).ToUniversalTime() -Format 'R'
    $Body = ConvertTo-Json @{
        "PartitionKey"      = $partitionKey
        "RowKey"            = ([guid]::NewGuid().tostring())
        "WriteTime"         = (Get-Date -Format "yyyyMMdd-HHmm")
        "UserPrincipalName" = $user.UserPrincipalName
        "Name"              = $user.Name
        "Mail"              = $user.mail
        "ExpiryDate"        = $user.ExpiryDate.ToString("yyyy-MM-dd")
    }
    $headers = @{
        "Authorization"  = "Bearer $Token"
        "x-ms-date"      = $Date
        "Content-type"   = "application/json"
        "Content-Length" = $Body.Length
        "x-ms-version"   = "2020-04-08"
        "Accept"         = "application/json;odata=fullmetadata"
    }
    $I = Invoke-RestMethod -Method POST -URI $URL -Headers $headers -Body $Body
}

New-Item -Path "C:\Script\AdPasswordExpiration.txt" -Value "AdPasswordExpiration.ps1 run successfully: $(Get-Date -Format 'yyyy-MM-dd HH:mm')" -Force
```
#### Application setting
```sql
appSettings: {
      APPLICATIONINSIGHTS_AUTHENTICATION_STRING: 'Authorization=AAD'
      APPLICATIONINSIGHTS_CONNECTION_STRING: 'InstrumentationKey=abcdefg...'
      AzureWebJobsStorage__blobServiceUri: 'https://storageprodabcd01.blob.core.windows.net'
      AzureWebJobsStorage__credential: 'managedidentity'
      AzureWebJobsStorage__queueServiceUri: 'https://storageprodabcd01.queue.core.windows.net'
      AzureWebJobsStorage__tableServiceUri: 'https://storageprodabcd01.table.core.windows.net'
      BUILD_FLAGS: 'UseExpressBuild'
      ENABLE_ORYX_BUILD: 'true'
      FUNCTIONS_EXTENSION_VERSION: '~4'
      FUNCTIONS_WORKER_RUNTIME: 'python'
      SCM_DO_BUILD_DURING_DEPLOYMENT: '1'
      XDG_CACHE_HOME: '/tmp/.cache'
    }
```