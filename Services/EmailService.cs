// Services/EmailService.cs
using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace MOAClover.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string body);
    }

    public class EmailService : IEmailService
    {
        private readonly EmailSettings _s;

        public EmailService(IOptions<EmailSettings> options)
        {
            _s = options.Value;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            using var mail = new MailMessage();
            mail.From = new MailAddress(_s.SenderEmail, _s.SenderName);
            mail.To.Add(toEmail);
            mail.Subject = subject;
            mail.Body = body;
            mail.IsBodyHtml = true;

            using var client = new SmtpClient(_s.SmtpServer, _s.SmtpPort)
            {
                EnableSsl = _s.EnableSSL,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(_s.Username, _s.Password),
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            await client.SendMailAsync(mail);
        }
    }
}
