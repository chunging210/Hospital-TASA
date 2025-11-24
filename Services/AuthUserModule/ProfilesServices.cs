using Microsoft.EntityFrameworkCore;
using TASA.Extensions;
using TASA.Models;
using TASA.Program;
using System.Text.RegularExpressions;

namespace TASA.Services.AuthUserModule
{
    public class ProfilesServices(TASAContext db, ServiceWrapper service) : IService
    {
        public record DetailVM
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string? Password { get; set; }
        }
        public DetailVM? Detail()
        {
            var userid = service.UserClaimsService.Me()?.Id;
            return db.AuthUser
                .AsNoTracking()
                .WhereNotDeleted()
                .Mapping(x => new DetailVM()
                {
                    Password = ""
                })
                .FirstOrDefault(x => x.Id == userid);
        }

        public bool IsValidPassword(string password)
        {
            string regexPattern = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{10,}$";
            return Regex.IsMatch(password, regexPattern);
        }

        public void Update(DetailVM vm)
        {
            var user = db.AuthUser
                .WhereNotDeleted()
                .FirstOrDefault(x => x.Id == vm.Id);
            if (user != null)
            {
                user.Name = vm.Name;
                user.Email = vm.Email;
                if (!string.IsNullOrEmpty(vm.Password))
                {
                    if (!IsValidPassword(vm.Password))
                    {
                        throw new HttpException("密碼不符合規則");
                    }
                    _ = _ = service.LogServices.LogAsync("變更密碼", $"帳號 {user.Account} 變更密碼");
                    user.Password = vm.Password;
                }
                db.SaveChanges();
            }
        }
    }
}
