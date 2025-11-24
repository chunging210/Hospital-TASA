using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace TASA.Models
{
    public partial class TASAContext : DbContext
    {
        partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
        {
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
                    {
                        if (property.GetValueConverter() == null)
                        {
                            var converter = new ValueConverter<DateTime, DateTime>(x => x.ToUniversalTime(), x => DateTime.SpecifyKind(x, DateTimeKind.Utc));
                            property.SetValueConverter(converter);
                        }
                    }
                }
            }
        }
    }
}
