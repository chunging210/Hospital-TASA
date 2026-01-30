using Microsoft.EntityFrameworkCore;
using TASA.Extensions;
using TASA.Models;
using TASA.Program;
using TASA.Models.Auth;
namespace TASA.Services.EquipmentModule
{
    public class EquipmentService(TASAContext db, ServiceWrapper service) : IService
    {

        // ========= 列表 ViewModel =========
        public record ListVM
        {
            public Guid Id { get; set; }
            public string? Name { get; set; } = string.Empty;
            public string? ProductModel { get; set; }
            public string? TypeName { get; set; } = string.Empty;
            public string? Building { get; set; }
            public string? Floor { get; set; }
            public string? RoomName { get; set; }
            public int RentalPrice { get; set; }
            public bool IsEnabled { get; set; }
            public DateTime CreateAt { get; set; }
            public Guid? DepartmentId { get; set; }
        }

        public IQueryable<ListVM> List(EquipmentQueryVM query)
        {
            Console.WriteLine("EquipmentService.List 被調用");

            var q = db.Equipment
                .AsNoTracking()
                .WhereNotDeleted()
                .WhereIf(query.IsEnabled.HasValue, x => x.IsEnabled == query.IsEnabled)
                .WhereIf(query.Keyword, x => x.Name.Contains(query.Keyword!))
                .WhereIf(!string.IsNullOrEmpty(query.Type), x => x.Type.ToString() == query.Type);


            return q.Select(x => new ListVM
            {
                Id = x.Id,
                Name = x.Name,
                ProductModel = x.ProductModel,
                TypeName = GetEquipmentTypeName(x.Type),
                Building = x.Room != null ? x.Room.Building : null,
                Floor = x.Room != null ? x.Room.Floor : null,
                RoomName = x.Room != null ? x.Room.Name : null,
                RentalPrice = x.RentalPrice,
                IsEnabled = x.IsEnabled,
                CreateAt = x.CreateAt,
                DepartmentId = x.DepartmentId
            })
            .AsQueryable();
        }

        // ========= 詳細 ViewModel =========
        public record DetailVM
        {
            public Guid? Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string? ProductModel { get; set; }
            public int Type { get; set; } = 3;
            public Guid? RoomId { get; set; }
            public string? Building { get; set; }
            public string? Floor { get; set; }
            public int RentalPrice { get; set; }
            public string? Host { get; set; }
            public int? Port { get; set; }
            public string? Account { get; set; }
            public string? Password { get; set; }
            public bool IsEnabled { get; set; } = true;
            public Guid? DepartmentId { get; set; }
        }

        public DetailVM? Detail(Guid id)
        {
            var equipment = db.Equipment
                .AsNoTracking()
                .WhereNotDeleted()
                .Where(x => x.Id == id)
                .Select(x => new DetailVM
                {
                    Id = x.Id,
                    Name = x.Name,
                    ProductModel = x.ProductModel,
                    Type = x.Type,
                    RoomId = x.RoomId,
                    Building = x.Room != null ? x.Room.Building : null,
                    Floor = x.Room != null ? x.Room.Floor : null,
                    DepartmentId = x.DepartmentId,
                    RentalPrice = x.RentalPrice,
                    Host = x.Host,
                    Port = x.Port,
                    Account = x.Account,
                    Password = x.Password,
                    IsEnabled = x.IsEnabled
                })
                .FirstOrDefault();

            // ✅ 權限檢查:非管理者只能查看自己分院的設備
            if (equipment != null)
            {
                var currentUser = service.UserClaimsService.Me();
                if (currentUser != null && !currentUser.IsAdmin && currentUser.DepartmentId.HasValue)
                {
                    if (equipment.DepartmentId != currentUser.DepartmentId)
                    {
                        throw new HttpException("您沒有權限查看此設備");
                    }
                }
            }

            return equipment;
        }

        /// <summary>
        /// 統一驗證方法 - 新增/編輯都使用
        /// </summary>
        private void ValidateEquipment(DetailVM vm)
        {
            // ✅ 1️⃣ 驗證：RoomId 存在性（若有提供）
            if (vm.RoomId.HasValue)
            {
                var room = db.SysRoom
                    .WhereNotDeleted()
                    .FirstOrDefault(x => x.Id == vm.RoomId);

                if (room == null)
                    throw new HttpException("選擇的會議室不存在");

                // ✅ 權限檢查:非管理者只能選擇自己分院的會議室
                var currentUser = service.UserClaimsService.Me();
                if (currentUser != null && !currentUser.IsAdmin && currentUser.DepartmentId.HasValue)
                {
                    if (room.DepartmentId != currentUser.DepartmentId)
                    {
                        throw new HttpException("您只能選擇自己分院的會議室");
                    }
                }
            }

            // ✅ 2️⃣ 驗證：公有設備(8)/攤位租借(9) 必須有租借金額
            if ((vm.Type == 8 || vm.Type == 9) && vm.RentalPrice <= 0)
            {
                throw new HttpException("公有設備和攤位租借必須設定租借金額");
            }

            // ✅ 3️⃣ 檢核重複（根據設備類型檢核不同欄位）
            if (vm.Type == 9)
            {
                // 攤位租借(9)：檢核名稱重複（排除自己）
                if (db.Equipment.WhereNotDeleted().Any(x => x.Name == vm.Name && x.Id != vm.Id))
                {
                    throw new HttpException("此攤位名稱已存在");
                }
            }
            else if ((vm.Type >= 1 && vm.Type <= 4) || vm.Type == 8)
            {
                // 其他類型(1,2,3,4,8)：檢核型號
                if (string.IsNullOrWhiteSpace(vm.ProductModel))
                {
                    throw new HttpException("產品型號為必填");
                }

                if (db.Equipment.WhereNotDeleted().Any(x => x.ProductModel == vm.ProductModel && x.Id != vm.Id))
                {
                    throw new HttpException("此產品型號已存在");
                }
            }
        }
        /// <summary>
        /// 新增設備
        /// </summary>
        public void Insert(DetailVM vm)
        {
            var currentUser = service.UserClaimsService.Me();
            if (currentUser?.Id == null)
                throw new HttpException("無法取得使用者資訊");

            // ✅ 統一驗證
            ValidateEquipment(vm);

            // ✅ 決定 DepartmentId
            Guid? departmentId = null;

            if (vm.RoomId.HasValue)
            {
                // 如果有會議室,用會議室的分院
                var room = db.SysRoom
                    .WhereNotDeleted()
                    .FirstOrDefault(x => x.Id == vm.RoomId);
                departmentId = room?.DepartmentId;
            }
            else
            {
                // 如果沒有會議室(公有設備),用使用者的分院
                departmentId = currentUser.DepartmentId;
            }

            // ✅ 權限檢查:非管理者強制使用自己的分院
            if (!currentUser.IsAdmin && currentUser.DepartmentId.HasValue)
            {
                if (departmentId != currentUser.DepartmentId)
                {
                    throw new HttpException("您只能新增自己分院的設備");
                }
            }

            var newEquipment = new Equipment()
            {
                Id = Guid.NewGuid(),
                Name = vm.Name,
                ProductModel = vm.ProductModel,
                Type = (byte)vm.Type,
                RoomId = vm.RoomId,
                DepartmentId = departmentId,  // ✅ 設定分院ID
                RentalPrice = vm.RentalPrice,
                Host = vm.Host,
                Port = vm.Port,
                Account = vm.Account,
                Password = vm.Password,
                IsEnabled = vm.IsEnabled,
                CreateAt = DateTime.Now,
                CreateBy = currentUser.Id.Value
            };

            db.Equipment.Add(newEquipment);
            db.SaveChanges();

            var deptInfo = departmentId.HasValue ? $"分院: {departmentId}" : "未指派分院";
            var roomInfo = vm.RoomId.HasValue ? $"會議室: {vm.RoomId}" : "公有設備";
            _ = service.LogServices.LogAsync("設備新增",
                $"{newEquipment.Name}({newEquipment.Id}) {deptInfo} {roomInfo} IsEnabled:{newEquipment.IsEnabled}");
        }
        /// <summary>
        /// 編輯設備
        /// </summary>
        public void Update(DetailVM vm)
        {
            if (!vm.Id.HasValue)
                throw new HttpException("設備ID不能為空");

            var data = db.Equipment
                .WhereNotDeleted()
                .FirstOrDefault(x => x.Id == vm.Id);

            if (data == null)
                throw new HttpException("設備不存在");

            // ✅ 權限檢查:非管理者只能編輯自己分院的設備
            var currentUser = service.UserClaimsService.Me();
            if (currentUser != null && !currentUser.IsAdmin && currentUser.DepartmentId.HasValue)
            {
                if (data.DepartmentId != currentUser.DepartmentId)
                {
                    throw new HttpException("您沒有權限編輯此設備");
                }
            }

            // ✅ 統一驗證
            ValidateEquipment(vm);

            // ✅ 更新 DepartmentId
            Guid? departmentId = null;
            if (vm.RoomId.HasValue)
            {
                var room = db.SysRoom
                    .WhereNotDeleted()
                    .FirstOrDefault(x => x.Id == vm.RoomId);
                departmentId = room?.DepartmentId;
            }
            else
            {
                // 公有設備:保持原有分院或使用使用者分院
                departmentId = data.DepartmentId ?? currentUser?.DepartmentId;
            }

            data.Name = vm.Name;
            data.ProductModel = vm.ProductModel;
            data.Type = (byte)vm.Type;
            data.RoomId = vm.RoomId;
            data.DepartmentId = departmentId;  // ✅ 更新分院ID
            data.RentalPrice = vm.RentalPrice;
            data.Host = vm.Host;
            data.Port = vm.Port;
            data.Account = vm.Account;
            data.Password = vm.Password;
            data.IsEnabled = vm.IsEnabled;

            db.SaveChanges();

            var deptInfo = departmentId.HasValue ? $"分院: {departmentId}" : "未指派分院";
            var roomInfo = vm.RoomId.HasValue ? $"會議室: {vm.RoomId}" : "公有設備";
            _ = service.LogServices.LogAsync("設備編輯",
                $"{data.Name}({data.Id}) {deptInfo} {roomInfo} IsEnabled:{data.IsEnabled}");
        }
        /// <summary>
        /// 刪除設備
        /// </summary>
        public void Delete(Guid id)
        {
            var data = db.Equipment
                .WhereNotDeleted()
                .FirstOrDefault(x => x.Id == id);

            if (data == null)
                throw new HttpException("設備不存在");

            // ✅ 權限檢查:非管理者只能刪除自己分院的設備
            var currentUser = service.UserClaimsService.Me();
            if (currentUser != null && !currentUser.IsAdmin && currentUser.DepartmentId.HasValue)
            {
                if (data.DepartmentId != currentUser.DepartmentId)
                {
                    throw new HttpException("您沒有權限刪除此設備");
                }
            }

            data.DeleteAt = DateTime.UtcNow;
            db.SaveChanges();
            _ = service.LogServices.LogAsync("設備刪除", $"{data.Name}({data.Id})");
        }

        /// <summary>
        /// 根據會議室取得設備列表
        /// </summary>
        public IQueryable<ListVM> GetEquipmentsByRoom(Guid roomId)
        {
            return db.Equipment
                .AsNoTracking()
                .WhereNotDeleted()
                .Where(x => x.RoomId == roomId)
                .Select(x => new ListVM
                {
                    Id = x.Id,
                    Name = x.Name,
                    ProductModel = x.ProductModel,
                    TypeName = GetEquipmentTypeName(x.Type),
                    Building = x.Room != null ? x.Room.Building : null,
                    Floor = x.Room != null ? x.Room.Floor : null,
                    RoomName = x.Room != null ? x.Room.Name : null,
                    RentalPrice = x.RentalPrice,
                    IsEnabled = x.IsEnabled,
                    CreateAt = x.CreateAt,
                    DepartmentId = x.DepartmentId
                })
                .AsQueryable();
        }

        /// <summary>
        /// 將 Type (byte) 轉換為設備類型名稱
        /// </summary>
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
    }
}