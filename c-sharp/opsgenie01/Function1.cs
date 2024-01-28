using System.Net;
using System.Text;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace func_opsgenie_01
{
    public class Function1
    {
        private readonly ILogger _logger;

        public Function1(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<Function1>();
        }

        [Function("trigger-http-opsgenie")]
        public async Task<HttpResponseData> RunAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            response.WriteString("Welcome to Azure Functions!");
            //Parse JSON
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic opsgenieData = JObject.Parse(requestBody);

            _logger.LogWarning($"action: {opsgenieData.action}");
            _logger.LogWarning($"alertId: {opsgenieData.alert.alertId}");
            _logger.LogWarning($"title: {opsgenieData.alert.message}");

            string alertId = opsgenieData.alert.alertId;
            string category = opsgenieData.alert.details.Category;
            string contact = string.IsNullOrEmpty((opsgenieData.alert.details.Contact).ToString()) ? "opsgenie@b3.se" : (opsgenieData.alert.details.Contact).ToString();
            string description = $"https://app.opsgenie.com/alert/detail/{opsgenieData.alert.alertId}";   
            string message = opsgenieData.alert.message;   

            //jitbit info
            string jitbitToken = "X";
            string jitbitBaseUrl = $"https://x.azurewebsites.net/api";

            //GET Opsgenie info
            string opsgenieUrl = "https://api.opsgenie.com/v2";
            string opsgenieApiKey = "X";

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

            if (opsgenieData.action == "Create") {

                //Get CategoryId
                var jitbitPostCat = (HttpWebRequest)WebRequest.Create(jitbitBaseUrl + "/categories");
                jitbitPostCat.Headers["Authorization"] = "Bearer " + jitbitToken;
                jitbitPostCat.ContentType = "application/json";
                var responseJitbitCat = (HttpWebResponse)jitbitPostCat.GetResponse();
                Console.WriteLine(responseJitbitCat.ResponseUri);
                var streamJitbitCat = await new StreamReader(responseJitbitCat.GetResponseStream()).ReadToEndAsync();
                var jitbitParsedJsonCat = JArray.Parse(streamJitbitCat);
                var catgoryFind = jitbitParsedJsonCat.Children()
                    .Where(x => x["NameWithSection"]?.ToString() == category);
                string catgoryId = new JArray(catgoryFind)[0]["CategoryID"].ToString();

                //Get UserId
                var jitbitPostUser = (HttpWebRequest)WebRequest.Create(jitbitBaseUrl + $"/UserByEmail?email={contact}");
                jitbitPostUser.Headers["Authorization"] = "Bearer " + jitbitToken;
                jitbitPostUser.ContentType = "application/json";
                var responseJitbitUser = (HttpWebResponse)jitbitPostUser.GetResponse();
                Console.WriteLine(responseJitbitUser.ResponseUri);
                var streamJitbitUser = await new StreamReader(responseJitbitUser.GetResponseStream()).ReadToEndAsync();
                var jitbitParsedJsonUser = JObject.Parse(streamJitbitUser);
                string userId = jitbitParsedJsonUser["UserID"].ToString();

                //Create Ticket
                var jitbitPostT = (HttpWebRequest)WebRequest.Create(jitbitBaseUrl +
                    $"/ticket?categoryId={catgoryId}&body={description}&subject={message}&priorityId={priorityId}&userId={userId}");
                jitbitPostT.Method = "POST";
                jitbitPostT.Headers["Authorization"] = "Bearer " + jitbitToken;
                jitbitPostT.ContentType = "application/json";
                var responseJitbitT = (HttpWebResponse)jitbitPostT.GetResponse();
                //Get TicketId
                var encoding = ASCIIEncoding.ASCII;
                var readerTId = new StreamReader(responseJitbitT.GetResponseStream(), encoding);
                string ticketId = readerTId.ReadToEnd();

                //Update Ticket
                var jitbitPostTU = (HttpWebRequest)WebRequest.Create(jitbitBaseUrl +
                    $"/SetCustomField?ticketId={ticketId}&fieldId=10&value=Event");
                jitbitPostTU.Method = "POST";
                jitbitPostTU.Headers["Authorization"] = "Bearer " + jitbitToken;
                jitbitPostTU.ContentType = "application/json";
                try
                {
                    var responseJitbitTU = (HttpWebResponse)jitbitPostTU.GetResponse();
                }
                catch (WebException ex)
                {
                    HttpWebResponse errorResponseAzure = (HttpWebResponse)ex.Response;
                }

                //Add TicketId as extra property
                Console.WriteLine(alertId);
                var opsgenieTId = (HttpWebRequest)WebRequest.CreateHttp(opsgenieUrl + $"/alerts/{alertId}/details");
                Console.WriteLine(opsgenieTId.RequestUri);
                opsgenieTId.Method = "POST";
                opsgenieTId.Headers["Authorization"] = "GenieKey " + opsgenieApiKey;
                opsgenieTId.ContentType = "application/json";
                var streamWriter = new StreamWriter(opsgenieTId.GetRequestStream());
                var opsgenieJson = JObject.FromObject(new
                {
                    details = new { TicketId = ticketId }
                });
                Console.WriteLine(opsgenieJson);
                streamWriter.Write(opsgenieJson);
                streamWriter.Flush();
                try
                {
                    var responseOpsgenie = (HttpWebResponse)opsgenieTId.GetResponse();
                }
                catch (WebException ex)
                {
                    HttpWebResponse errorResponseAzure = (HttpWebResponse)ex.Response;
                }
            }
            else
            {
                string ticketId = opsgenieData.alert.details.TicketId;
                string user = opsgenieData.alert.username;
                string action = (opsgenieData.action).ToString();
                string comment;


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

                var jitbitPostC = (HttpWebRequest)WebRequest.Create(jitbitBaseUrl +
                    $"/comment?id={ticketId}&body={comment}");
                jitbitPostC.Method = "POST";
                jitbitPostC.Headers["Authorization"] = "Bearer " + jitbitToken;
                jitbitPostC.ContentType = "application/json";
                try
                {
                    var responseJitbitC = (HttpWebResponse)jitbitPostC.GetResponse();
                }
                catch (WebException ex)
                {
                    HttpWebResponse errorResponseAzure = (HttpWebResponse)ex.Response;
                }

                if (action == "Acknowledge" || action == "UnAcknowledge" || action == "AssignOwnership" || action == "TakeOwnership") 
                {
                    string email = action == "AssignOwnership" ? opsgenieData.alert.owner : user;
                    switch (action)
                    {
                        //case "UnAcknowledge": email = null; break;
                        case "AssignOwnership": email = opsgenieData.alert.owner; break;
                        default: email = user;
                            break;
                    }
                    if (action == "Acknowledge" || action == "AssignOwnership" || action == "TakeOwnership")
                    {
                        //Get UserId
                        var jitbitPostUser = (HttpWebRequest)WebRequest.Create(jitbitBaseUrl + $"/UserByEmail?email={email}");
                        jitbitPostUser.Headers["Authorization"] = "Bearer " + jitbitToken;
                        jitbitPostUser.ContentType = "application/json";
                        var responseJitbitUser = (HttpWebResponse)jitbitPostUser.GetResponse();
                        Console.WriteLine(responseJitbitUser.ResponseUri);
                        Console.WriteLine($"UserId: {email}");
                        var streamJitbitUser = await new StreamReader(responseJitbitUser.GetResponseStream()).ReadToEndAsync();
                        var jitbitParsedJsonUser = JObject.Parse(streamJitbitUser);
                        string userId = jitbitParsedJsonUser["UserID"].ToString();
                        Console.WriteLine($"UserId: {userId}");
                        //Assign ticket
                        var jitbitPostAs = (HttpWebRequest)WebRequest.Create(jitbitBaseUrl +
                            $"/UpdateTicket?id={ticketId}&assignedUserId={userId}");
                        jitbitPostAs.Method = "POST";
                        jitbitPostAs.Headers["Authorization"] = "Bearer " + jitbitToken;
                        jitbitPostAs.ContentType = "application/json";
                        try
                        {
                            var responseJitbitAs = (HttpWebResponse)jitbitPostAs.GetResponse();
                        }
                        catch (WebException ex)
                        {
                            HttpWebResponse errorResponseAzure = (HttpWebResponse)ex.Response;
                        }

                    }
                    //UnAssign ticket
                    if (action == "UnAcknowledge")
                    {
                        var jitbitPostUnAs = (HttpWebRequest)WebRequest.Create(jitbitBaseUrl +
                        $"/UpdateTicket?id={ticketId}&assignedUserId=0");
                        jitbitPostUnAs.Method = "POST";
                        jitbitPostUnAs.Headers["Authorization"] = "Bearer " + jitbitToken;
                        jitbitPostUnAs.ContentType = "application/json";
                        try
                        {
                            var responseJitbitUnAs = (HttpWebResponse)jitbitPostUnAs.GetResponse();
                        }
                        catch (WebException ex)
                        {
                            HttpWebResponse errorResponseAzure = (HttpWebResponse)ex.Response;
                        }
                    }
                }

                if (action == "Close")
                {
                    var jitbitPostClose = (HttpWebRequest)WebRequest.Create(jitbitBaseUrl +
                        $"/UpdateTicket?id={ticketId}&statusId=3");
                    jitbitPostClose.Method = "POST";
                    jitbitPostClose.Headers["Authorization"] = "Bearer " + jitbitToken;
                    jitbitPostClose.ContentType = "application/json";
                    try
                    {
                        var responseJitbitClose = (HttpWebResponse)jitbitPostClose.GetResponse();
                    }
                    catch (WebException ex)
                    {
                        HttpWebResponse errorResponseAzure = (HttpWebResponse)ex.Response;
                    }
                }
            }
            return response;
        }
    }
}
