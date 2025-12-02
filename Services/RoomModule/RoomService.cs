using Microsoft.EntityFrameworkCore;
using TASA.Extensions;
using TASA.Models;
using TASA.Program;
using System.Text.Json;

namespace TASA.Services.RoomModule
{
    public class RoomService(TASAContext db, ServiceWrapper service) : IService
    {
        public record ListVM
        {
            public uint No { get; set; }
            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string? Building { get; set; }
            public byte? Floor { get; set; }
            public uint Capacity { get; set; }
            public decimal Area { get; set; }
            public string? Status { get; set; }
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
            public string? Building { get; set; }
            public byte? Floor { get; set; }
            public string? Number { get; set; }
            public string? Description { get; set; }
            public uint Capacity { get; set; }
            public decimal Area { get; set; }
            public string? Image { get; set; }
            public string? Status { get; set; }
            public string? PricingType { get; set; } // "hourly" 或 "period"
            public bool IsEnabled { get; set; }
            public object? BookingSettings { get; set; } // JSON 格式
            public List<PricingDetailVM>? PricingDetails { get; set; } // 收費詳情
        }

        public record PricingDetailVM
        {
            public string? Name { get; set; }
            public string? StartTime { get; set; }
            public string? EndTime { get; set; }
            public decimal Price { get; set; }
            public bool Enabled { get; set; }
        }

        public DetailVM? Detail(Guid id)
        {
            var room = db.SysRoom
                .AsNoTracking()
                .WhereNotDeleted()
                .FirstOrDefault(x => x.Id == id);

            if (room == null) return null;

            var detailVM = new DetailVM
            {
                Id = room.Id,
                Name = room.Name,
                Building = room.Building,
                Floor = room.Floor,
                Number = room.Number,
                Description = room.Description,
                Capacity = room.Capacity,
                Area = room.Area,
                Image = room.Image,
                Status = room.Status,
                PricingType = room.PricingType,
                IsEnabled = room.IsEnabled,
                BookingSettings = room.BookingSettings != null
                    ? JsonSerializer.Deserialize<object>(room.BookingSettings)
                    : null,
                PricingDetails = new List<PricingDetailVM>()
            };

            // 取得收費詳情
            if (room.PricingType == "hourly")
            {
                var hourlyPricing = db.SysRoomPriceHourly
                    .AsNoTracking()
                    .Where(x => x.RoomId == room.Id)
                    .OrderBy(x => x.StartTime)
                    .Select(x => new PricingDetailVM
                    {
                        Name = $"{x.StartTime:hh\\:mm} - {x.EndTime:hh\\:mm}",
                        StartTime = x.StartTime.ToString(@"hh\:mm"),
                        EndTime = x.EndTime.ToString(@"hh\:mm"),
                        Price = x.Price,
                        Enabled = x.IsEnabled
                    })
                    .ToList();

                detailVM.PricingDetails = hourlyPricing;
            }
            else if (room.PricingType == "period")
            {
                var periodPricing = db.SysRoomPricePeriod
                    .AsNoTracking()
                    .Where(x => x.RoomId == room.Id)
                    .OrderBy(x => x.StartTime)
                    .Select(x => new PricingDetailVM
                    {
                        Name = x.Name,
                        StartTime = x.StartTime.ToString(@"hh\:mm"),
                        EndTime = x.EndTime.ToString(@"hh\:mm"),
                        Price = x.Price,
                        Enabled = x.IsEnabled
                    })
                    .ToList();

                detailVM.PricingDetails = periodPricing;
            }

            return detailVM;
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
                Name = vm.Name,
                Building = vm.Building,
                Floor = vm.Floor,
                Number = vm.Number,
                Description = vm.Description,
                Capacity = vm.Capacity,
                Area = vm.Area,
                Image = vm.Image,
                Status = vm.Status ?? "available",
                PricingType = vm.PricingType ?? "hourly",
                BookingSettings = vm.BookingSettings != null
                    ? JsonSerializer.Serialize(vm.BookingSettings)
                    : null,
                IsEnabled = vm.IsEnabled,
                CreateAt = DateTime.Now,
                CreateBy = userid!.Value
            };

            db.SysRoom.Add(newSysRoom);
            db.SaveChanges();

            // 新增收費設定
            SavePricingDetails(newSysRoom.Id, vm.PricingType, vm.PricingDetails);

            _ = service.LogServices.LogAsync("會議室新增",
                $"{newSysRoom.Name}({newSysRoom.Id}) IsEnabled:{newSysRoom.IsEnabled} PricingType:{newSysRoom.PricingType}");
        }

        public void Update(DetailVM vm)
        {
            var data = db.SysRoom
                .WhereNotDeleted()
                .FirstOrDefault(x => x.Id == vm.Id);

            if (data != null)
            {
                data.Name = vm.Name;
                data.Building = vm.Building;
                data.Floor = vm.Floor;
                data.Number = vm.Number;
                data.Description = vm.Description;
                data.Capacity = vm.Capacity;
                data.Area = vm.Area;
                data.Image = vm.Image;
                data.Status = vm.Status ?? "available";
                data.PricingType = vm.PricingType ?? "hourly";
                data.BookingSettings = vm.BookingSettings != null
                    ? JsonSerializer.Serialize(vm.BookingSettings)
                    : null;
                data.IsEnabled = vm.IsEnabled;

                db.SaveChanges();

                // 更新收費設定（先刪除再新增）
                DeletePricingDetails(data.Id, data.PricingType);
                SavePricingDetails(data.Id, vm.PricingType, vm.PricingDetails);

                _ = service.LogServices.LogAsync("會議室編輯",
                    $"{data.Name}({data.Id}) IsEnabled:{data.IsEnabled} PricingType:{data.PricingType}");
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

                // 軟刪除關聯的收費設定
                var hourlyPrices = db.SysRoomPriceHourly
                    .Where(x => x.RoomId == data.Id)
                    .ToList();
                foreach (var price in hourlyPrices)
                {
                    price.DeleteAt = DateTime.UtcNow;
                }

                var periodPrices = db.SysRoomPricePeriod
                    .Where(x => x.RoomId == data.Id)
                    .ToList();
                foreach (var price in periodPrices)
                {
                    price.DeleteAt = DateTime.UtcNow;
                }

                db.SaveChanges();
                _ = service.LogServices.LogAsync("會議室刪除", $"{data.Name}({data.Id})");
            }
        }

        /// <summary>
        /// 保存收費設定
        /// </summary>
        private void SavePricingDetails(Guid roomId, string? pricingType, List<PricingDetailVM>? pricingDetails)
        {
            if (string.IsNullOrEmpty(pricingType) || pricingDetails == null || pricingDetails.Count == 0)
                return;

            var userid = service.UserClaimsService.Me()?.Id;

            if (pricingType == "hourly")
            {
                foreach (var pricing in pricingDetails.Where(p => p.Enabled))
                {
                    if (TimeSpan.TryParse(pricing.StartTime, out var startTime) &&
                        TimeSpan.TryParse(pricing.EndTime, out var endTime))
                    {
                        var hourlyPrice = new SysRoomPriceHourly
                        {
                            Id = Guid.NewGuid(),
                            RoomId = roomId,
                            StartTime = startTime,
                            EndTime = endTime,
                            Price = pricing.Price,
                            IsEnabled = pricing.Enabled,
                            CreateAt = DateTime.Now,
                            CreateBy = userid!.Value
                        };
                        db.SysRoomPriceHourly.Add(hourlyPrice);
                    }
                }
            }
            else if (pricingType == "period")
            {
                foreach (var pricing in pricingDetails.Where(p => p.Enabled))
                {
                    if (TimeSpan.TryParse(pricing.StartTime, out var startTime) &&
                        TimeSpan.TryParse(pricing.EndTime, out var endTime))
                    {
                        var periodPrice = new SysRoomPricePeriod
                        {
                            Id = Guid.NewGuid(),
                            RoomId = roomId,
                            Name = pricing.Name ?? "未命名時段",
                            StartTime = startTime,
                            EndTime = endTime,
                            Price = pricing.Price,
                            IsEnabled = pricing.Enabled,
                            CreateAt = DateTime.Now,
                            CreateBy = userid!.Value
                        };
                        db.SysRoomPricePeriod.Add(periodPrice);
                    }
                }
            }

            db.SaveChanges();
        }

        /// <summary>
        /// 刪除收費設定
        /// </summary>
        private void DeletePricingDetails(Guid roomId, string? pricingType)
        {
            if (pricingType == "hourly")
            {
                var hourlyPrices = db.SysRoomPriceHourly
                    .Where(x => x.RoomId == roomId && x.DeleteAt == null)
                    .ToList();
                foreach (var price in hourlyPrices)
                {
                    price.DeleteAt = DateTime.UtcNow;
                }
            }
            else if (pricingType == "period")
            {
                var periodPrices = db.SysRoomPricePeriod
                    .Where(x => x.RoomId == roomId && x.DeleteAt == null)
                    .ToList();
                foreach (var price in periodPrices)
                {
                    price.DeleteAt = DateTime.UtcNow;
                }
            }

            db.SaveChanges();
        }
    }
}