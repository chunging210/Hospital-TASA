using Microsoft.EntityFrameworkCore;
using TASA.Extensions;
using TASA.Models;
using TASA.Program;

namespace TASA.Services.WebexModule
{
    public class WebexService(TASAContext db, ServiceWrapper service) : IService
    {
        public record ListVM
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Client_id { get; set; } = string.Empty;
            public DateTime? Expires { get; set; }
            public DateTime? Refresh_token_expires { get; set; }
            public bool IsEnabled { get; set; }
            public DateTime CreateAt { get; set; }
        }
        /// <summary>
        /// 列表
        /// </summary>
        public IQueryable<ListVM> List(BaseQueryVM query)
        {
            return db.Webex
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
            public string Client_id { get; set; } = string.Empty;
            public string Client_secret { get; set; } = string.Empty;
            public string Access_token { get; set; } = string.Empty;
            public string Refresh_token { get; set; } = string.Empty;
            public bool IsEnabled { get; set; }
        }
        public DetailVM? Detail(Guid id)
        {
            return db.Webex
                .AsNoTracking()
                .WhereNotDeleted()
                .Mapping<DetailVM>()
                .FirstOrDefault(x => x.Id == id);
        }

        public void Insert(DetailVM vm)
        {
            var userid = service.UserClaimsService.Me()?.Id;
            if (db.Webex.WhereNotDeleted().Any(x => x.Name == vm.Name))
            {
                throw new HttpException("Webex已存在");
            }

            var newWebex = new Webex()
            {
                Id = Guid.NewGuid(),
                Name = vm.Name,
                Client_id = vm.Client_id,
                Client_secret = vm.Client_secret,
                Access_token = vm.Access_token,
                Refresh_token = vm.Refresh_token,
                IsEnabled = vm.IsEnabled,
                CreateAt = DateTime.Now,
                CreateBy = userid!.Value
            };
            db.Webex.Add(newWebex);
            db.SaveChanges();
            _ = service.LogServices.LogAsync("Webex新增", $"{newWebex.Name}({newWebex.Id}) IsEnabled:{newWebex.IsEnabled}");
        }

        public void Update(DetailVM vm)
        {
            var data = db.Webex
                .WhereNotDeleted()
                .FirstOrDefault(x => x.Id == vm.Id);
            if (data != null)
            {
                data.Name = vm.Name;
                data.Client_id = vm.Client_id;
                data.Client_secret = vm.Client_secret;
                data.Access_token = vm.Access_token;
                data.Refresh_token = vm.Refresh_token;
                data.IsEnabled = vm.IsEnabled;
                db.SaveChanges();
                _ = service.LogServices.LogAsync("Webex編輯", $"{data.Name}({data.Id}) IsEnabled:{data.IsEnabled}");
            }
        }

        public void Delete(Guid id)
        {
            var data = db.Webex
                .WhereNotDeleted()
                .FirstOrDefault(x => x.Id == id);

            if (data != null)
            {
                data.DeleteAt = DateTime.Now;
                db.SaveChanges();
                _ = service.LogServices.LogAsync("Webex刪除", $"{data.Name}({data.Id})");
            }
        }
    }
}
