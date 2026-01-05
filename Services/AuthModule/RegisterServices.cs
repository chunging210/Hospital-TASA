using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using TASA.Extensions;
using TASA.Models;
using TASA.Program;
using System.Net;
using Newtonsoft.Json;

namespace TASA.Services.AuthModule
{
    public class RegisterServices(TASAContext db, ServiceWrapper service) : IService
    {
        /* ===============================
         * Register VM
         * =============================== */
        public record RegisterVM
        {
            [Required]
            public string Name { get; set; } = string.Empty;

            [Required]
            [EmailAddress]
            public string Email { get; set; } = string.Empty;

            [Required]
            public string Password { get; set; } = string.Empty;

            [Required]
            public string ConfirmPassword { get; set; } = string.Empty;

            /// <summary>
            /// 分院 Id（null = 一般會員）
            /// </summary>
            public Guid? DepartmentId { get; set; }
        }

        /* ===============================
         * Register
         * =============================== */
        public void Register(RegisterVM vm)
        {
            // 0️⃣ 密碼確認
            if (vm.Password != vm.ConfirmPassword)
            {
                var failureInfo = new { UserName = vm.Email, IsSuccess = false, FailureReason = "密碼不符" };
                _ = service.LogServices.LogAsync("user_register_failed", JsonConvert.SerializeObject(failureInfo));
                throw new HttpException("兩次輸入的密碼不一致")
                {
                    StatusCode = System.Net.HttpStatusCode.BadRequest
                };
            }

            // 1️⃣ 檢查帳號 / Email 是否已存在
            var exists = db.AuthUser
                .AsNoTracking()
                .WhereNotDeleted()
                .Any(x => x.Account == vm.Email || x.Email == vm.Email);

            if (exists)
            {
                var failureInfo = new { UserName = vm.Email, IsSuccess = false, FailureReason = "Email已存在" };
                _ = service.LogServices.LogAsync("user_register_failed", JsonConvert.SerializeObject(failureInfo));
                throw new HttpException("此 Email 已被註冊")
                {
                    StatusCode = System.Net.HttpStatusCode.BadRequest
                };
            }

            // 2️⃣ 取得「一般使用者」角色
            var normalRole = db.AuthRole
                .FirstOrDefault(x => x.Code == "NORMAL");

            if (normalRole == null)
            {
                throw new HttpException("系統尚未設定一般使用者角色")
                {
                    StatusCode = System.Net.HttpStatusCode.InternalServerError
                };
            }

            // 3️⃣ 分院（可為 null）
            SysDepartment? department = null;
            if (vm.DepartmentId.HasValue)
            {
                department = db.SysDepartment
                    .AsNoTracking()
                    .FirstOrDefault(x => x.Id == vm.DepartmentId.Value);

                if (department == null)
                {
                    throw new HttpException("所選分院不存在")
                    {
                        StatusCode = HttpStatusCode.BadRequest
                    };
                }
            }

            // 4️⃣ 密碼加密
            var hashVm = HashString.Hash(vm.Password);

            // 5️⃣ 建立使用者
            var user = new AuthUser
            {
                Id = Guid.NewGuid(),
                Account = vm.Email,
                Email = vm.Email,
                Name = vm.Name,
                PasswordHash = hashVm.Hash,
                PasswordSalt = hashVm.Salt,
                DepartmentId = department?.Id,
                IsEnabled = true,
                CreateAt = DateTime.Now,
            };

            user.AuthRole.Add(normalRole);

            db.AuthUser.Add(user);
            db.SaveChanges();

            // 6️⃣ 紀錄 Log
            var successInfo = new { UserName = user.Account, Email = user.Email, IsSuccess = true };
            _ = service.LogServices.LogAsync("user_register_success", JsonConvert.SerializeObject(successInfo));
        }

    }
}
