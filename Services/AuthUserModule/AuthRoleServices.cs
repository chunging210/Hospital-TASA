using Microsoft.EntityFrameworkCore;
using TASA.Extensions;
using TASA.Models;
using TASA.Program;

namespace TASA.Services.AuthUserModule
{
    public class AuthRoleServices(TASAContext db) : IService
    {
        public const string Normal = "NORMAL";
        public const string Admin = "ADMINN";
        public const string Staff = "STAFF";

        public record ListVM
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }
        public IEnumerable<ListVM> List()
        {
            return db.AuthRole
                .AsNoTracking()
                .WhereNotDeleted()
                .WhereEnabled()
                .Mapping<ListVM>();
        }
    }
}
