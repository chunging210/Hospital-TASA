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
            // DateTime UTC 轉換
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
                    {
                        if (property.GetValueConverter() == null)
                        {
                            var converter = new ValueConverter<DateTime, DateTime>(
                                x => x.ToUniversalTime(),
                                x => DateTime.SpecifyKind(x, DateTimeKind.Utc)
                            );
                            property.SetValueConverter(converter);
                        }
                    }
                }
            }

            // ✅ Query Filter - 使用實例方法
            modelBuilder.Entity<SysRoom>().HasQueryFilter(e =>
                CurrentUserIsAdmin || e.DepartmentId == CurrentUserDepartmentId);

            modelBuilder.Entity<Conference>().HasQueryFilter(e =>
                CurrentUserIsAdmin || e.DepartmentId == CurrentUserDepartmentId);

            modelBuilder.Entity<ConferenceTemplate>().HasQueryFilter(e =>
                CurrentUserIsAdmin || e.DepartmentId == CurrentUserDepartmentId);

            modelBuilder.Entity<Equipment>().HasQueryFilter(e =>
                CurrentUserIsAdmin || e.DepartmentId == CurrentUserDepartmentId);
        }

    }
}