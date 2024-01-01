### Login using FTP
- Go to function > Overview > Get Publish Profile
- Find FTP credentials
    - Server: waws-prod-xxx-xxx.ftp.azurewebsites.windows.net
    - User: func-xxx-prod-01\$func-xxx-prod-01
- Login with WinSCP
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
