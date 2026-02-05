using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using TASA.Extensions;
using TASA.Models;
using TASA.Program;

namespace TASA.Services.AuthModule
{
    public class PasswordServices(TASAContext db, ServiceWrapper service) : IService
    {
        public void ToHash()
        {
            var user = db.AuthUser.Where(x => string.IsNullOrEmpty(x.PasswordHash)).ToList();
            foreach (var item in user)
            {
                var hashPassword = HashString.Hash(item.Password);
                item.PasswordHash = hashPassword.Hash;
                item.PasswordSalt = hashPassword.Salt;
            }
            db.SaveChanges();
        }

        public record ForgetMailVM
        {
            [Required(ErrorMessage = "帳號是必要項")]
            public string Account { get; set; } = string.Empty;
        }
        public void ForgetMail(ForgetMailVM vm)
        {
            var user = db.AuthUser
                .AsNoTracking()
                .WhereNotDeleted()
                .FirstOrDefault(x => x.Account == vm.Account);
            if (user == null)
            {
                throw new HttpException("此信箱尚未註冊");
            }
            service.LoginServices.IsEnabled(user);
            if (user != null)
            {
                var newAuthForget = new AuthForget()
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    ExpiresAt = DateTime.Now.AddMinutes(15),
                    IsUsed = false
                };
                db.AuthForget.Add(newAuthForget);
                db.SaveChanges();
                _ = _ = service.LogServices.LogAsync("忘記密碼", $"帳號 {user.Account}");
                service.PasswordMail.Forget(newAuthForget.Id, newAuthForget.ExpiresAt, user.Email);
            }
        }

        public record ForgetVM
        {
            public Guid Id { get; set; }
            [Required(ErrorMessage = "密碼是必要項")]
            public string Password { get; set; } = string.Empty;
        }
        public void Forget(ForgetVM vm)
        {
            var forget = db.AuthForget
                .WhereNotDeleted()
                .FirstOrDefault(x => x.Id == vm.Id);
            if (forget != null)
            {
                if (forget.IsUsed)
                {
                    throw new HttpException("此連結已使用過，請重新申請");
                }
                if (forget.ExpiresAt < DateTime.Now)
                {
                    throw new HttpException("此連結已過期，請重新申請");
                }

                var user = db.AuthUser
                    .WhereNotDeleted()
                    .FirstOrDefault(x => x.Id == forget.UserId);
                if (user != null)
                {
                    forget.IsUsed = true;
                    var hashPassword = HashString.Hash(vm.Password);
                    user.PasswordHash = hashPassword.Hash;
                    user.PasswordSalt = hashPassword.Salt;
                    db.SaveChanges();
                    _ = _ = service.LogServices.LogAsync("忘記密碼", $"帳號 {user.Account} 變更密碼");
                    service.PasswordMail.PasswordChange(user.Email);
                }
            }
        }

        public record ChangePWVM
        {
            [Required(ErrorMessage = "密碼是必要項")]
            public string Password { get; set; } = string.Empty;
        }
        public void ChangePW(ChangePWVM vm)
        {
            var userid = service.UserClaimsService.Me()?.Id;
            var user = db.AuthUser
                .WhereNotDeleted()
                .FirstOrDefault(x => x.Id == userid);
            if (user != null)
            {
                var hashPassword = HashString.Hash(vm.Password);
                user.PasswordHash = hashPassword.Hash;
                user.PasswordSalt = hashPassword.Salt;
                db.SaveChanges();
                _ = _ = service.LogServices.LogAsync("變更密碼", $"帳號 {user.Account} 變更密碼");
                service.PasswordMail.PasswordChange(user.Email);
            }
        }
    }
}
