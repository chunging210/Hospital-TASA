using Microsoft.EntityFrameworkCore;
using TASA.Extensions;
using TASA.Models;
using TASA.Models.Enums;
using TASA.Program;

namespace TASA.Services
{
    public class SelectServices(TASAContext db) : IService
    {
        public record RoomVM : IdNameVM
        {
            public IEnumerable<IdNameVM> Ecs { get; set; } = [];
        }

        public record RoomListVM
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string? Building { get; set; }
            public string? Floor { get; set; }
            public string? Number { get; set; }
            public uint Capacity { get; set; }
            public decimal Area { get; set; }
            public RoomStatus Status { get; set; }

            public IEnumerable<string>? Images { get; set; }
        }
        public IQueryable<IdNameVM> Room()
        {
            return db.SysRoom
                .AsNoTracking()
                .WhereNotDeleted()
                .WhereEnabled()
                .Mapping(x => new RoomVM()
                {
                    Ecs = x.Ecs.Where(x => x.IsEnabled && x.DeleteAt == null).Select(x => new IdNameVM() { Id = x.Id, Name = x.Name }).ToList()
                });
        }

        public IQueryable<RoomListVM> RoomList(BaseQueryVM query)
        {
            var q = db.SysRoom
                .AsNoTracking()
                .WhereNotDeleted()
                .WhereEnabled()
                .Where(x => x.Status != RoomStatus.Maintenance);

            if (!string.IsNullOrWhiteSpace(query.Keyword))
            {
                q = q.Where(x => x.Name.Contains(query.Keyword));
            }

            return q.Mapping(x => new RoomListVM
            {
                Id = x.Id,
                Name = x.Name,
                Building = x.Building,
                Floor = x.Floor,
                Number = x.Number,
                Capacity = x.Capacity,
                Area = x.Area,
                Status = x.Status,
                Images = x.Images
                    .Where(i => i.ImagePath != "")
                    .OrderBy(i => i.SortOrder)
                    .Select(i => i.ImagePath)
            });
        }

        public IQueryable<IdNameVM> Role()
        {
            return db.AuthRole
                .AsNoTracking()
                .WhereNotDeleted()
                .WhereEnabled()
                .Mapping<IdNameVM>();
        }

        public IQueryable<IdNameVM> User()
        {
            return db.AuthUser
                .AsNoTracking()
                .WhereNotDeleted()
                .WhereEnabled()
                .Mapping<IdNameVM>();
        }

        public record UserScheduleVM
        {
            public record QueryVM(DateTime ScheduleDate, Guid? DepartmentId, bool? Contains, Guid[]? List, string Keyword);
            public record BusyTimeVM
            {
                public DateTime? StartTime { get; set; }
                public DateTime? EndTime { get; set; }
            };
            public record ReturnVM : IdNameVM
            {
                public string DepartmentName { get; set; } = string.Empty;
                public string Email { get; set; } = string.Empty;
                public IEnumerable<BusyTimeVM> BusyTime { get; set; } = [];
            }
        }
        /// <summary>
        /// 忙碌時段
        /// </summary>
        public IQueryable<UserScheduleVM.ReturnVM> UserSchedule(UserScheduleVM.QueryVM query)
        {
            var start = query.ScheduleDate.Date;
            var end = start.Set(hour: 23, minute: 59, second: 59);
            var list = query.List ?? [];
            var conference =
                from c in db.Conference.AsNoTracking().WhereNotDeleted()
                join cu in db.ConferenceUser.AsNoTracking() on c.Id equals cu.ConferenceId
                where start <= c.StartTime && c.StartTime <= end
                select new
                {
                    cu.User.Id,
                    c.StartTime,
                    c.EndTime
                };
            return db.AuthUser
                .AsNoTracking()
                .WhereNotDeleted()
                .WhereEnabled()
                .WhereIf(query.DepartmentId.HasValue, x => x.DepartmentId == query.DepartmentId)
                .WhereIf(query.Contains.HasValue, x => list.Contains(x.Id) == query.Contains)
                .WhereIf(query.Keyword, x => x.Name.Contains(query.Keyword) || x.Email.Contains(query.Keyword))
                .OrderBy(x => x.Department.Sequence)
                .ThenBy(x => x.No)
                .Select(x => new UserScheduleVM.ReturnVM()
                {
                    Id = x.Id,
                    Name = x.Name,
                    DepartmentName = x.Department.Name,
                    Email = x.Email,
                    BusyTime = conference
                        .Where(y => y.Id == x.Id)
                        .Select(y => new UserScheduleVM.BusyTimeVM()
                        {
                            StartTime = y.StartTime,
                            EndTime = y.EndTime
                        })
                        .ToList(),
                });
        }

        public IQueryable<IdNameVM> Department()
        {
            return db.SysDepartment
                .AsNoTracking()
                .WhereNotDeleted()
                .WhereEnabled()
                .Mapping<IdNameVM>();
        }

        public record TreeVM : IdNameVM
        {
            public IEnumerable<TreeVM> Children { get; set; } = [];
        }
        private static IEnumerable<TreeVM> GetChildren(IEnumerable<SysDepartment> data, Guid? parentId)
        {
            return data
                .Where(x => x.Parent == parentId)
                .OrderBy(x => x.Sequence)
                .Select(x => new TreeVM()
                {
                    Id = x.Id,
                    Name = x.Name,
                    Children = GetChildren(data, x.Id)
                });
        }
        public IEnumerable<TreeVM> DepartmentTree()
        {
            var data = db.SysDepartment.AsNoTracking().WhereNotDeleted().WhereEnabled().ToList();
            return GetChildren(data, null);
        }

        public IQueryable<IdNameVM> ConferenceCreateBy()
        {
            return db.Conference
                .AsNoTracking()
                .WhereNotDeleted()
                .Select(x => new IdNameVM()
                {
                    Id = x.CreateByNavigation.Id,
                    Name = x.CreateByNavigation.Name,
                })
                .Distinct();
        }

        public IQueryable<IdNameVM> Equipment()
        {
            return db.Equipment
                .AsNoTracking()
                .WhereNotDeleted()
                .WhereEnabled()
                .Mapping<IdNameVM>();
        }

        public record ECSVM : IdNameVM
        {
            public Guid RoomId { get; set; }
        }
        public IQueryable<ECSVM> ECS()
        {
            return db.Ecs
                .AsNoTracking()
                .WhereNotDeleted()
                .WhereEnabled()
                .Mapping<ECSVM>();
        }
    }
}