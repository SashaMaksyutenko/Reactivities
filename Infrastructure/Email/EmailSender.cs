using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;
using MimeKit.Text;
namespace Infrastructure.Email
{
    public class EmailSender
    {
        private readonly IConfiguration _config;

        public EmailSender(IConfiguration config)
        {
            _config = config;
        }
        public async Task<bool> SendEmailAsync(string userEmail, string emailSubject, string msg){
            var result = "";
            try
            {
                var email = new MimeMessage();
                email.From.Add(MailboxAddress.Parse(_config["Ethereal:EMail"]));
                email.To.Add(MailboxAddress.Parse(userEmail));
                email.Subject = emailSubject;
                email.Body = new TextPart(TextFormat.Html) { Text = msg };
                using var smtp = new SmtpClient();
                smtp.CheckCertificateRevocation = false;
                //may need to use StartTls ?
                smtp.Connect("smtp.ethereal.email", 587, SecureSocketOptions.Auto);
                smtp.Authenticate(_config["Ethereal:EMail"], _config["Ethereal:Password"]);
                result =  await smtp.SendAsync(email);
                smtp.Dispose();
                return true;
            }
            catch (Exception ex) 
            {
                Console.WriteLine(result);
                Console.WriteLine(ex);
                return false;
            }
        }
    }
}