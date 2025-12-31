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
            public uint Capacity { get; set; }
            public decimal Area { get; set; }
            public RoomStatus Status { get; set; }

            public IEnumerable<string>? Images { get; set; }

            public int EquipmentCount { get; set; }

        }

                public record BuildingVM
        {
            public string Building { get; set; } = string.Empty;
            public List<FloorVM> Floors { get; set; } = new();
        }

        public record BuildingFloorVM
        {
            public string Building { get; set; } = string.Empty;
            public List<string> Floors { get; set; } = new();
        }

        public record EquipmentByRoomsQueryVM
        {
            public Guid[]? RoomIds { get; init; }
        }

        public record EquipmentGroupVM
        {
            public List<EquipmentListVM> Shared { get; init; } = new();
            public Dictionary<Guid, List<EquipmentListVM>> ByRoom { get; init; } = new();
        }

        public record FloorVM
        {
            public string Floor { get; set; } = string.Empty;
            public List<RoomSelectVM> Rooms { get; set; } = new();
        }

        public record RoomSelectVM
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public PricingType PricingType { get; set; }
        }

        public record RoomByFloorQueryVM
{
    public string Building { get; init; } = string.Empty;
    public string Floor { get; init; } = string.Empty;
}

public record RoomSlotQueryVM
{
    public Guid RoomId { get; init; }
    public DateOnly  Date { get; init; }   // yyyy-MM-dd
}

public record RoomSlotVM
{
    public string Key { get; init; } = string.Empty;
    public string? Name { get; init; }          // Period 用
    public TimeOnly StartTime { get; init; }
    public TimeOnly EndTime { get; init; }
    public decimal Price { get; init; }
    public bool Occupied { get; init; }
}




public List<BuildingFloorVM> RoomBuildingFloors()
{
    // 1️⃣ 先拉平資料（避免 MySQL OUTER APPLY）
    var data = db.SysRoom
        .AsNoTracking()
        .WhereNotDeleted()
        .Where(x =>
            x.Status != RoomStatus.Maintenance &&
            x.Building != null &&
            x.Floor != null
        )
        .Select(x => new
        {
            x.Building,
            x.Floor
        })
        .ToList();   // ⚠️ 關鍵：在這裡中斷 IQueryable

    // 2️⃣ 在記憶體中 GroupBy（C#）
    return data
        .GroupBy(x => x.Building!)
        .Select(bg => new BuildingFloorVM
        {
            Building = bg.Key,
            Floors = bg
                .Select(x => x.Floor!)
                .Distinct()
                .OrderBy(f => f)
                .ToList()
        })
        .OrderBy(b => b.Building)
        .ToList();
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


public IEnumerable<RoomSlotVM> RoomSlots(RoomSlotQueryVM query)
{
    var room = db.SysRoom
        .AsNoTracking()
        .WhereNotDeleted()
        .FirstOrDefault(x => x.Id == query.RoomId);

    if (room == null)
        return [];

    // 1️⃣ 可販售時段（全部使用 TimeSpan）
    var baseSlots = room.PricingType == PricingType.Hourly
        ? db.SysRoomPriceHourly
            .AsNoTracking()
            .Where(x =>
                x.RoomId == query.RoomId &&
                x.IsEnabled &&
                x.DeleteAt == null
            )
            .Select(x => new
            {
                Name = (string?)null,
                Start = x.StartTime, // TimeSpan
                End = x.EndTime,     // TimeSpan
                x.Price
            })
            .ToList()
        : db.SysRoomPricePeriod
            .AsNoTracking()
            .Where(x =>
                x.RoomId == query.RoomId &&
                x.IsEnabled &&
                x.DeleteAt == null
            )
            .Select(x => new
            {
                x.Name,
                Start = x.StartTime, // TimeSpan
                End = x.EndTime,     // TimeSpan
                x.Price
            })
            .ToList();

    // 2️⃣ 已佔用時段（TimeSpan）
    var occupiedSlots = db.ConferenceRoomSlot
        .AsNoTracking()
        .Where(x =>
            x.RoomId == query.RoomId &&
            x.SlotDate == query.Date
        )
        .Select(x => new
        {
            x.StartTime, // TimeSpan
            x.EndTime    // TimeSpan
        })
        .ToList();

    // 3️⃣ 比較 TimeSpan（⚠️這裡完全不出現 TimeOnly）
    var result = baseSlots
        .OrderBy(x => x.Start)
        .Select(s => new
        {
            s.Name,
            s.Start,
            s.End,
            s.Price,
            Occupied = occupiedSlots.Any(o =>
{
                var oStart = o.StartTime.ToTimeSpan();
                var oEnd = o.EndTime.ToTimeSpan();

                return !(oEnd <= s.Start || oStart >= s.End);
            })
        })
        .ToList();

    // 4️⃣ 最後一步：只在「回傳 VM」時轉成 TimeOnly
    return result.Select(s => new RoomSlotVM
    {
        Key = $"{s.Start}-{s.End}",
        Name = s.Name,
        StartTime = TimeOnly.FromTimeSpan(s.Start),
        EndTime = TimeOnly.FromTimeSpan(s.End),
        Price = s.Price,
        Occupied = s.Occupied
    }).ToList();
}

        
public IEnumerable<RoomSelectVM> RoomsByFloor(RoomByFloorQueryVM query)
{
    // ===== Debug 1：確認參數 =====
    Console.WriteLine("=== RoomsByFloor Debug ===");
    Console.WriteLine($"Building = '{query.Building}'");
    Console.WriteLine($"Floor    = '{query.Floor}'");

    // ===== Debug 2：先不加條件，看 DB 到底有什麼 =====
    var all = db.SysRoom
        .AsNoTracking()
        .WhereNotDeleted()
        .Select(x => new
        {
            x.Id,
            x.Name,
            x.Building,
            x.Floor,
            x.Status
        })
        .ToList();

    Console.WriteLine($"SysRoom 總筆數 = {all.Count}");

    foreach (var r in all)
    {
        Console.WriteLine(
            $"Room: {r.Name}, Building={r.Building}, Floor={r.Floor}, Status={r.Status}"
        );
    }

    // ===== Debug 3：真正套條件 =====
    var result = db.SysRoom
        .AsNoTracking()
        .WhereNotDeleted()
        .Where(x =>
            x.Status != RoomStatus.Maintenance &&
            x.Building == query.Building &&
            x.Floor == query.Floor
        )
        .OrderBy(x => x.Name)
        .Select(x => new RoomSelectVM
        {
            Id = x.Id,
            Name = x.Name,
            PricingType = x.PricingType
        })
        .ToList();

    Console.WriteLine($"符合條件筆數 = {result.Count}");
    Console.WriteLine("==========================");

    return result;
}

       public IQueryable<RoomListVM> RoomList(SysRoomQueryVM query)
        {


            var q = db.SysRoom
                .AsNoTracking()
                .WhereNotDeleted()
                .WhereEnabled()
                .Where(x => x.Status != RoomStatus.Maintenance);

            if (!string.IsNullOrWhiteSpace(query.Building))
            {
                q = q.Where(x => x.Building == query.Building);
            }

            if (!string.IsNullOrWhiteSpace(query.Floor))
            {
                q = q.Where(x => x.Floor == query.Floor);
            }

            if (!string.IsNullOrWhiteSpace(query.Keyword))
            {
                q = q.Where(x => x.Name.Contains(query.Keyword));
            }

            return q.Select(x => new RoomListVM
            {
                Id = x.Id,
                Name = x.Name,
                Building = x.Building,
                Floor = x.Floor,
                Capacity = x.Capacity,
                Area = x.Area,
                Status = x.Status,
                EquipmentCount = x.Equipment.Count(e => e.DeleteAt == null),
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

        public record EquipmentListVM
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string? ProductModel { get; set; }
            public string? TypeName { get; set; }
            public Guid? RoomId { get; set; }
            public string? RoomName { get; set; }
            public decimal RentalPrice { get; set; }
            public bool IsEnabled { get; set; }
        }

        private static string GetEquipmentTypeName(byte type)
        {
            return type switch
            {
                1 => "影像設備",
                2 => "聲音設備",
                3 => "控制設備",
                4 => "分配器",
                8 => "公有設備",
                9 => "攤位租借",
                _ => "未知"
            };
        }

public EquipmentGroupVM EquipmentByRooms(EquipmentByRoomsQueryVM query)
{
    Console.WriteLine("=== [EquipmentByRooms] Start ===");
    
    var sharedEquipment = db.Equipment
        .AsNoTracking()
        .WhereNotDeleted()
        .Where(x => x.IsEnabled)
        .Where(x => x.Type == 8 || x.Type == 9)
        .Where(x => x.RoomId == null)
        .Select(x => new EquipmentListVM
        {
            Id = x.Id,
            Name = x.Name,
            ProductModel = x.ProductModel,
            TypeName = GetEquipmentTypeName(x.Type),
            RoomId = x.RoomId,
            RoomName = x.Room != null ? x.Room.Name : null,
            RentalPrice = x.RentalPrice,
            IsEnabled = x.IsEnabled
        })
        .ToList();

    var byRoomEquipment = new Dictionary<Guid, List<EquipmentListVM>>();

    if (query.RoomIds != null && query.RoomIds.Length > 0)
    {
        foreach (var roomId in query.RoomIds)
        {
            var roomSpecificEquipment = db.Equipment
                .AsNoTracking()
                .WhereNotDeleted()
                .Where(x => x.IsEnabled)
                .Where(x => x.Type == 8 || x.Type == 9)
                .Where(x => x.RoomId == roomId)
                .Select(x => new EquipmentListVM
                {
                    Id = x.Id,
                    Name = x.Name,
                    ProductModel = x.ProductModel,
                    TypeName = GetEquipmentTypeName(x.Type),
                    RoomId = x.RoomId,
                    RoomName = x.Room != null ? x.Room.Name : null,
                    RentalPrice = x.RentalPrice,
                    IsEnabled = x.IsEnabled
                })
                .ToList();

            byRoomEquipment[roomId] = roomSpecificEquipment;
        }
    }

    var result = new EquipmentGroupVM
    {
        Shared = sharedEquipment,
        ByRoom = byRoomEquipment
    };

    return result;
}
        public IEnumerable<EquipmentListVM> EquipmentByRoom(Guid? roomId = null)
        {
            // ✅ 顯示 request 參數
            Console.WriteLine($"[EquipmentByRoom] Request Start" + roomId);
            Console.WriteLine($"  roomId: {(roomId.HasValue ? roomId.ToString() : "null (共用設備)")}");
            
            try
            {
                var result = db.Equipment
                    .AsNoTracking()
                    .WhereNotDeleted()
                    .Where(x => x.IsEnabled)  // 只取啟用的
                    .Where(x => x.Type == 8 || x.Type == 9)  // 只取設備(8) 和 攤位(9)
                    .Where(x => 
                        x.RoomId == null ||  // 共用設備
                        (roomId.HasValue && x.RoomId == roomId)  // 該房間專屬
                    )
                    .Select(x => new EquipmentListVM
                    {
                        Id = x.Id,
                        Name = x.Name,
                        ProductModel = x.ProductModel,
                        TypeName = GetEquipmentTypeName(x.Type),
                        RoomId = x.RoomId,
                        RoomName = x.Room != null ? x.Room.Name : null,
                        RentalPrice = x.RentalPrice,
                        IsEnabled = x.IsEnabled
                    })
                    .ToList();

                // ✅ 顯示查詢結果統計
                Console.WriteLine($"[EquipmentByRoom] Found {result.Count()} items:");
                Console.WriteLine($"  - 設備: {result.Count(x => x.TypeName != "攤位租借")} 個");
                Console.WriteLine($"  - 攤位: {result.Count(x => x.TypeName == "攤位租借")} 個");
                
                // ✅ 詳細列出每一項
                foreach (var item in result)
                {
                    Console.WriteLine($"    [{item.TypeName}] {item.Name} (RoomId: {(item.RoomId.HasValue ? item.RoomId.ToString() : "null")}) - ${item.RentalPrice}");
                }

                Console.WriteLine($"[EquipmentByRoom] Request End\n");

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EquipmentByRoom] ❌ Error: {ex.Message}");
                Console.WriteLine($"  Stack: {ex.StackTrace}\n");
                throw;
            }
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