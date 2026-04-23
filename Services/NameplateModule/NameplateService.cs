#nullable disable
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using TASA.Extensions;
using TASA.Models;
using TASA.Program;

namespace TASA.Services.NameplateModule;

public class NameplateService(TASAContext db, ServiceWrapper service) : IService
{
    public record ListVM
    {
        public Guid Id { get; set; }
        public byte DeviceType { get; set; }
        public string DeviceTypeName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Host { get; set; }
        public int? Port { get; set; }
        public string? Mac { get; set; }
        public Guid? DistributorId { get; set; }
        public string? DistributorName { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime CreateAt { get; set; }
    }

    public record DetailVM
    {
        public Guid? Id { get; set; }
        [JsonRequired] public byte DeviceType { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Host { get; set; }
        public int? Port { get; set; }
        public string? Mac { get; set; }
        public Guid? DistributorId { get; set; }
        public bool IsEnabled { get; set; } = true;
    }

    public IQueryable<ListVM> List(string? keyword, byte? deviceType, bool? isEnabled)
    {
        var q = db.SysNameplate
            .AsNoTracking()
            .WhereNotDeleted()
            .WhereIf(deviceType.HasValue, x => x.DeviceType == deviceType)
            .WhereIf(isEnabled.HasValue, x => x.IsEnabled == isEnabled)
            .WhereIf(keyword, x => x.Name.Contains(keyword!) || (x.Host != null && x.Host.Contains(keyword!)));

        return q.Select(x => new ListVM
        {
            Id           = x.Id,
            DeviceType   = x.DeviceType,
            DeviceTypeName = x.DeviceType == 0 ? "分配器" : "桌牌",
            Name         = x.Name,
            Host         = x.Host,
            Port         = x.Port,
            Mac          = x.Mac,
            DistributorId   = x.DistributorId,
            DistributorName = x.Distributor != null ? x.Distributor.Name : null,
            IsEnabled    = x.IsEnabled,
            CreateAt     = x.CreateAt,
        });
    }

    public DetailVM? Detail(Guid id)
    {
        return db.SysNameplate
            .AsNoTracking()
            .WhereNotDeleted()
            .Where(x => x.Id == id)
            .Select(x => new DetailVM
            {
                Id           = x.Id,
                DeviceType   = x.DeviceType,
                Name         = x.Name,
                    Host         = x.Host,
                Port         = x.Port,
                Mac          = x.Mac,
                DistributorId = x.DistributorId,
                IsEnabled    = x.IsEnabled,
            })
            .FirstOrDefault();
    }

    public void Insert(DetailVM vm)
    {
        var user = service.UserClaimsService.Me()
            ?? throw new UnauthorizedAccessException();

        db.SysNameplate.Add(new SysNameplate
        {
            Id           = Guid.NewGuid(),
            DeviceType   = vm.DeviceType,
            Name         = vm.Name,
            Host         = vm.Host,
            Port         = vm.Port,
            Mac          = vm.Mac,
            DistributorId = vm.DistributorId,
            IsEnabled    = vm.IsEnabled,
            CreateAt     = DateTime.Now,
            CreateBy     = user.Id!.Value,
        });
        db.SaveChanges();
    }

    public void Update(DetailVM vm)
    {
        var entity = db.SysNameplate.WhereNotDeleted().FirstOrDefault(x => x.Id == vm.Id)
            ?? throw new KeyNotFoundException();

        entity.DeviceType   = vm.DeviceType;
        entity.Name         = vm.Name;
        entity.Host         = vm.Host;
        entity.Port         = vm.Port;
        entity.Mac          = vm.Mac;
        entity.DistributorId = vm.DistributorId;
        entity.IsEnabled    = vm.IsEnabled;
        db.SaveChanges();
    }

    public void Delete(Guid id)
    {
        var entity = db.SysNameplate.WhereNotDeleted().FirstOrDefault(x => x.Id == id)
            ?? throw new KeyNotFoundException();
        entity.DeleteAt = DateTime.Now;
        db.SaveChanges();
    }

    /// <summary>取得所有分配器（供桌牌選擇關聯用）</summary>
    public List<IdNameVM> DistributorOptions()
    {
        return db.SysNameplate
            .AsNoTracking()
            .WhereNotDeleted()
            .Where(x => x.DeviceType == 0 && x.IsEnabled)
            .Select(x => new IdNameVM { Id = x.Id, Name = x.Name })
            .ToList();
    }
}
