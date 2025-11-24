using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using TASA.Extensions;
using TASA.Models;
using TASA.Program;

namespace TASA.Services.AuthModule
{
    public class LoginServices(TASAContext db, ServiceWrapper service) : IService
    {
        public AuthUser? IsValidUser(string account, string password)
        {
            var user = db.AuthUser
                .Include(x => x.AuthRole)
                .Include(x => x.Department)
                .AsNoTracking()
                .WhereNotDeleted()
                .FirstOrDefault(x => x.Account == account);
            if (user == null || !HashString.Verify(password, user.PasswordHash, user.PasswordSalt))
            {
                return null;
            }
            IsEnabled(user);
            return user;
        }

        public void IsEnabled(AuthUser? user)
        {
            if (user?.IsEnabled == false)
            {
                _ = _ = service.LogServices.LogAsync("登入失敗", $"帳號 {user.Account} 已停用");
                throw new HttpException("帳號已停用")
                {
                    StatusCode = System.Net.HttpStatusCode.Unauthorized
                };
            }
        }

        public record LoginVM
        {
            [Required(ErrorMessage = "帳號是必要項")]
            public string Account { get; set; } = string.Empty;
            [Required(ErrorMessage = "密碼是必要項")]
            public string Password { get; set; } = string.Empty;
        }
        public AuthUser Login(LoginVM vm)
        {
            var user = IsValidUser(vm.Account, vm.Password);
            if (user == null)
            {
                _ = _ = service.LogServices.LogAsync("登入失敗", $"帳號 {vm.Account}");
                throw new HttpException("登入失敗")
                {
                    StatusCode = System.Net.HttpStatusCode.Unauthorized
                };
            }
            else
            {
                _ = _ = service.LogServices.LogAsync("登入成功", $"帳號 {user.Account}");
                return user;
            }
        }

        /// <summary>
        /// 設定 JWT Cookie
        /// </summary>
        public void GenerateCookie(IResponseCookies cookies, AuthUser user)
        {
            Jwt.GenerateCookie(cookies, UserClaimsService.ToClaims(user));
        }

        /// <summary>
        /// 刪除 JWT Cookie
        /// </summary>
        public void DeleteCookie(IResponseCookies cookies)
        {
            Jwt.DeleteCookie(cookies);
        }

        public string RedirectUrl(ClaimsPrincipal? user)
        {
            var authUser = UserClaimsService.ToAuthUser(user?.Claims);
            if (authUser != null)
            {
                return "/calendar";
            }
            else
            {
                return "/";
            }
        }
    }
}
