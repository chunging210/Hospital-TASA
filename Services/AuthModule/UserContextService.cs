using TASA.Models.Auth;
using Microsoft.EntityFrameworkCore;
using TASA.Extensions;
using TASA.Models;
using TASA.Models.Enums;
using TASA.Program;
using TASA.Services.AuthModule;

namespace TASA.Services.AuthModule
{
    /// <summary>
    /// 系統級「使用者上下文」服務
    /// - 每個 Request 只產生一次
    /// - 整個系統唯一可信的使用者身分來源
    /// </summary>
    public class UserContextService : IService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly Lazy<UserContext?> _current;

        public UserContextService(
            IHttpContextAccessor httpContextAccessor,
            UserClaimsService userClaimsService)
        {
            _httpContextAccessor = httpContextAccessor;
            
            _current = new Lazy<UserContext?>(() =>
            {
                Console.WriteLine("========== UserContextService.Current 被呼叫 ==========");
                Console.WriteLine($"_httpContextAccessor: {(_httpContextAccessor != null ? "✅" : "❌")}");
                Console.WriteLine($"HttpContext: {(_httpContextAccessor?.HttpContext != null ? "✅" : "❌")}");
                
                // ✅ 檢查是否在 HTTP Request 中
                if (_httpContextAccessor.HttpContext == null)
                {
                    Console.WriteLine("❌ HttpContext is NULL");
                    Console.WriteLine("====================================================");
                    return null;
                }

                Console.WriteLine($"User: {(_httpContextAccessor.HttpContext.User != null ? "✅" : "❌")}");
                Console.WriteLine($"User.Identity.IsAuthenticated: {_httpContextAccessor.HttpContext.User?.Identity?.IsAuthenticated}");
                Console.WriteLine($"Claims Count: {_httpContextAccessor.HttpContext.User?.Claims?.Count() ?? 0}");
                
                if (_httpContextAccessor.HttpContext.User?.Claims != null)
                {
                    foreach (var claim in _httpContextAccessor.HttpContext.User.Claims)
                    {
                        Console.WriteLine($"  Claim: {claim.Type} = {claim.Value}");
                    }
                }

                var me = userClaimsService.Me();
                
                Console.WriteLine($"userClaimsService.Me(): {(me != null ? "✅" : "❌")}");
                
                if (me == null || me.Id == null)
                {
                    Console.WriteLine("❌ Me() 回傳 null 或 Id 是 null");
                    Console.WriteLine("====================================================");
                    return null;
                }

                Console.WriteLine($"✅ Me().Id: {me.Id}");
                Console.WriteLine($"✅ Me().Name: {me.Name}");
                Console.WriteLine($"✅ Me().IsAdmin: {me.IsAdmin}");
                Console.WriteLine($"✅ Me().DepartmentId: {me.DepartmentId}");
                Console.WriteLine("====================================================");

                return new UserContext
                {
                    UserId = me.Id.Value,
                    DepartmentId = me.DepartmentId,
                    DepartmentName = me.DepartmentName,

                    IsAdmin = me.IsAdmin,
                    IsDirector = me.IsDirector,
                    IsAccountant = me.IsAccountant
                };
            });
        }

        /// <summary>
        /// 目前 Request 的使用者上下文
        /// </summary>
        public UserContext? Current => _current.Value;
    }
}