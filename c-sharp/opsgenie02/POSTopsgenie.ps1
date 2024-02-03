#####Create Alert
$URL = " http://localhost:7158/api/trigger-http-opsgenie"
$headers = @{
}
$body = @"
{
    "action": "Create",
    "alert": {
      "alertId": "ff89fa96-dd5d-4c62-ac2c-4b25b90xx7a1-1706958506428",
      "message": "ALERT TESTING API",
      "tags": [
        "OverwriteQuietHours",
        "SuppressAlertNotification",
      ],
      "tinyId": "45352",
      "entity": "entity1",
      "alias": "ALIAS1",
      "createdAt": 1703849509773,
      "updatedAt": 1703849510592000000,
      "username": "Alert API",
      "description": "description1",
      "team": "Operations",
      "responders": [
        {
          "id": "xxxxx",
          "type": "team",
          "name": "Operations"
        }
      ],
      "teams": [
        "xxxxx"
      ],
      "actions": [],
      "addedTags": "OverwriteQuietHours,SuppressAlertNotification,SuppressTicketCreation",
      "details": {
        "Category": "xxCare / Övervakning - Larm",
        "Contact": "opsgenie-xxcare@xx.se",
        "Customer": "xxCare"
      },
      "priority": "P5",
      "source": "192.168.0.1"
    },
    "source": {
      "name": "192.168.0.1",
      "type": "API"
    },
    "integrationName": "Webhook01",
    "integrationId": "fb03dcd1-xx35-4e4b-ade6-08f3909e3b92",
    "integrationType": "Webhook"
  }
"@
Invoke-RestMethod -Method POST -URI $URL -Headers $headers -Body $body

#####Acknowledge Alert
$URL = " http://localhost:7158/api/trigger-http-opsgenie"
$headers = @{}
$body = @"
{
    "action": "UnAcknowledge",
    "alert": {
      "alertId": "ff89fa96-dd5d-4c62-ac2c-4b25b90xx7a1-1706958506428",
      "message": "ALERT TESTING API",
      "tags": [
        "OverwriteQuietHours",
        "SuppressAlertNotification",
      ],
      "tinyId": "45352",
      "entity": "entity1",
      "alias": "ALIAS1",
      "createdAt": 1703849509773,
      "updatedAt": 1703849510592000000,
      "username": "karol.sek@xx.se",
      "description": "description1",
      "team": "Operations",
      "responders": [
        {
          "id": "xxxxx",
          "type": "team",
          "name": "Operations"
        }
      ],
      "teams": [
        "xxxxx"
      ],
      "actions": [],
      "addedTags": "OverwriteQuietHours,SuppressAlertNotification,SuppressTicketCreation",
      "details": {
        "Category": "xxCare / Övervakning - Larm",
        "Contact": "opsgenie-xxcare@xx.se",
        "Customer": "xxCare",
        "TicketId": "175697"
      },
      "priority": "P5",
      "source": "192.168.0.1"
    },
    "source": {
      "name": "192.168.0.1",
      "type": "API"
    },
    "integrationName": "Webhook01",
    "integrationId": "fb03dcd1-xx35-4e4b-ade6-08f3909e3b92",
    "integrationType": "Webhook"
  }
"@
Invoke-RestMethod -Method POST -URI $URL -Headers $headers -Body $body


#####Close Alert
$URL = " http://localhost:7158/api/trigger-http-opsgenie"
$headers = @{}
$body = @"
{
    "action": "Close",
    "alert": {
      "alertId": "ff89fa96-dd5d-4c62-ac2c-4b25b90xx7a1-1706958506428",
      "message": "ALERT TESTING API",
      "tags": [
        "OverwriteQuietHours",
        "SuppressAlertNotification",
      ],
      "tinyId": "45352",
      "entity": "entity1",
      "alias": "ALIAS1",
      "createdAt": 1703849509773,
      "updatedAt": 1703849510592000000,
      "username": "karol.sek@xx.se",
      "description": "description1",
      "team": "Operations",
      "responders": [
        {
          "id": "xxxxx",
          "type": "team",
          "name": "Operations"
        }
      ],
      "teams": [
        "xxxxx"
      ],
      "actions": [],
      "addedTags": "OverwriteQuietHours,SuppressAlertNotification,SuppressTicketCreation",
      "details": {
        "Category": "xxCare / Övervakning - Larm",
        "Contact": "opsgenie-xxcare@xx.se",
        "Customer": "xxCare",
        "TicketId": "175697"
      },
      "priority": "P5",
      "source": "192.168.0.1"
    },
    "source": {
      "name": "192.168.0.1",
      "type": "API"
    },
    "integrationName": "Webhook01",
    "integrationId": "fb03dcd1-xx35-4e4b-ade6-08f3909e3b92",
    "integrationType": "Webhook"
  }
"@
Invoke-RestMethod -Method POST -URI $URL -Headers $headers -Body $body