using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;

namespace func_opsgenie_02
{
    public class Function1
    {
        private readonly ILogger<Function1> _logger;

        public Function1(ILogger<Function1> logger)
        {
            _logger = logger;
        }

        [Function("trigger-http-opsgenie")]
        //public IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req)
        public async Task<IActionResult> RunAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            //Parse JSON
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic opsgenieData = JObject.Parse(requestBody);

            _logger.LogWarning($"action: {opsgenieData.action}");
            _logger.LogWarning($"alertId: {opsgenieData.alert.alertId}");
            _logger.LogWarning($"title: {opsgenieData.alert.message}");

            string alertId = opsgenieData.alert.alertId;
            string category = opsgenieData.alert.details.Category;
            string contact = string.IsNullOrEmpty((opsgenieData.alert.details.Contact).ToString()) ? "opsgenie@xx.se" : (opsgenieData.alert.details.Contact).ToString();
            string description = $"https://app.opsgenie.com/alert/detail/{opsgenieData.alert.alertId}";
            string message = opsgenieData.alert.message;

            //jitbit info
            string jitbitToken = "X";
            string jitbitBaseUrl = $"https://x.azurewebsites.net/api";
            var jitbitClient = new HttpClient();
            jitbitClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + jitbitToken);

            //GET Opsgenie info
            string opsgenieBaseUrl = "https://api.opsgenie.com/v2";
            string opsgenieApiKey = "X";
            var opsgenieClient = new HttpClient();
            opsgenieClient.DefaultRequestHeaders.Add("Authorization", "GenieKey " + opsgenieApiKey);

            //Match priority
            string priorityId;
            switch ((opsgenieData.alert.priority).ToString())
            {
                case "P1": priorityId = "2"; break;
                case "P2": priorityId = "1"; break;
                case "P3": priorityId = "0"; break;
                case "P4": priorityId = "-1"; break;
                case "P5": priorityId = "-1"; break;
                default: priorityId = "0"; break;
            }
            Console.WriteLine(priorityId);

            if (opsgenieData.action == "Create")
            {
                try
                {
                    //Get CategoryId
                    var jitbitCatResponse = await jitbitClient.GetAsync(jitbitBaseUrl + "/categories");
                var jitbitCatContent = await jitbitCatResponse.Content.ReadAsStringAsync();
                var jitbitParsedJsonCat = JArray.Parse(jitbitCatContent);
                var categoryFind = jitbitParsedJsonCat.Children()
                    .FirstOrDefault(x => x["NameWithSection"]?.ToString() == category);
                string catgoryId = new JArray(categoryFind)[0]["CategoryID"].ToString();
                Console.WriteLine("catgoryId: " + catgoryId);

                //Get UserId
                var jitbitUserResponse = await jitbitClient.GetAsync(jitbitBaseUrl + $"/UserByEmail?email={contact}");
                var jitbitUserContent = await jitbitUserResponse.Content.ReadAsStringAsync();
                var jitbitParsedJsonUser = JObject.Parse(jitbitUserContent);
                string userId = jitbitParsedJsonUser["UserID"].ToString();
                Console.WriteLine("userId: " + userId);

                //Create Ticket
                var jitbitTResponse = await jitbitClient.PostAsync(jitbitBaseUrl +
                    $"/ticket?categoryId={catgoryId}&body={description}&subject={message}&priorityId={priorityId}&userId={userId}", null);
                var ticketId = await jitbitTResponse.Content.ReadAsStringAsync();
                Console.WriteLine("ticketId: " + ticketId);

                //Update Ticket
                var jitbitTUResponse = await jitbitClient.PostAsync(jitbitBaseUrl +
                    $"/SetCustomField?ticketId={ticketId}&fieldId=10&value=Event", null);

                //Get alert 
                var opsgenieAlert = await opsgenieClient.GetAsync(opsgenieBaseUrl +
                    $"/alerts/{alertId}");
                var alert = await opsgenieAlert.Content.ReadAsStringAsync();
                var alertParsedJson = JObject.Parse(alert);
                message = alertParsedJson.SelectToken("data.message").ToString();
                Console.WriteLine("alertTile: " + alertParsedJson.SelectToken("data.message"));
                Console.WriteLine("alertSeen: " + alertParsedJson?["data"]?["seen"]);

                //Add TicketId as extra property
                var opsgeniePropJson = new
                {
                    details = new { TicketId = ticketId }
                };
                var opsgeniePropTId = await opsgenieClient.PostAsJsonAsync(opsgenieBaseUrl +
                    $"/alerts/{alertId}/details", opsgeniePropJson);

                HttpStatusCode statusCodeProp = opsgeniePropTId.StatusCode;
                Console.WriteLine("StatusProp: " + statusCodeProp);
                //Add TicketId as message
                var opsgenieMsgJson = new
                {
                    message = $"[{ticketId}]" + message
                };
                var opsgenieMsgTId = await opsgenieClient.PostAsJsonAsync(opsgenieBaseUrl +
                    $"/alerts/{alertId}/message", opsgenieMsgJson);

                HttpStatusCode statusCodeMsg = opsgenieMsgTId.StatusCode;
                Console.WriteLine("StatusMsg: " + statusCodeMsg);

                //Get Ticket Id from message
                var pattern = @"\[([^]]*)\]";
                ticketId = Regex.Match(message, pattern).Groups[1].Value;
                Console.WriteLine("RegEx: " + ticketId);
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogError(ex.Message);
                }
            }
            else
            {
                var pattern = @"\[([^]]*)\]";
                string ticketId = Regex.Match(message, pattern).Groups[1].Value;
                string user = opsgenieData.alert.username;
                string action = (opsgenieData.action).ToString();
                string comment;
                Console.WriteLine(ticketId);

                switch (action)
                {
                    case "UpdatePriority": comment = $"Priority was updated by {user}"; break;
                    case "RemoveTags": comment = $"Tag was removed by {user}"; break;
                    case "Escalate": comment = $"Alert was escalated"; break;
                    case "AddNote": comment = $"Note was added by {user}"; break;
                    case "AddTags": comment = $"Tag was added by {user}"; break;
                    case "AddResponder": comment = $"Responder {user} was added"; break;
                    case "AssignOwnership": comment = $"Alert was assigned to {user}"; break;
                    case "TakeOwnership": comment = $"Ownership was taken by {user}"; break;
                    case "UpdateDescription": comment = $"Alert description was updated by {user}"; break;
                    case "UpdateMessage": comment = $"Alert title was updated by {user}"; break;
                    case "SnoozeEnded": comment = $"Alert snooze ended"; break;
                    case "EscalateToNext": comment = $"Alert was escalated to next"; break;
                    default: comment = $"Alert was {action.ToLower()}d by {user}"; break;
                }

                try
                {
                    var jitbitTUResponse = await jitbitClient.PostAsync(jitbitBaseUrl +
                    $"/comment?id={ticketId}&body={comment}", null);
                    jitbitTUResponse.EnsureSuccessStatusCode();
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogError(ex.Message);
                }

                if (action == "Acknowledge" || action == "UnAcknowledge" || action == "AssignOwnership" || action == "TakeOwnership")
                {
                    string email = action == "AssignOwnership" ? opsgenieData.alert.owner : user;
                    switch (action)
                    {
                        case "AssignOwnership": email = opsgenieData.alert.owner; break;
                        default:
                            email = user;
                            break;
                    }
                    if (action == "Acknowledge" || action == "AssignOwnership" || action == "TakeOwnership")
                    {
                        
                        try
                        {   //Get UserId
                            var jitbitUserResponse = await jitbitClient.GetAsync(jitbitBaseUrl + $"/UserByEmail?email={email}");
                            var jitbitUserContent = await jitbitUserResponse.Content.ReadAsStringAsync();
                            var jitbitParsedJsonUser = JObject.Parse(jitbitUserContent);
                            string userEmail = jitbitParsedJsonUser.ToString();
                            string userId = jitbitParsedJsonUser["UserID"].ToString();
                            Console.WriteLine($"UserId: {userEmail}");

                            //Assign ticket
                            var jitbitCloseResponse = await jitbitClient.PostAsync(jitbitBaseUrl +
                                $"/UpdateTicket?id={ticketId}&assignedUserId={userId}", null);
                            jitbitCloseResponse.EnsureSuccessStatusCode();
                        }
                        catch (HttpRequestException ex)
                        {
                            _logger.LogError(ex.Message);
                        }
                    }
                    //UnAssign ticket
                    if (action == "UnAcknowledge")
                    {
                        try
                        {
                            var jitbitCloseResponse = await jitbitClient.PostAsync(jitbitBaseUrl +
                                $"/UpdateTicket?id={ticketId}&assignedUserId=0", null);
                            jitbitCloseResponse.EnsureSuccessStatusCode();
                        }
                        catch (HttpRequestException ex)
                        {
                            _logger.LogError(ex.Message);
                        }
                    }
                }

                if (action == "Close")
                {   //Close Ticket
                    try
                    {
                        var jitbitCloseResponse = await jitbitClient.PostAsync(jitbitBaseUrl +
                        $"/UpdateTicket?id={ticketId}&statusId=3", null);
                        jitbitCloseResponse.EnsureSuccessStatusCode();
                    }
                    catch (HttpRequestException ex)
                    {
                        _logger.LogError(ex.Message);
                    }
                }
            }
            return new OkObjectResult("Welcome to Azure Functions!");
        }
    }
}
