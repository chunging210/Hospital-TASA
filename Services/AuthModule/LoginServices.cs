using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using TASA.Extensions;
using TASA.Models;
using TASA.Program;
using Newtonsoft.Json;

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
                var failureInfo = new { UserName = user.Account, LoginMethod = "本地", IsSuccess = false, FailureReason = "帳號已停用" };
                _ = service.LogServices.LogAsync("login_failed", JsonConvert.SerializeObject(failureInfo));
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
                // ✅ 改這裡：info 改成 JSON 字串
                var failureInfo = new { UserName = vm.Account, LoginMethod = "本地", IsSuccess = false, FailureReason = "密碼錯誤" };
                _ = service.LogServices.LogAsync("login_failed", Newtonsoft.Json.JsonConvert.SerializeObject(failureInfo));
                throw new HttpException("登入失敗")
                {
                    StatusCode = System.Net.HttpStatusCode.Unauthorized
                };
            }
            else
            {
                // ✅ 改這裡：info 改成 JSON 字串
                var successInfo = new { UserName = user.Account, LoginMethod = "本地", IsSuccess = true, FailureReason = (string)null };
                _ = service.LogServices.LogAsync("login_success", Newtonsoft.Json.JsonConvert.SerializeObject(successInfo));
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
                return "/Auth/Profiles";
            }
            else
            {
                return "/";
            }
        }
    }
}
