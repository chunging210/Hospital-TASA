using Microsoft.EntityFrameworkCore;
using TASA.Extensions;
using TASA.Models;
using TASA.Program;
using TASA.Program.ModelState;

namespace TASA.Services.ConferenceTemplateMoule
{
    public class ConferenceTemplateService(TASAContext db, ServiceWrapper service) : IService
    {
        public record ListVM
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public byte DurationHH { get; set; }
            public byte DurationSS { get; set; }
            public string? Host { get; set; }
            public IEnumerable<string> Room { get; set; } = [];
            public IEnumerable<string> Department { get; set; } = [];
        }
        /// <summary>
        /// 列表
        /// </summary>
        public IQueryable<ListVM> List(Guid? userId)
        {
            return db.ConferenceTemplate
                .AsNoTracking()
                .WhereNotDeleted()
                .WhereIf(userId.HasValue, x => x.CreateBy == userId)
                .Mapping(x => new ListVM()
                {
                    Host = x.ConferenceTemplateUser.Where(y => y.IsHost).Select(y => y.User.Name).FirstOrDefault(),
                    Room = x.Room.Select(y => y.Name).ToList(),
                    Department = x.Department.Select(y => y.Name).ToList(),
                });
        }

        public record DetailVM
        {
            public record RoomVM : IdNameVM
            {

            }
            public record UserVM : IdNameVM
            {
                public bool IsAttendees { get; set; }
                public bool IsHost { get; set; }
                public bool IsRecorder { get; set; }
            }
            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public byte UsageType { get; set; }
            public byte? MCU { get; set; }
            public bool Recording { get; set; }
            public string Description { get; set; } = string.Empty;
            public byte DurationHH { get; set; }
            public byte DurationSS { get; set; }
            public string RRule { get; set; } = string.Empty;
            public IEnumerable<RoomVM> Room { get; set; } = [];
            public IEnumerable<UserVM> User { get; set; } = [];
            public IEnumerable<IdNameVM> Department { get; set; } = [];
        }

        /// <summary>
        /// 詳細資料
        /// </summary>        
        public DetailVM? Detail(Guid id)
        {
            return db.ConferenceTemplate
                .AsNoTracking()
                .WhereNotDeleted()
                .Mapping(x => new DetailVM()
                {
                    Room = x.Room.Select(x => new DetailVM.RoomVM()
                    {
                        Id = x.Id,
                        Name = x.Name,
                    }),
                    User = x.ConferenceTemplateUser.Select(x => new DetailVM.UserVM()
                    {
                        Id = x.User.Id,
                        Name = x.User.Name,
                        IsAttendees = x.IsAttendees,
                        IsHost = x.IsHost,
                        IsRecorder = x.IsRecorder
                    }),
                    Department = x.Department.Select(x => new IdNameVM()
                    {
                        Id = x.Id,
                        Name = x.Name
                    }),
                })
                .FirstOrDefault(x => x.Id == id);
        }

        private static List<ConferenceTemplateUser> GetUsers(IEnumerable<Guid> attendees, Guid? host, Guid? recorder)
        {
            var users = attendees.Select(x => new ConferenceTemplateUser { UserId = x, IsAttendees = true, }).ToList();
            if (host.HasValue)
            {
                var user = users.FirstOrDefault(x => x.UserId == host.Value);
                if (user == null)
                {
                    user = new ConferenceTemplateUser { UserId = host.Value };
                    users.Add(user);
                }
                user.IsHost = true;
            }
            if (recorder.HasValue)
            {
                var user = users.FirstOrDefault(x => x.UserId == recorder.Value);
                if (user == null)
                {
                    user = new ConferenceTemplateUser { UserId = recorder.Value };
                    users.Add(user);
                }
                user.IsRecorder = true;
            }
            return users;
        }

        public record InsertVM
        {
            public Guid? Id { get; set; }
            [RequiredI18n(ErrorMessage = "會議名稱是必要項")]
            public string? Name { get; set; }
            [RequiredI18n(ErrorMessage = "會議類型是必要項")]
            public byte? UsageType { get; set; }
            public byte? MCU { get; set; }
            public bool Recording { get; set; }
            public string? Description { get; set; }
            [RequiredI18n(ErrorMessage = "持續時間是必要項")]
            public byte? DurationHH { get; set; }
            [RequiredI18n(ErrorMessage = "持續時間是必要項")]
            public byte? DurationSS { get; set; }
            public string? RRule { get; set; }
            [MinLengthI18n(1, "IsRoomRequired", ErrorMessage = "與會地點是必要項")]
            public IEnumerable<Guid> Room { get; set; } = [];
            public IEnumerable<Guid> User { get; set; } = [];
            public IEnumerable<Guid> Department { get; set; } = [];
            public Guid? Host { get; set; }
            public Guid? Recorder { get; set; }

            private bool IsRoomRequired()
            {
                return UsageType == 1;
            }
        }
        /// <summary>
        /// 新增
        /// </summary>
        public Guid Insert(InsertVM vm)
        {
            var userId = service.UserClaimsService.Me()?.Id;
            if (db.ConferenceTemplate.WhereNotDeleted().Any(x => x.Name == vm.Name && x.CreateBy == userId))
            {
                throw new HttpException(I18nMessgae.DataExists);
            }

            var data = new ConferenceTemplate
            {
                Id = Guid.NewGuid(),
                Name = vm.Name,
                UsageType = vm.UsageType!.Value,
                MCU = vm.UsageType == 2 ? vm.MCU : null,
                Recording = vm.UsageType == 2 && vm.Recording,
                Description = vm.Description,
                DurationHH = vm.DurationHH!.Value,
                DurationSS = vm.DurationSS!.Value,
                RRule = vm.RRule,
                Room = [.. db.SysRoom.Where(x => vm.Room.Contains(x.Id))],
                ConferenceTemplateUser = GetUsers(vm.User, vm.Host, vm.Recorder),
                Department = [.. db.SysDepartment.Where(x => vm.Department.Contains(x.Id))],
                CreateBy = userId!.Value,
                CreateAt = DateTime.Now,
            };

            db.ConferenceTemplate.Add(data);
            db.SaveChanges();
            _ = service.LogServices.LogAsync("會議範本新增", $"{data.Name}({data.Id})");

            return data.Id;
        }

        /// <summary>
        /// 編輯
        /// </summary>
        public void Update(InsertVM vm)
        {
            var userId = service.UserClaimsService.Me()?.Id;
            if (db.ConferenceTemplate.WhereNotDeleted().Any(x => x.Id != vm.Id && x.Name == vm.Name && x.CreateBy == userId))
            {
                throw new HttpException(I18nMessgae.DataExists);
            }

            var data = db.ConferenceTemplate
                .Include(x => x.Room)
                .Include(x => x.Department)
                .Include(x => x.ConferenceTemplateUser)
                .WhereNotDeleted()
                .FirstOrDefault(x => x.Id == vm.Id) ?? throw new HttpException(I18nMessgae.DataNotFound);

            data.Name = vm.Name;
            data.UsageType = vm.UsageType!.Value;
            data.MCU = vm.UsageType == 2 ? vm.MCU : null;
            data.Recording = vm.UsageType == 2 && vm.Recording;
            data.Description = vm.Description;
            data.DurationHH = vm.DurationHH!.Value;
            data.DurationSS = vm.DurationSS!.Value;
            data.Room = [.. db.SysRoom.Where(x => vm.Room.Contains(x.Id))];
            data.ConferenceTemplateUser = GetUsers(vm.User, vm.Host, vm.Recorder);
            data.Department = [.. db.SysDepartment.Where(x => vm.Department.Contains(x.Id))];

            db.SaveChanges();
            _ = service.LogServices.LogAsync("會議範本編輯", $"{data.Name}({data.Id})");
        }

        /// <summary>
        /// 刪除
        /// </summary>
        public void Delete(Guid id)
        {
            var data = db.ConferenceTemplate
                .WhereNotDeleted()
                .FirstOrDefault(x => x.Id == id);

            if (data != null)
            {
                data.DeleteAt = DateTime.Now;
                db.SaveChanges();
                _ = service.LogServices.LogAsync("會議範本刪除", $"{data.Name}({data.Id})");
            }
        }
    }
}
