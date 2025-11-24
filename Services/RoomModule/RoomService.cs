using Microsoft.EntityFrameworkCore;
using TASA.Extensions;
using TASA.Models;
using TASA.Program;

namespace TASA.Services.RoomModule
{
    public class RoomService(TASAContext db, ServiceWrapper service) : IService
    {
        public record ListVM
        {
            public uint No { get; set; }
            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public bool IsEnabled { get; set; }
            public DateTime CreateAt { get; set; }
        }
        /// <summary>
        /// 列表
        /// </summary>
        public IQueryable<ListVM> List(BaseQueryVM query)
        {
            return db.SysRoom
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
            public string? Description { get; set; }
            public bool IsEnabled { get; set; }
        }
        public DetailVM? Detail(Guid id)
        {
            return db.SysRoom
                .AsNoTracking()
                .WhereNotDeleted()
                .Mapping<DetailVM>()
                .FirstOrDefault(x => x.Id == id);
        }

        public void Insert(DetailVM vm)
        {
            var userid = service.UserClaimsService.Me()?.Id;
            if (db.SysRoom.WhereNotDeleted().Any(x => x.Name == vm.Name))
            {
                throw new HttpException("會議室已存在");
            }

            var newSysRoom = new SysRoom()
            {
                Id = Guid.NewGuid(),
                Type = 1,
                Name = vm.Name,
                Description = vm.Description,
                IsEnabled = vm.IsEnabled,
                CreateAt = DateTime.Now,
                CreateBy = userid!.Value
            };
            db.SysRoom.Add(newSysRoom);
            db.SaveChanges();
            _ = service.LogServices.LogAsync("會議室新增", $"{newSysRoom.Name}({newSysRoom.Id}) IsEnabled:{newSysRoom.IsEnabled}");
        }

        public void Update(DetailVM vm)
        {
            var data = db.SysRoom
                .WhereNotDeleted()
                .FirstOrDefault(x => x.Id == vm.Id);
            if (data != null)
            {
                data.Name = vm.Name;
                data.Description = vm.Description;
                data.IsEnabled = vm.IsEnabled;
                db.SaveChanges();
                _ = service.LogServices.LogAsync("會議室編輯", $"{data.Name}({data.Id}) IsEnabled:{data.IsEnabled}");
            }
        }

        public void Delete(Guid id)
        {
            var data = db.SysRoom
                .WhereNotDeleted()
                .FirstOrDefault(x => x.Id == id);

            if (data != null)
            {
                data.DeleteAt = DateTime.UtcNow;
                db.SaveChanges();
                _ = service.LogServices.LogAsync("會議室刪除", $"{data.Name}({data.Id})");
            }
        }
    }
}
