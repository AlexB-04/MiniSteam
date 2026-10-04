using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace MiniSteam.Helpers
{
    public class MailHelper : IMailHelper
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<MailHelper> _logger;

        public MailHelper(
            IConfiguration configuration,
            ILogger<MailHelper> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public Response SendEmail(string to, string subject, string body)
        {
            var nameFrom = _configuration["Mail:NameFrom"];
            var from = _configuration["Mail:From"];
            var smtp = _configuration["Mail:Smtp"];
            var portText = _configuration["Mail:Port"];
            var password = _configuration["Mail:Password"];

            if (string.IsNullOrWhiteSpace(nameFrom) ||
                string.IsNullOrWhiteSpace(from) ||
                string.IsNullOrWhiteSpace(smtp) ||
                string.IsNullOrWhiteSpace(portText) ||
                string.IsNullOrWhiteSpace(password) ||
                !int.TryParse(portText, out var port))
            {
                _logger.LogWarning("Mail configuration is incomplete or invalid.");

                return new Response
                {
                    IsSuccess = false,
                    Message = "Unable to send email."
                };
            }

            var message = new MimeMessage();

            message.From.Add(new MailboxAddress(nameFrom, from));
            message.To.Add(new MailboxAddress(to, to));
            message.Subject = subject;

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = body
            };

            message.Body = bodyBuilder.ToMessageBody();

            try
            {
                using var client = new SmtpClient();

                // Port 587 is intended for explicit STARTTLS. Using StartTls here
                // prevents silently sending credentials over an unencrypted SMTP session.
                client.Connect(smtp, port, SecureSocketOptions.StartTls);
                client.Authenticate(from, password);

                client.Send(message);
                client.Disconnect(true);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Unable to send MiniSteam email.");

                return new Response
                {
                    IsSuccess = false,
                    Message = "Unable to send email."
                };
            }

            return new Response
            {
                IsSuccess = true
            };
        }
    }
}
