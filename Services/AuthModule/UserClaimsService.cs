using System.Security.Claims;
using System.Text.Json;
using TASA.Models;
using TASA.Program;
using TASA.Services.AuthUserModule;

namespace TASA.Services.AuthModule
{
    public class UserClaimsService : IService
    {
        private readonly Lazy<ClaimsPrincipal?> HttpContextUser;

        private readonly Lazy<MeVM?> LazyMe;

        public UserClaimsService(IHttpContextAccessor httpContextAccessor)
        {
            HttpContextUser = new Lazy<ClaimsPrincipal?>(() =>
            {
                return httpContextAccessor.HttpContext?.User;
            });
            LazyMe = new Lazy<MeVM?>(() =>
            {
                return ToAuthUser(HttpContextUser.Value?.Claims);
            });
        }

        public static IEnumerable<Claim> ToClaims(AuthUser user)
        {
            return [
                new Claim("id", user.Id.ToString()),
                new Claim("name", user.Name),
                new Claim("authrole", JsonSerializer.Serialize(user.AuthRole.Where(y => y.IsEnabled && y.DeleteAt == null).Select(x=>x.Code))),
                new Claim("departmentid", user.DepartmentId?.ToString()??""),
                new Claim("departmentname", user.Department?.Name??""),
            ];
        }

        public record MeVM
        {
            public Guid? Id { get; set; }
            public string? Name { get; set; }
            public List<string> Role { get; set; } = [];
            public Guid? DepartmentId { get; set; }
            public string? DepartmentName { get; set; }
            public bool IsAdmin { get; set; }
            public bool IsDirector { get; set; }
            public bool IsAccountant { get; set; }
        };

        public static MeVM? ToAuthUser(IEnumerable<Claim>? claims)
        {
            if (claims == null || !claims.Any())
            {
                return null;
            }
            _ = Guid.TryParse(claims.FirstOrDefault(x => x.Type == "id")?.Value, out var userid);
            var roleclaims = claims.FirstOrDefault(x => x.Type == "authrole")?.Value;
            var role = new List<string>();
            if (!string.IsNullOrEmpty(roleclaims))
            {
                role = JsonSerializer.Deserialize<List<string>>(roleclaims) ?? [];
            }
            _ = Guid.TryParse(claims.FirstOrDefault(x => x.Type == "departmentid")?.Value, out var departmentId);
            var departmentName = claims.FirstOrDefault(x => x.Type == "departmentname")?.Value;
            return new MeVM()
            {
                Id = userid,
                Name = claims.FirstOrDefault(x => x.Type == "name")?.Value,
                Role = role,
                DepartmentId = departmentId,
                DepartmentName = departmentName,
                IsAdmin = role.Any(x => x == AuthRoleServices.Admin),
                IsDirector = role.Any(x => x == AuthRoleServices.Director),      // ← 新增
                IsAccountant = role.Any(x => x == AuthRoleServices.Accountant)   // ← 新增
            };
        }

        public MeVM? Me()
        {
            return LazyMe.Value;
        }
    }
}
