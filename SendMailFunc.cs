using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Azure.Communication.Email;
using Azure.Communication.Email.Models;
using System.Collections.Generic;
using System.Linq;
using System;

namespace ACSSendMail202206
{
    public static class SendMailFunc
    {
        static EmailClient client = new EmailClient("YOUR_ACS_CONNECTION_STRING");
        static string sender = "ACSMailSender@YOUR_ACS_EMAIL_DOMAIN.azurecomm.net";

        [FunctionName("SendMail")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            SendMailProperties properties = await JsonSerializer.DeserializeAsync<SendMailProperties>(req.Body);

            string responseMessage = "";

            try
            {
                EmailMessage message = new EmailMessage(
                    sender,
                    new EmailContent(properties.Subject) { PlainText = properties.PlainContent, Html = properties.HtmlContent },
                    new EmailRecipients(properties.Recipients.Select(x => new EmailAddress(x.Email, x.Name)).ToList())
                    );

                SendEmailResult result = await client.SendAsync(message);

                string messageId = result.MessageId;
                if (!string.IsNullOrEmpty(messageId))
                {
                    SendStatusResult status = await client.GetSendStatusAsync(messageId);

                    while (status.Status == SendStatus.Queued)
                    {
                        await Task.Delay(5000);
                        status = await client.GetSendStatusAsync(messageId);
                    }

                    if (status.Status == SendStatus.OutForDelivery)
                    {
                        responseMessage = $"Email send task completed (out for delivery). MessageId : <{messageId}>";
                    }
                    else
                    {
                        responseMessage = $"Email seems to be dropped. Please try later again. MessageId : <{messageId}>";
                    }
                }
                else
                {
                    responseMessage = $"Failed to send email.";
                }

            }
            catch (Exception e)
            {
                responseMessage = $"Please make sure send POST request with info such as: "
                                    + "Recipients(Name,Email), Subject and PlainContent and/or HtmlContent.\n"
                                    + "exception source: " + e.Source;
            }

            return new OkObjectResult(responseMessage);
        }
    }

    public class SendMailProperties
    {
        public List<Recipient> Recipients { get; set; }
        public string Subject { get; set; }
        public string PlainContent { get; set; }
        public string HtmlContent { get; set; }
    }
    public class Recipient
    {
        public string Email { get; set; }
        public string Name { get; set; }
    }

}
