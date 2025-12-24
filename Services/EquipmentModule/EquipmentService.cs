using Microsoft.EntityFrameworkCore;
using TASA.Extensions;
using TASA.Models;
using TASA.Program;

namespace TASA.Services.EquipmentModule
{
    public class EquipmentService(TASAContext db, ServiceWrapper service) : IService
    {
        public record ListVM
        {
            public Guid Id { get; set; }
            public string? Name { get; set; } = string.Empty;
            public string? Host { get; set; } = string.Empty;
            public int? Port { get; set; }
            public bool IsEnabled { get; set; }
            public DateTime CreateAt { get; set; }
        }
        /// <summary>
        /// 列表
        /// </summary>
        public IQueryable<ListVM> List(BaseQueryVM query)
        {
            Console.WriteLine("123");
            return db.Equipment
                .AsNoTracking()
                .WhereNotDeleted()
                .WhereIf(query.IsEnabled.HasValue, x => x.IsEnabled == query.IsEnabled)
                .WhereIf(query.Keyword, x => x.Name.Contains(query.Keyword!))
                .Mapping<ListVM>();
        }

        public record DetailVM
        {
            public Guid? Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public byte Type { get; set; }
            public string? Host { get; set; }
            public int? Port { get; set; }
            public string? Account { get; set; }
            public string? Password { get; set; }
            public bool IsEnabled { get; set; }
        }
        public DetailVM? Detail(Guid id)
        {
            return db.Equipment
                .AsNoTracking()
                .WhereNotDeleted()
                .Mapping<DetailVM>()
                .FirstOrDefault(x => x.Id == id);
        }

        public void Insert(DetailVM vm)
        {
            var userid = service.UserClaimsService.Me()?.Id;
            if (db.Equipment.WhereNotDeleted().Any(x => x.Name == vm.Name))
            {
                throw new HttpException("設備已存在");
            }

            var newEquipment = new Equipment()
            {
                Id = Guid.NewGuid(),
                Name = vm.Name,
                Type = 3,
                Host = vm.Host,
                Port = vm.Port,
                Account = vm.Account,
                Password = vm.Password,
                IsEnabled = vm.IsEnabled,
                CreateAt = DateTime.Now,
                CreateBy = userid!.Value
            };
            db.Equipment.Add(newEquipment);
            db.SaveChanges();
            _ = service.LogServices.LogAsync("設備新增", $"{newEquipment.Name}({newEquipment.Id}) IsEnabled:{newEquipment.IsEnabled}");
        }

        public void Update(DetailVM vm)
        {
            var data = db.Equipment
                .WhereNotDeleted()
                .FirstOrDefault(x => x.Id == vm.Id);
            if (data != null)
            {
                data.Name = vm.Name;
                data.Type = vm.Type;
                data.Host = vm.Host;
                data.Port = vm.Port;
                data.Account = vm.Account;
                data.Password = vm.Password;
                data.IsEnabled = vm.IsEnabled;
                db.SaveChanges();
                _ = service.LogServices.LogAsync("設備編輯", $"{data.Name}({data.Id}) IsEnabled:{data.IsEnabled}");
            }
        }

        public void Delete(Guid id)
        {
            var data = db.Equipment
                .WhereNotDeleted()
                .FirstOrDefault(x => x.Id == id);

            if (data != null)
            {
                data.DeleteAt = DateTime.UtcNow;
                db.SaveChanges();
                _ = service.LogServices.LogAsync("設備刪除", $"{data.Name}({data.Id})");
            }
        }
    }
}
