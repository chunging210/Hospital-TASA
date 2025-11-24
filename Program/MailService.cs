using System.Net.Mail;
using System.Text;
using TASA.Library.Core;

namespace TASA.Program
{
    public class MailService : IService
    {
        public readonly bool Enable;
        private readonly string? FromMail;
        private readonly string? FromName;
        private readonly SmtpClient Smtp;

        public MailService(IConfiguration config)
        {
            var MailConfig = config.GetSection("Mail");
            Enable = MailConfig.GetValue<bool>("Enable");
            FromMail = MailConfig["FromMail"];
            FromName = MailConfig["FromName"];
            var Host = MailConfig["Host"];
            var Port = MailConfig.GetValue<int>("Port");
            var UserName = MailConfig["UserName"];
            var Password = MailConfig["Password"];
            var EnableSsl = MailConfig.GetValue<bool>("EnableSsl");
            Smtp = new SmtpClient(Host, Port)
            {
                Credentials = new System.Net.NetworkCredential(UserName, Password),
                EnableSsl = EnableSsl
            };
        }

        public event EventHandler<MailServiceEvent>? MailSent;

        public MailMessage NewMailMessage()
        {
            return new MailMessage()
            {
                From = new MailAddress(FromMail ?? "", FromName, Encoding.UTF8),
                SubjectEncoding = Encoding.UTF8,
                IsBodyHtml = true,
                BodyEncoding = Encoding.UTF8,
                Priority = MailPriority.Normal
            };
        }

        public async Task Send(MailMessage msg, string className, string functionName)
        {
            if (!Enable)
            {
                return;
            }

            Exception? exception = null;
            try
            {
                await Smtp.SendMailAsync(msg);
            }
            catch (Exception e)
            {
                exception = e;
            }

            var eventArgs = new MailServiceEvent
            {
                Addresses = string.Join(";", msg.To.Select(x => x.Address)),
                ClassName = className,
                FunctionName = functionName,
                ExceptionMessage = exception?.GetBaseException().Message
            };
            MailSent?.Invoke(this, eventArgs);
        }
    }
}
