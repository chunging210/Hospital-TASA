using Microsoft.EntityFrameworkCore;
using System.Net.Mail;
using TASA.Library.Core;
using TASA.Models;
using TASA.Program;

namespace TASA.Services.MailModule
{
    public class NUUMail
    {
        private readonly MailService _mailservice;
        private readonly IDbContextFactory<TASAContext> _dbContextFactory;
        private readonly IHttpContextAccessor _http;

        protected bool Enable
        {
            get
            {
                return _mailservice.Enable;
            }
        }

        protected string? BaseUrl
        {
            get
            {
                var request = _http.HttpContext?.Request;
                if (request == null) return null;
                return $"{request.Scheme}://{request.Host}/";
            }
        }

        public NUUMail(MailService mailservice, IDbContextFactory<TASAContext> dbContextFactory, IHttpContextAccessor httpContextAccessor)
        {
            _mailservice = mailservice;
            mailservice.MailSent += Mailservice_MailSent;
            _dbContextFactory = dbContextFactory;
            _http = httpContextAccessor;
        }

        protected MailMessage NewMailMessage()
        {
            return _mailservice.NewMailMessage();
        }

        /// TODO 需要改寫:定義背景工作服務
        protected async Task SendAsync(MailMessage mail, string className, string functionName)
        {
            var filename = Path.GetFileNameWithoutExtension(className);
            mail.Body = $@"
{mail.Body}
<p></p>
<p>此為自動發信，請勿回信！</p>";
            await _mailservice.Send(mail, filename, functionName);
        }

        private void Mailservice_MailSent(object? sender, MailServiceEvent e)
        {
            using var db = _dbContextFactory.CreateDbContext();
            db.LogMail.Add(new LogMail()
            {
                Address = e.Addresses,
                ClassName = e.ClassName,
                FunctionName = e.FunctionName,
                Exception = e.ExceptionMessage,
                SendTime = DateTime.Now
            });
            db.SaveChangesAsync().GetAwaiter().GetResult();
        }
    }
}
