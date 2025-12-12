using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

namespace MOAClover.Services
{
    public class EmailSettings
    {
        public string SmtpServer { get; set; }
        public int Port { get; set; }
        public string SenderEmail { get; set; }
        public string SenderName { get; set; }
        public string Password { get; set; }
    }

    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string body);
    }

    public class EmailService : IEmailService
    {
        private readonly EmailSettings _config;

        public EmailService(IOptions<EmailSettings> config)
        {
            _config = config.Value;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            using var client = new SmtpClient(_config.SmtpServer, _config.Port)
            {
                Credentials = new NetworkCredential(_config.SenderEmail, _config.Password),
                EnableSsl = true
            };

            var mail = new MailMessage()
            {
                From = new MailAddress(_config.SenderEmail, _config.SenderName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            mail.To.Add(toEmail);

            await client.SendMailAsync(mail);
        }
    }
}
