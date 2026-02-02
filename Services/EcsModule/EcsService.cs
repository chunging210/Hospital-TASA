using Microsoft.EntityFrameworkCore;
using TASA.Extensions;
using TASA.Models;
using TASA.Program;

namespace TASA.Services.EcsModule
{
    public class EcsService(TASAContext db, ServiceWrapper service, IServiceScopeFactory scopeFactory) : IService
    {
        public record ListVM
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string RoomName { get; set; } = string.Empty;
            public string Macro { get; set; } = string.Empty;
            public bool IsEnabled { get; set; }
            public DateTime CreateAt { get; set; }
        }
        /// <summary>
        /// 列表
        /// </summary>
        public IQueryable<ListVM> List(BaseQueryVM query)
        {
            return db.Ecs
                .AsNoTracking()
                .WhereNotDeleted()
                .WhereIf(query.IsEnabled.HasValue, x => x.IsEnabled == query.IsEnabled)
                .WhereIf(query.Keyword, x => x.Name.Contains(query.Keyword!))
                .Mapping(x => new ListVM()
                {
                    RoomName = x.Room.Name
                });
        }

        public record DetailVM
        {
            public Guid? Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public Guid RoomId { get; set; }
            public string Macro { get; set; } = string.Empty;
            public IEnumerable<Guid> Equipment { get; set; } = [];
            public bool IsEnabled { get; set; }
        }
        public DetailVM? Detail(Guid id)
        {
            return db.Ecs
                .AsNoTracking()
                .WhereNotDeleted()
                .Mapping(x => new DetailVM()
                {
                    Equipment = db.EcsEquipment.Where(y => y.EcsId == x.Id).Select(y => y.EquipmentId).ToList()
                })
                .FirstOrDefault(x => x.Id == id);
        }

        public void Insert(DetailVM vm)
        {
            var userid = service.UserClaimsService.Me()?.Id;
            if (db.Ecs.WhereNotDeleted().Any(x => x.RoomId == vm.RoomId && x.Name == vm.Name))
            {
                throw new HttpException("環控已存在");
            }

            var newEcs = new Ecs()
            {
                Id = Guid.NewGuid(),
                Name = vm.Name,
                RoomId = vm.RoomId,
                Macro = vm.Macro,
                IsEnabled = vm.IsEnabled,
                CreateAt = DateTime.Now,
                CreateBy = userid!.Value
            };
            db.Ecs.Add(newEcs);
            foreach (var item in vm.Equipment)
            {
                db.EcsEquipment.Add(new EcsEquipment()
                {
                    EcsId = newEcs.Id,
                    EquipmentId = item
                });
            }
            db.SaveChanges();
            _ = _ = service.LogServices.LogAsync("環控新增", $"{newEcs.Name}({newEcs.Id}) IsEnabled:{newEcs.IsEnabled}");
        }

        public void Update(DetailVM vm)
        {
            var data = db.Ecs
                .WhereNotDeleted()
                .FirstOrDefault(x => x.Id == vm.Id);
            if (data != null)
            {
                data.Name = vm.Name;
                data.RoomId = vm.RoomId;
                data.Macro = vm.Macro;
                db.EcsEquipment.Where(x => x.EcsId == data.Id).ExecuteDelete();
                foreach (var item in vm.Equipment)
                {
                    db.EcsEquipment.Add(new EcsEquipment()
                    {
                        EcsId = data.Id,
                        EquipmentId = item
                    });
                }
                data.IsEnabled = vm.IsEnabled;
                db.SaveChanges();
                _ = _ = service.LogServices.LogAsync("環控編輯", $"{data.Name}({data.Id}) IsEnabled:{data.IsEnabled}");
            }
        }

        public void Delete(Guid id)
        {
            var data = db.Ecs
                .WhereNotDeleted()
                .FirstOrDefault(x => x.Id == id);

            if (data != null)
            {
                data.DeleteAt = DateTime.Now;
                db.SaveChanges();
                _ = _ = service.LogServices.LogAsync("環控刪除", $"{data.Name}({data.Id})");
            }
        }

        public async Task Send(Guid id, bool isTest = false)
        {
            var data = db.Ecs
                .WhereNotDeleted()
                .FirstOrDefault(x => x.Id == id);
            if (data != null)
            {
                var equipment = db.Equipment
                    .AsNoTracking()
                    .WhereNotDeleted()
                    .Where(x => x.IsEnabled && db.EcsEquipment.Where(y => y.EcsId == id).Select(y => y.EquipmentId).Any(y => y == x.Id))
                    .Select(x => new
                    {
                        x.Host,
                        x.Port
                    })
                    .ToList();

                var userId = service.UserClaimsService.Me()?.Id;

                //Task.Run(async () =>
                //{
                //var scope = scopeFactory.CreateScope();
                //var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<TASAContext>>();
                //await using var db = dbContextFactory.CreateDbContext();
                foreach (var item in equipment)
                {
                    var message = string.Empty;
                    try
                    {
                        await TcpCommandService.SendTcpCommand(item.Host!, item.Port ?? 0, data.Macro);
                    }
                    catch (Exception ex)
                    {
                        message = ex.GetBaseException().Message;
                    }
                    //await LogServices.LogAsync(db, "", $"環控{(isTest ? "測試" : "發送")}", $"{item.Host}:{item.Port} Macro:{data.Macro}/{message}", userId);
                    _ = service.LogServices.LogAsync($"環控{(isTest ? "測試" : "發送")}", $"{item.Host}:{item.Port} Macro:{data.Macro}/{message}");
                }
                //});
            }
        }
    }
}
