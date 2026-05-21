using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

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
            if (string.IsNullOrWhiteSpace(toEmail))
                return;

            var message = new MimeMessage();

            message.From.Add(
                new MailboxAddress(_s.SenderName, _s.SenderEmail));

            message.To.Add(
                MailboxAddress.Parse(toEmail));

            message.Subject = subject;

            message.Body = new TextPart("html")
            {
                Text = body
            };

            using var client = new SmtpClient();

            try
            {
                var secureOption = _s.EnableSSL
                    ? SecureSocketOptions.StartTls
                    : SecureSocketOptions.Auto;

                await client.ConnectAsync(
                    _s.SmtpServer,
                    _s.SmtpPort,
                    secureOption);

                await client.AuthenticateAsync(
                    _s.Username,
                    _s.Password);

                await client.SendAsync(message);

                await client.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[EMAIL_SEND_ERROR]");
                Console.WriteLine(ex.Message);
                throw;
            }
        }
    }
}