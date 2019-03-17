using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Configuration;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace Mafren
{
    public static class Mafren
    {
        private static IConfigurationRoot _config;
        private static TraceWriter _log;

        [FunctionName("Mafren")]
        public static async Task Run([TimerTrigger("0 */1 * * * *")]TimerInfo myTimer,
            TraceWriter log,
            ExecutionContext context)
        {
            _log = log;
            _config = new ConfigurationBuilder()
                .SetBasePath(context.FunctionAppDirectory)
                .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            var httpHandler = new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                AllowAutoRedirect = true,
            };

            var httpClient = new HttpClient(httpHandler);
            var data = new FormUrlEncodedContent(new[]{
                new KeyValuePair<string, string>("planning", "7070"),
                new KeyValuePair<string, string>("nextButton", "Etape suivante")
            });

            var result = await httpClient.PostAsync("http://www.hauts-de-seine.gouv.fr/booking/create/4462/1", data);
            _log.Info(result.StatusCode.ToString());
            var htmlPage = await result.Content.ReadAsStringAsync();
            _log.Info(htmlPage);

            if (!htmlPage.Contains("Il n'existe plus de plage horaire libre pour votre demande de rendez-vous."))
            {
                log.Warning("Booking AVAILABLE");
                await SendEmail();
            }
        }

        static async Task SendEmail()
        {
            var client = new SendGridClient(_config["SendGridKey"]);
            var from = new EmailAddress(_config["From"]);
            var subject = "NATIONALITE - Vite - prends rdv maintenant!";
            var plainTextContent = "http://www.hauts-de-seine.gouv.fr/booking/create/4462";
            var htmlContent = "<a href=\"http://www.hauts-de-seine.gouv.fr/booking/create/4462\">Clique ici</a>";

            var toEmails = _config.GetSection("To")
                                .GetChildren()
                                .Select(x => x.Value);

            foreach (var email in toEmails)
            {
                _log.Info(email);
                var to = new EmailAddress(email);
                var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, htmlContent);
                await client.SendEmailAsync(msg);
            }
        }

        public static void AddDefaultHeaders(this HttpClient httpClient)
        {
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
            httpClient.DefaultRequestHeaders.Add("Pragma", "no-cache");
            httpClient.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
            httpClient.DefaultRequestHeaders.Add("Origin", "http://www.hauts-de-seine.gouv.fr");
            httpClient.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
            httpClient.DefaultRequestHeaders.Add("Content-Type", "application/x-www-form-urlencoded");
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/72.0.3626.121 Safari/537.36");
            httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8");
            httpClient.DefaultRequestHeaders.Add("Referer", "http://www.hauts-de-seine.gouv.fr/booking/create/4462/1"); ;
            httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate");
            httpClient.DefaultRequestHeaders.Add("Accept-Language", "fr");
        }
    }
}
