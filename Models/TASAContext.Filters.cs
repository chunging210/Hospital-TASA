// Models/TASAContext.Filters.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TASA.Services.AuthModule;
using TASA.Models.Auth;

namespace TASA.Models
{
    public partial class TASAContext
    {

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
        {

            // ✅ Query Filter - 使用實例方法
            modelBuilder.Entity<SysRoom>().HasQueryFilter(e =>
                CurrentUserIsAdmin || e.DepartmentId == CurrentUserDepartmentId);

            modelBuilder.Entity<Conference>().HasQueryFilter(e =>
                CurrentUserIsAdmin || e.DepartmentId == CurrentUserDepartmentId);

            modelBuilder.Entity<ConferenceTemplate>().HasQueryFilter(e =>
                CurrentUserIsAdmin || e.DepartmentId == CurrentUserDepartmentId);

            modelBuilder.Entity<Equipment>().HasQueryFilter(e =>
                CurrentUserIsAdmin || e.DepartmentId == CurrentUserDepartmentId);

            modelBuilder.Entity<LogSys>().HasQueryFilter(e =>
                CurrentUserIsAdmin || e.DepartmentId == CurrentUserDepartmentId);
        }

    }
}