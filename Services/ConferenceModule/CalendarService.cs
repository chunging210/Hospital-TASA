using Microsoft.EntityFrameworkCore;
using TASA.Extensions;
using TASA.Models;
using TASA.Program;

namespace TASA.Services.ConferenceModule
{
    public class CalendarService(TASAContext db, ServiceWrapper service) : IService
    {
        private IQueryable<Guid> UserConferences(Guid? userId)
        {
            return db.Conference
                .AsNoTracking()
                .Where(x => x.CreateBy == userId || x.ConferenceUser.Any(y => y.UserId == userId))
                .Select(x => x.Id);
        }

        public record ListVM
        {
            public record ExtendedPropsVM
            {
                public Guid Id { get; set; }
                public IEnumerable<string> Room { get; set; } = [];
                public IEnumerable<string> User { get; set; } = [];
                public IEnumerable<string> Department { get; set; } = [];
                public bool Self { get; set; }
            }
            public string Title { get; set; } = string.Empty;
            public DateTime Start { get; set; }
            public DateTime End { get; set; }
            public ExtendedPropsVM? ExtendedProps { get; set; }
        }
        /// <summary>
        /// FullCalendar 會議列表
        /// </summary>
        public IQueryable<ListVM> List(BaseQueryVM query)
        {
            query.Start ??= new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            query.End ??= query.Start.Value.AddMonths(1);
            var userId = service.UserClaimsService.Me()?.Id;
            var ConferencesIds = UserConferences(userId);
            return db.Conference
                .AsNoTracking()
                .WhereNotDeleted()
                .WhereIf(query.Start.HasValue, x => query.Start <= x.StartTime)
                .WhereIf(query.End.HasValue, x => x.StartTime <= query.End)
                .WhereIf(query.Self == true, x => ConferencesIds.Contains(x.Id))
                .Mapping(x => new ListVM()
                {
                    Title = x.Name,
                    Start = x.StartTime,
                    End = x.EndTime,
                    ExtendedProps = new ListVM.ExtendedPropsVM()
                    {
                        Id = x.Id,
                        Room = x.Room.Select(y => y.Name).ToList(),
                        User = x.ConferenceUser.Select(y => y.User.Name).ToList(),
                        Department = x.Department.Select(y => y.Name).ToList(),
                        Self = ConferencesIds.Contains(x.Id),
                    }
                });
        }

        public record RecentVM
        {
            public string Name { get; set; } = string.Empty;
            public DateTime StartTime { get; set; }
            public bool Self { get; set; }
        }
        /// <summary>
        /// 最近活動
        /// </summary>
        public IQueryable<RecentVM> Recent(int length = 3)
        {
            var userId = service.UserClaimsService.Me()?.Id;
            var ConferencesIds = UserConferences(userId);
            return db.Conference
                .AsNoTracking()
                .WhereNotDeleted()
                .Where(x => x.StartTime >= DateTime.UtcNow && ConferencesIds.Contains(x.Id))
                .OrderBy(x => x.StartTime)
                .Take(length)
                .Mapping(x => new RecentVM()
                {
                    Self = x.CreateBy == userId
                });
        }
    }
}
