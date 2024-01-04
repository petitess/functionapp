using namespace System.Net

# Input bindings are passed in via param block.
param($Request, $TriggerMetadata)

$UrlOpsgenie = "https://api.opsgenie.com/v2"
$ApiKeyOpsgenie = $Env:API_KEY_OPSGENIE
$HeadersOpsgenie = @{
    "Authorization" = "GenieKey $ApiKeyOpsgenie"
    "Content-type"  = "application/json"
}

$UrlB3Studio = "https://myapp.azurewebsites.net/api"
$TokenB3Studio = $Env:TOKEN_B3STUDIO
$HeadersB3Studio = @{
    "Authorization" = "Bearer $TokenB3Studio"
    "Content-type"  = "application/json"
}

$AlertId = $Request.Body.alert.alertId
"AlertId: " + $AlertId
"Action: " + $Request.Body.Action
$Message = $Request.Body.alert.message
$Message
$Contact = $null -eq $Request.Body.alert.details.Contact ? "opsgenie@b3.se" : $Request.Body.alert.details.Contact
$Category = $null -eq $Request.Body.alert.details.Category ? "Övervakning / Larm" : $Request.Body.alert.details.Category
$Description = "https://app.opsgenie.com/alert/detail/$AlertId"

switch ($Request.Body.alert.priority) {
    P1 { $PriorityId = 2 }
    P2 { $PriorityId = 1 }
    P3 { $PriorityId = 0 }
    P4 { $PriorityId = -1 }
    P5 { $PriorityId = -1 }
    Default { $PriorityId = 0 }
}

if ($Request.Body.Action -eq "Create") {
    "Slår upp CategoryId"
    try {
        $CategoryId = ((Invoke-RestMethod `
                    -Method GET `
                    -URI "$UrlB3Studio/categories" `
                    -Headers $HeadersB3Studio) | Where-Object { $_.NameWithSection -eq $Category }).CategoryID
    }
    catch {
        throw "{0} {1}" -f $_.Exception.Response.StatusCode.value__, $_.Exception.Response.ReasonPhrase
    }
    "Slår upp UserId"
    try {
        $UserId = (Invoke-RestMethod `
                -Method GET `
                -URI "$UrlB3Studio/UserByEmail?email=$Contact" `
                -Headers $HeadersB3Studio).UserId
    }
    catch {
        throw "{0} {1}" -f $_.Exception.Response.StatusCode.value__, $_.Exception.Response.ReasonPhrase
    }
    "Skapar ett ärende" 
    try {
        $TicketId = Invoke-RestMethod `
            -Method POST `
            -URI "$UrlB3Studio/ticket?categoryId=$CategoryId&body=$Description&subject=$Message&priorityId=$PriorityId&userId=$UserId" `
            -Headers $HeadersB3Studio
    }
    catch {
        throw "{0} {1}" -f $_.Exception.Response.StatusCode.value__, $_.Exception.Response.ReasonPhrase
    }
    "Updaterar ärendetyp"
    "TicketId: " + $TicketId
    try {
        Invoke-RestMethod -Method POST -URI "$UrlB3Studio/SetCustomField?ticketId=$TicketId&fieldId=10&value=Event" -Headers $HeadersB3Studio
    }
    catch {
        throw "{0} {1}" -f $_.Exception.Response.StatusCode.value__, $_.Exception.Response.ReasonPhrase
    }
    "Lägger till TicketId"
    $Body = ConvertTo-Json @{
        "details" = @{
            "TicketId" = $TicketId
        }
    }
    try {
        Invoke-RestMethod -Method POST -URI "$UrlOpsgenie/alerts/$AlertId/details" -Headers $HeadersOpsgenie -Body $Body
    }
    catch {
        throw "{0} {1}" -f $_.Exception.Response.StatusCode.value__, $_.Exception.Response.ReasonPhrase
    }
} 

else {
    $TicketId = $Request.Body.alert.details.TicketId
    $User = $Request.Body.alert.username
    switch ($Request.Body.Action) {
        UpdatePriority { $Comment = "Priority was updated by {0}" -f $User }
        RemoveTags { $Comment = "Tag was removed by {0}" -f $User }
        Escalate { $Comment = "Alert was escalated" }
        AddNote { $Comment = "Note was added by {0}" -f $User }
        AddTags { $Comment = "Tag was added by {0}" -f $User }
        AddResponder { $Comment = "Responder {0} was added" -f $Request.Body.alert.responder }
        AssignOwnership { $Comment = "Alert was assigned to {0}" -f $Request.Body.alert.owner }
        TakeOwnership { $Comment = "Ownership was taken by {0}" -f $User }
        UpdateDescription { $Comment = "Alert description was updated by {0}" -f $User }
        UpdateMessage { $Comment = "Alert title was updated by {0}" -f $User }
        SnoozeEnded { $Comment = "Alert snooze ended" }
        EscalateToNext { $Comment = "Alert was escalated to next" }
        Default { $Comment = "Alert was {0}d by {1}" -f $Request.Body.Action.ToLower() , $User }
    }
    try {
        Invoke-RestMethod -Method POST -URI "$UrlB3Studio/comment?id=$TicketId&body=$Comment" -Headers $HeadersB3Studio
    }
    catch {
        throw "{0} {1}" -f $_.Exception.Response.StatusCode.value__, $_.Exception.Response.ReasonPhrase
    }
    if ($Request.Body.Action -in "Acknowledge", "UnAcknowledge", "AssignOwnership", "TakeOwnership") {
        "Tilldelar ärendet"
        $Email = $Request.Body.Action -eq "AssignOwnership" ? $Request.Body.alert.owner : $Request.Body.alert.username

        switch ($Request.Body.Action) {
            UnAcknowledge { $Email = $null }
            AssignOwnership { $Email = $Request.Body.alert.owner }
            Default { $Email = $User }
        }

        "Slår upp UserId"
        try {
            $UserId = (Invoke-RestMethod `
                    -Method GET `
                    -URI "$UrlB3Studio/UserByEmail?email=$Email" `
                    -Headers $HeadersB3Studio).UserId
            $null -eq $UserId ? $($UserId = 0) : $UserId
        }
        catch {
            throw "{0} {1}" -f $_.Exception.Response.StatusCode.value__, $_.Exception.Response.ReasonPhrase
        }
        try {
            Invoke-RestMethod -Method POST -URI "$UrlB3Studio/UpdateTicket?id=$TicketId&assignedUserId=$UserId" -Headers $HeadersB3Studio
        }
        catch {
            throw "{0} {1}" -f $_.Exception.Response.StatusCode.value__, $_.Exception.Response.ReasonPhrase
        }
    }
    if ($Request.Body.Action -eq "Close") {
        "Stänger ärendet"
        try {
            Invoke-RestMethod -Method POST -URI "$UrlB3Studio/UpdateTicket?id=$TicketId&statusId=3" -Headers $HeadersB3Studio
        }
        catch {
            throw "{0} {1}" -f $_.Exception.Response.StatusCode.value__, $_.Exception.Response.ReasonPhrase
        }
    }
}