func new --template "Http Trigger" --name trigger-http-b3studio-01 --authlevel anonymous

func azure functionapp publish func-grf-jb-prod-01 --force

func start

### Trigger function app
```powershell
$funcTrigger = @{
    alert_group_id = "I8YEE3VVDATL4"
    event          = @{
        type = "alert group created" #"resolve" #"acknowledge" # "unacknowledge" 
        time = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    }
    alert_group    = @{
        id               = "I8YEE3VVDATL4"
        integration_id   = "CT3W19EDXV3H5"
        title            = "[A3] api-alert-pwsh"
        labels           = @{
            "Cat"      = "Help - Larm"
            "Customer" = "A3"
        }
            
        resolution_notes = @(
            @{
                id         = "MM3869IGM1L73"
                author     = "UAY22ELM1ES43"
                created_at = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
                text       = "hej"
            }
            @{
                id         = "MWJ44H6IT8BID"
                author     = $null
                created_at = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
                text       = "260201"
            })
    }
    user           = @{
        id       = "UAY22ELM1ES43"
        username = "grafana"
        email    = "grafana@a3.se"
    }
    alert_payload     = @{
        data           = @{
            essentials = @{
                alertId          = "/subscriptions/blablabla"
                alertRule        = "api-alert-pwsh"
                alert_uid        = "unique-alert-id-20260214164239"
                alertRuleID      = "blbla"
                description      = "This is a test alert from PowerShell"
                monitorCondition = "Fired"
            }
        }
    }

} | ConvertTo-Json -Depth 10

# $response = Invoke-RestMethod -Uri "http://localhost:7071/api/http_trigger" -Method Post -Body $funcTrigger
$response = Invoke-RestMethod -Uri "https://func-grf-jb-prod-01.azurewebsites.net/api/grafana_trigger?code=xyz" -Method Post -Body $funcTrigger
$response
```