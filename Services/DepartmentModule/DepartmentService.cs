using Microsoft.EntityFrameworkCore;
using TASA.Extensions;
using TASA.Models;
using TASA.Program;

namespace TASA.Services.DepartmentModule
{
    public class DepartmentService(TASAContext db, ServiceWrapper service) : IService
    {
        public record ListVM
        {
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
            return db.SysDepartment
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
            public Guid? Parent { get; set; }
            public bool IsEnabled { get; set; }
        }
        public DetailVM? Detail(Guid id)
        {
            return db.SysDepartment
                .AsNoTracking()
                .WhereNotDeleted()
                .Mapping<DetailVM>()
                .FirstOrDefault(x => x.Id == id);
        }

        public void Insert(DetailVM vm)
        {
            var userid = service.UserClaimsService.Me()?.Id;
            if (db.SysDepartment.WhereNotDeleted().Any(x => x.Parent == vm.Parent && x.Name == vm.Name))
            {
                throw new HttpException("單位已存在");
            }

            var newSysDepartment = new SysDepartment()
            {
                Id = Guid.NewGuid(),
                Parent = vm.Parent,
                Name = vm.Name,
                IsEnabled = vm.IsEnabled,
                CreateAt = DateTime.Now,
                CreateBy = userid!.Value
            };
            db.SysDepartment.Add(newSysDepartment);
            db.SaveChanges();
            _ = service.LogServices.LogAsync("單位新增", $"{newSysDepartment.Name}({newSysDepartment.Id}) IsEnabled:{newSysDepartment.IsEnabled}");
        }

        public void Update(DetailVM vm)
        {
            var data = db.SysDepartment
                .WhereNotDeleted()
                .FirstOrDefault(x => x.Id == vm.Id);
            if (data != null)
            {
                data.Parent = vm.Parent;
                data.Name = vm.Name;
                data.IsEnabled = vm.IsEnabled;
                db.SaveChanges();
                _ = service.LogServices.LogAsync("單位編輯", $"{data.Name}({data.Id}) IsEnabled:{data.IsEnabled}");
            }
        }

        public void Delete(Guid id)
        {
            var data = db.SysDepartment
                .WhereNotDeleted()
                .FirstOrDefault(x => x.Id == id);

            if (data != null)
            {
                data.DeleteAt = DateTime.Now;
                db.SaveChanges();
                _ = service.LogServices.LogAsync("單位刪除", $"{data.Name}({data.Id})");
            }
        }
    }
}
