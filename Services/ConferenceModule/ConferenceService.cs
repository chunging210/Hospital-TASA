using Microsoft.EntityFrameworkCore;
using TASA.Extensions;
using TASA.Models;
using TASA.Program;
using TASA.Program.ModelState;

namespace TASA.Services.ConferenceModule
{
    public class ConferenceService(TASAContext db, ServiceWrapper service) : IService
    {
        public SettingServices.SettingsModel.UCMSSettings ConferenceSettings { get { return service.SettingServices.GetSettings().UCNS; } }
        public DateTime PreparationTime { get { return DateTime.UtcNow.AddMinutes(ConferenceSettings.BeforeStart); } }

        public record ListVM
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
            public byte DurationHH { get; set; }
            public byte DurationSS { get; set; }
            public string? Host { get; set; }
            public IEnumerable<string> Room { get; set; } = [];
            public IEnumerable<string> ConferenceUser { get; set; } = [];
            public IEnumerable<string> Department { get; set; } = [];
            public Guid CreateBy { get; set; }
            public string CreateByName { get; set; } = string.Empty;
            public byte Status { get; set; }
            public bool CanEdit { get; set; }
            public bool Recording7 { get; set; }
            public bool ZeroTouch { get; set; }
        }
        /// <summary>
        /// 列表
        /// </summary>
        public IQueryable<ListVM> List(BaseQueryVM query)
        {
            return db.Conference
                .AsNoTracking()
                .WhereNotDeleted()
                .WhereIf(query.Start.HasValue, x => query.Start <= x.StartTime)
                .WhereIf(query.End.HasValue, x => x.StartTime <= query.End)
                .WhereIf(query.RoomId.HasValue, x => x.Room.Any(y => y.Id == query.RoomId))
                .WhereIf(query.DepartmentId.HasValue, x => x.Department.Any(y => y.Id == query.DepartmentId))
                .WhereIf(query.UserId.HasValue, x => x.CreateBy == query.UserId)
                .WhereIf(query.Keyword, x => x.Name.Contains(query.Keyword!))
                .OrderByDescending(x => x.StartTime)
                .Mapping(x => new ListVM()
                {
                    Host = x.ConferenceUser.Where(y => y.IsHost).Select(y => y.User.Name).FirstOrDefault(),
                    Room = x.Room.Select(y => y.Name).ToList(),
                    ConferenceUser = x.ConferenceUser.Select(y => y.User.Name).ToList(),
                    Department = x.Department.Select(y => y.Name).ToList(),
                    CreateByName = x.CreateByNavigation.Name,
                    CanEdit = x.StartTime > PreparationTime,
                    Recording7 = x.Status == 4 && x.MCU == 7,
                    //ZeroTouch = x.Status == 3 && x.MCU == 7 && x.Room.SelectMany(y => y.RoomEquipment).Any(y => y.Type == 1 || y.Type == 2),
                });
        }

        public record DetailVM
        {
            public record RoomVM : IdNameVM
            {
                public IEnumerable<IdNameVM> Ecs { get; set; } = [];
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
            public DateTime StartTime { get; set; }
            public bool StartNow { get; set; }
            public DateTime EndTime { get; set; }
            public byte DurationHH { get; set; }
            public byte DurationSS { get; set; }
            public string RRule { get; set; } = string.Empty;
            public IEnumerable<RoomVM> Room { get; set; } = [];
            public IEnumerable<UserVM> User { get; set; } = [];
            public IEnumerable<IdNameVM> Department { get; set; } = [];
            public string CreateBy { get; set; } = string.Empty;
            public ConferenceWebex? Webex { get; set; }
            public bool CanEdit { get; set; }
        }

        /// <summary>
        /// 詳細資料
        /// </summary>        
        public DetailVM? Detail(Guid id)
        {
            return db.Conference
                .AsNoTracking()
                .WhereNotDeleted()
                .Mapping(x => new DetailVM()
                {
                    Room = x.Room.Select(x => new DetailVM.RoomVM()
                    {
                        Id = x.Id,
                        Name = x.Name,
                        Ecs = x.Ecs.Select(y => new IdNameVM() { Id = y.Id, Name = y.Name })
                    }),
                    User = x.ConferenceUser.Select(x => new DetailVM.UserVM()
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
                    CreateBy = x.CreateByNavigation.Name,
                    CanEdit = x.StartTime > PreparationTime,
                    Webex = x.ConferenceWebex
                })
                .FirstOrDefault(x => x.Id == id);
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
            [RequiredI18n, StartTimeGreaterThanNow("StartNow")]
            public DateTime? StartTime { get; set; }
            public bool StartNow { get; set; }
            [RequiredI18n(ErrorMessage = "持續時間是必要項")]
            public byte? DurationHH { get; set; }
            [RequiredI18n(ErrorMessage = "持續時間是必要項")]
            public byte? DurationSS { get; set; }
            public string? RRule { get; set; }
            [MinLengthI18n(1, "IsRoomRequired", ErrorMessage = "與會地點是必要項")]
            public List<Guid> Room { get; set; } = [];
            public List<Guid> Ecs { get; set; } = [];
            public List<Guid> User { get; set; } = [];
            public List<Guid> Department { get; set; } = [];
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
            if (db.Conference.WhereNotDeleted().Any(x => x.Name == vm.Name && x.StartTime == vm.StartTime))
            {
                throw new HttpException(I18nMessgae.DataExists);
            }

            var StartTime = vm.StartNow ? DateTime.UtcNow.AddSeconds(ConferenceSettings.DelayStartTime) : vm.StartTime!.Value;
            var EndTime = StartTime.AddHours(vm.DurationHH!.Value).AddMinutes(vm.DurationSS!.Value);
            var used = db.Conference
                .AsNoTracking()
                .WhereNotDeleted()
                .Where(x => x.Id != vm.Id && x.StartTime <= EndTime && StartTime <= x.EndTime)
                .SelectMany(x => x.Room)
                .Where(x => vm.Room.Contains(x.Id))
                .Distinct()
                .ToDictionary(x => x.Name, x => stringArray);
            if (used.Count > 0)
            {
                throw new HttpException(used);
            }

            var data = new Conference
            {
                Id = Guid.NewGuid(),
                Name = vm.Name,
                UsageType = vm.UsageType!.Value,
                MCU = vm.UsageType == 2 ? vm.MCU : null,
                Recording = vm.UsageType == 2 && vm.Recording,
                Description = vm.Description,
                StartTime = StartTime,
                DurationHH = vm.DurationHH!.Value,
                DurationSS = vm.DurationSS!.Value,
                EndTime = EndTime,
                RRule = vm.RRule,
                Room = [.. db.SysRoom.Where(x => vm.Room.Contains(x.Id))],
                Ecs = [.. db.Ecs.Where(x => vm.Ecs.Contains(x.Id))],
                ConferenceUser = GetUsers(vm.User, vm.Host, vm.Recorder),
                Department = [.. db.SysDepartment.Where(x => vm.Department.Contains(x.Id))],
                CreateBy = userId!.Value,
                CreateAt = DateTime.UtcNow,
            };
            data.Status = GetStatus(vm.StartNow, data);
            data.ConferenceWebex = service.WebexMeetingService.Create(data);
            //if (webex != null)
            //{
            //    data.Webex = webex;
            //    if (vm.StartNow)
            //    {
            //        var rooms = data.Rooms.Select(x => x.Id);
            //        var equipments = db.Set<RoomsEquipment>()
            //            .AsNoTracking()
            //            .WhereNotDeleted()
            //            .Where(x => x.IsEnabled && rooms.Contains(x.RoomsId));
            //        RoomsEquipmentService.ZeroTouch(webex, equipments);
            //    }
            //}

            db.Conference.Add(data);
            db.SaveChanges();
            service.ConferenceMail.New(data);
            _ = _ = service.LogServices.LogAsync("會議新增", $"{data.Name}({data.Id})");
            return data.Id;
        }

        /// <summary>
        /// 編輯
        /// </summary>
        public void Update(InsertVM vm)
        {
            if (db.Conference.WhereNotDeleted().Any(x => x.Id != vm.Id && x.Name == vm.Name && x.StartTime == vm.StartTime))
            {
                throw new HttpException(I18nMessgae.DataExists);
            }

            var StartTime = vm.StartNow ? DateTime.UtcNow.AddSeconds(ConferenceSettings.DelayStartTime) : vm.StartTime!.Value;
            var EndTime = StartTime.AddHours(vm.DurationHH!.Value).AddMinutes(vm.DurationSS!.Value);
            var used = db.Conference
                .AsNoTracking()
                .WhereNotDeleted()
                .Where(x => x.Id != vm.Id && x.StartTime <= EndTime && StartTime <= x.EndTime)
                .SelectMany(x => x.Room)
                .Where(x => vm.Room.Contains(x.Id))
                .Distinct()
                .ToDictionary(x => x.Name, x => stringArray);
            if (used.Count > 0)
            {
                throw new HttpException(used);
            }

            var data = db.Conference
                //.Include(x => x.ConferenceWebex)
                .Include(x => x.Room)
                .Include(x => x.Ecs)
                .Include(x => x.ConferenceUser)
                .Include(x => x.Department)
                .WhereNotDeleted()
                .FirstOrDefault(x => x.Id == vm.Id) ?? throw new HttpException(I18nMessgae.DataNotFound);

            data.Name = vm.Name;
            data.UsageType = vm.UsageType!.Value;
            data.MCU = vm.UsageType == 2 ? vm.MCU : null;
            data.Recording = vm.UsageType == 2 && vm.Recording;
            data.Description = vm.Description;
            data.StartTime = StartTime;
            data.DurationHH = vm.DurationHH!.Value;
            data.DurationSS = vm.DurationSS!.Value;
            data.EndTime = EndTime;
            data.Room = [.. db.SysRoom.Where(x => vm.Room.Contains(x.Id))];
            data.Ecs = [.. db.Ecs.Where(x => vm.Ecs.Contains(x.Id))];
            data.ConferenceUser = GetUsers(vm.User, vm.Host, vm.Recorder);
            data.Department = [.. db.SysDepartment.Where(x => vm.Department.Contains(x.Id))];
            data.Status = GetStatus(vm.StartNow, data);
            data.ConferenceWebex = service.WebexMeetingService.Create(data);
            //if (webex != null)
            //{
            //    data.Webex = webex;
            //}

            db.SaveChanges();
            service.ConferenceMail.New(data, "[會議修改通知]");
            _ = _ = service.LogServices.LogAsync("會議編輯", $"{data.Name}({data.Id})");
        }

        private static readonly string[] stringArray = ["已被預約"];

        private static List<ConferenceUser> GetUsers(IEnumerable<Guid> attendees, Guid? host, Guid? recorder)
        {
            var users = attendees.Select(x => new ConferenceUser { UserId = x, IsAttendees = true, }).ToList();
            if (host.HasValue)
            {
                var user = users.FirstOrDefault(x => x.UserId == host.Value);
                if (user == null)
                {
                    user = new ConferenceUser { UserId = host.Value };
                    users.Add(user);
                }
                user.IsHost = true;
            }
            if (recorder.HasValue)
            {
                var user = users.FirstOrDefault(x => x.UserId == recorder.Value);
                if (user == null)
                {
                    user = new ConferenceUser { UserId = recorder.Value };
                    users.Add(user);
                }
                user.IsRecorder = true;
            }
            return users;
        }

        public byte GetStatus(bool startNow, Conference conference)
        {
            if (DateTime.UtcNow > (conference.FinishTime?.ToUniversalTime() ?? conference.EndTime.ToUniversalTime()))
            {
                return 4;
            }
            if (startNow || DateTime.UtcNow > conference.StartTime.ToUniversalTime())
            {
                service.JobService.DoEcs(conference);
                return 3;
            }
            if (DateTime.UtcNow.AddMinutes(ConferenceSettings.BeforeStart) > conference.StartTime.ToUniversalTime())
            {
                service.JobService.DoEcs(conference);
                return 2;
            }
            return 1;
        }

        /// <summary>
        /// 提早結束
        /// </summary>
        public void End(Guid id)
        {
            var data = db.Conference
                .WhereNotDeleted()
                .FirstOrDefault(x => x.Id == id);

            if (data != null)
            {
                data.FinishTime = DateTime.UtcNow;
                data.Status = 4;
                db.SaveChanges();
                _ = service.LogServices.LogAsync("會議結束", $"{data.Name}({data.Id})");
            }
        }

        /// <summary>
        /// 刪除
        /// </summary>
        public void Delete(Guid id)
        {
            var data = db.Conference
                .WhereNotDeleted()
                .FirstOrDefault(x => x.Id == id);

            if (data != null)
            {
                data.DeleteAt = DateTime.UtcNow;
                db.SaveChanges();
                _ = service.LogServices.LogAsync("會議刪除", $"{data.Name}({data.Id})");
            }
        }
    }
}
